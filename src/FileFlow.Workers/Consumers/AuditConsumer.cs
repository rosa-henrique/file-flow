using DotNetCore.CAP;

using FileFlow.Data.Context;
using FileFlow.Data.Entities;
using FileFlow.Shared.Contracts;

using Microsoft.EntityFrameworkCore;

namespace FileFlow.Workers.Consumers;

public class AuditConsumer(FileFlowDbContext dbContext, ILogger<AuditConsumer> logger) : ICapSubscribe
{
    [CapSubscribe("file.uploaded", Group = "fileflow.workers.audit")]
    public async Task OnFileUploaded(FileUploadedEvent @event)
    {
        logger.LogInformation("Iniciando criação de log de inicio de processamento para {@Event}", @event);

        var log = MediaAssetLog.Create(@event.MediaAssetId,
            MediaAssetEventType.MIGRATION_STARTED,
            "Upload iniciado",
            @event.TempPath,
            @event.TempBucket);

        dbContext.MediaAssetLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }

    [CapSubscribe("file.migration.completed", Group = "fileflow.workers.audit")]
    public async Task OnFileMigrated(FileMigrationCompletedEvent @event)
    {
        logger.LogInformation("Iniciando criação de log de arquivo migrado para {@Event}", @event);

        var log = MediaAssetLog.CreateComplete(@event.MediaAssetId,
            "Upload finalizado",
            @event.TempPath,
            @event.TempBucket,
            @event.FinalPath,
            @event.FinalBucket,
            @event.CompletedAt);

        dbContext.MediaAssetLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }

    [CapSubscribe("file.migration.cleaned", Group = "fileflow.workers.audit")]
    public async Task OnFileCleaned(FileCleanedEvent @event)
    {
        var log = MediaAssetLog.Create(@event.MediaAssetId,
            MediaAssetEventType.DELETED,
            "Arquivo apagado do bucket temporário",
            @event.TempPath,
            @event.TempBucket,
            @event.CleanedAt);

        dbContext.MediaAssetLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }

    [CapSubscribe("file.uploaded.failed", Group = "fileflow.workers.audit")]
    public async Task OnFileMigrateFailed(FileMigrationFailedEvent @event)
    {
        var log = MediaAssetLog.Create(@event.MediaAssetId,
            MediaAssetEventType.MIGRATION_FAILED,
            "Falha ao migrar arquivo",
            @event.TempPath,
            @event.TempBucket,
            @event.FailedAt,
            @event.Details);

        dbContext.MediaAssetLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }

    [CapSubscribe("file.uploaded.retry", Group = "fileflow.workers.audit")]
    public async Task OnRetryFileMigrate(RetryFileUploadedEvent @event)
    {
        var log = MediaAssetLog.Create(@event.MediaAssetId,
            MediaAssetEventType.MIGRATION_ATTEMPT_FAILED,
            "Tentativa de migração com erro",
            @event.TempPath,
            @event.TempBucket,
            @event.FailedAt,
            @event.Details);

        var logRetry = MediaAssetLog.Create(@event.MediaAssetId,
            MediaAssetEventType.RETRY_INITIATED,
            "Retentativa de upload iniciado",
            @event.TempPath,
            @event.TempBucket);

        dbContext.MediaAssetLogs.AddRange(log, logRetry);
        await dbContext.SaveChangesAsync();
    }
}