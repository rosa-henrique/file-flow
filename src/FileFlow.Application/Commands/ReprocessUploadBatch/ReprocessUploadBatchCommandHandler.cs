using DotNetCore.CAP;

using FileFlow.Data.Context;
using FileFlow.Data.Entities;
using FileFlow.Shared.Contracts;
using FileFlow.Shared.Exceptions;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileFlow.Application.Commands.ReprocessUploadBatch;

public class ReprocessUploadBatchCommandHandler(
    FileFlowDbContext dbContext,
    ICapPublisher capPublisher,
    IConfiguration configuration,
    ILogger<ReprocessUploadBatchCommandHandler> logger) : IRequestHandler<ReprocessUploadBatchCommand>
{
    private readonly string _bucketFinal = configuration.GetValue<string>("S3:BucketFinal")!;

    public async Task Handle(ReprocessUploadBatchCommand request, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                capPublisher,
                autoCommit: false,
                ct);

            try
            {
                var uploadBatch = await dbContext.UploadBatches
                    .Include(batch => batch.MediaAssets.Where(asset => asset.Status == MediaAssetStatus.FAILED))
                    .SingleOrDefaultAsync(batch => batch.Id == request.UploadBatchId, ct);

                if (uploadBatch is null)
                {
                    throw new NotFoundException(
                        $"Não foi encontrado batch para id {request.UploadBatchId}");
                }

                if (uploadBatch.MediaAssets.Count == 0)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }

                var failedAssets = uploadBatch.MediaAssets
                    .ToList();

                var failedAssetIds = failedAssets
                    .Select(asset => asset.Id)
                    .ToList();

                var lastLogsByAssetId = await dbContext.MediaAssetLogs
                    .Where(log => failedAssetIds.Contains(log.MediaAssetId) && log.EventType == MediaAssetEventType.MIGRATION_STARTED)
                    .GroupBy(log => log.MediaAssetId)
                    .Select(group => group
                        .OrderByDescending(log => log.Timestamp)
                        .ThenByDescending(log => log.Id)
                        .First())
                    .ToDictionaryAsync(log => log.MediaAssetId, ct);

                var startedReprocessing = false;

                foreach (var asset in failedAssets)
                {
                    if (!lastLogsByAssetId.TryGetValue(asset.Id, out var lastLog))
                    {
                        logger.LogWarning(
                            "Nao foi encontrado log de inicio de migracao para o asset {MediaAssetId} do batch {UploadBatchId}",
                            asset.Id,
                            request.UploadBatchId);
                        continue;
                    }

                    if (!startedReprocessing)
                    {
                        uploadBatch.SetToReprocess();
                        startedReprocessing = true;
                    }

                    asset.SetToReprocess();

                    await capPublisher.PublishAsync(
                        "file.uploaded",
                        new FileUploadedEvent
                        {
                            MediaAssetId = asset.Id,
                            UploadBatchId = asset.UploadBatchId,
                            OriginalFileName = asset.OriginalFileName,
                            MimeType = asset.MimeType,
                            Size = asset.Size,
                            TempPath = lastLog.TempPath,
                            Title = asset.Title,
                            Tags = asset.Tags,
                            TempBucket = lastLog.TempBucket,
                            FinalBucket = _bucketFinal,
                        },
                        cancellationToken: ct);
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                logger.LogError(ex, "Erro ao reprocessar arquivos do batch {UploadBatchId}", request.UploadBatchId);
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}