using DotNetCore.CAP;

using FileFlow.Data.Context;
using FileFlow.Data.Entities;
using FileFlow.Shared.Contracts;

using Microsoft.EntityFrameworkCore;

namespace FileFlow.Workers.Consumers;

public class DomainEventConsumer(FileFlowDbContext dbContext) : ICapSubscribe
{
    [CapSubscribe("file.uploaded", Group = "fileflow.workers.domainEvent")]
    public Task OnFileUploaded(FileUploadedEvent @event)
    {
        var taskMediaAsset = dbContext.MediaAssets
            .Where(x => x.Id == @event.MediaAssetId && x.Status == MediaAssetStatus.PENDING)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.Status, MediaAssetStatus.MIGRATING));

        var taskUploadBatch = dbContext.UploadBatches
            .Where(x => x.Id == @event.UploadBatchId && x.Status == UploadBatchStatus.PENDING)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.Status, UploadBatchStatus.PROCESSING));

        return Task.WhenAll(taskMediaAsset, taskUploadBatch);
    }

    [CapSubscribe("file.migration.completed", Group = "fileflow.workers.domainEvent")]
    public async Task OnFileMigrated(FileMigrationCompletedEvent @event)
    {
        var mediaAsset = await dbContext.MediaAssets
            .FirstOrDefaultAsync(x =>
                x.Id == @event.MediaAssetId &&
                (x.Status == MediaAssetStatus.PENDING || x.Status == MediaAssetStatus.MIGRATING));

        if (mediaAsset == null)
        {
            return;
        }

        await dbContext.MediaAssets
            .Where(x => x.Id == @event.MediaAssetId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.FinalPath, @event.FinalPath)
                        .SetProperty(m => m.FinalBucket, @event.FinalBucket)
                        .SetProperty(m => m.Status, MediaAssetStatus.MIGRATED)
                        .SetProperty(m => m.CompletedAt, @event.CompletedAt));

        await TryChangeUploadBatchOnFileFinished(mediaAsset.UploadBatchId, @event.CompletedAt);
    }

    [CapSubscribe("file.uploaded.failed", Group = "fileflow.workers.domainEvent")]
    public async Task OnFileMigrateFailed(FileMigrationFailedEvent @event)
    {
        var mediaAsset = await dbContext.MediaAssets
            .FirstOrDefaultAsync(x =>
                x.Id == @event.MediaAssetId &&
                (x.Status == MediaAssetStatus.PENDING || x.Status == MediaAssetStatus.MIGRATING));

        if (mediaAsset == null)
        {
            return;
        }

        await dbContext.MediaAssets
            .Where(x => x.Id == @event.MediaAssetId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.ErrorMessage, "Erro ao migrar arquivo")
                    .SetProperty(m => m.Status, MediaAssetStatus.FAILED)
                    .SetProperty(m => m.CompletedAt, @event.FailedAt));

        await TryChangeUploadBatchOnFileFinished(mediaAsset.UploadBatchId, @event.FailedAt);
    }

    private async Task TryChangeUploadBatchOnFileFinished(Guid uploadBatchId, DateTime completedAt)
    {
        // Cenário 1: Todos os MediaAssets estão MIGRATED → status fica COMPLETED
        var allMigrated = await dbContext.UploadBatches
            .Where(x => x.Id == uploadBatchId &&
                        (x.Status == UploadBatchStatus.PENDING || x.Status == UploadBatchStatus.PROCESSING) &&
                        x.MediaAssets.All(m => m.Status == MediaAssetStatus.MIGRATED))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.Status, UploadBatchStatus.COMPLETED)
                       .SetProperty(m => m.CompletedAt, completedAt));

        if (allMigrated > 0)
        {
            return;
        }

        // Cenário 2: Todos os MediaAssets estão FAILED → status fica FAILED
        var allFailed = await dbContext.UploadBatches
            .Where(x => x.Id == uploadBatchId &&
                        (x.Status == UploadBatchStatus.PENDING || x.Status == UploadBatchStatus.PROCESSING) &&
                        x.MediaAssets.All(m => m.Status == MediaAssetStatus.FAILED))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.Status, UploadBatchStatus.FAILED)
                       .SetProperty(m => m.CompletedAt, completedAt));

        if (allFailed > 0)
        {
            return;
        }

        // Cenário 3: Mix de FAILED e MIGRATED → status fica PARTIAL
        await dbContext.UploadBatches
            .Where(x => x.Id == uploadBatchId &&
                        (x.Status == UploadBatchStatus.PENDING || x.Status == UploadBatchStatus.PROCESSING) &&
                        x.MediaAssets.All(m => m.Status == MediaAssetStatus.MIGRATED || m.Status == MediaAssetStatus.FAILED))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(m => m.Status, UploadBatchStatus.PARTIAL)
                       .SetProperty(m => m.CompletedAt, completedAt));
    }
}