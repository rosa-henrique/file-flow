using DotNetCore.CAP;

using FileFlow.Data.Context;
using FileFlow.Data.Entities;
using FileFlow.Shared.Contracts;

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
            @event.TempPath);

        dbContext.MediaAssetLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }

    [CapSubscribe("file.migration.completed", Group = "fileflow.workers.audit")]
    public async Task OnFileMigrated(FileMigrationCompletedEvent @event)
    {
        logger.LogInformation("Iniciando criação de log de arquivo migrado para {@Event}", @event);

        var log = MediaAssetLog.Create(@event.MediaAssetId,
            MediaAssetEventType.MIGRATION_COMPLETED,
            "Upload iniciado",
            @event.TempPath,
            @event.FinalPath,
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
            @event.TempPath);

        dbContext.MediaAssetLogs.Add(log);
        await dbContext.SaveChangesAsync();
    }
}