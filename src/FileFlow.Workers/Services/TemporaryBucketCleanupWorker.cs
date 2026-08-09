using Amazon.S3;
using Amazon.S3.Model;

using FileFlow.Data.Context;
using FileFlow.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.Workers.Services;

public sealed class TemporaryBucketCleanupWorker(
    IServiceScopeFactory serviceScopeFactory,
    IAmazonS3 s3Client,
    IConfiguration configuration,
    ILogger<TemporaryBucketCleanupWorker> logger) : BackgroundService
{
    private readonly string _temporaryBucket = configuration.GetValue<string>("S3:BucketTemporary")
        ?? throw new InvalidOperationException("A configuração 'S3:BucketTemporary' é obrigatória.");
    private readonly TimeSpan _scanInterval = GetPositiveTimeSpan(configuration, "BucketCleanup:IntervalMinutes", 15);
    private readonly TimeSpan _minimumAge = GetPositiveTimeSpan(configuration, "BucketCleanup:MinimumAgeMinutes", 60);
    private readonly int _maxKeysPerPage = GetPositiveInt(configuration, "BucketCleanup:MaxKeysPerPage", 1000, 1000);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker de limpeza iniciado para o bucket temporário {BucketName} com intervalo de {Interval} e idade mínima de {MinimumAge}",
            _temporaryBucket,
            _scanInterval,
            _minimumAge);

        await ExecuteCleanupSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(_scanInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ExecuteCleanupSafelyAsync(stoppingToken);
        }
    }

    private async Task ExecuteCleanupSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CleanupTemporaryBucketAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao limpar o bucket temporário {BucketName}", _temporaryBucket);
        }
    }

    private async Task CleanupTemporaryBucketAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileFlowDbContext>();

        var threshold = DateTime.UtcNow.Subtract(_minimumAge);
        string? continuationToken = null;
        var staleObjectsCount = 0;
        var deletedObjectsCount = 0;

        do
        {
            var response = await s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _temporaryBucket,
                    ContinuationToken = continuationToken,
                    MaxKeys = _maxKeysPerPage,
                },
                stoppingToken);

            var staleObjects = response.S3Objects
                .Where(s3Object => !string.IsNullOrWhiteSpace(s3Object.Key) &&
                                   s3Object.LastModified is DateTime lastModified &&
                                   lastModified.ToUniversalTime() <= threshold)
                .ToList();

            staleObjectsCount += staleObjects.Count;

            if (staleObjects.Count > 0)
            {
                deletedObjectsCount += await DeleteStaleObjectsAsync(
                    dbContext,
                    staleObjects,
                    stoppingToken);
            }

            continuationToken = response.IsTruncated == true
                ? response.NextContinuationToken
                : null;
        }
        while (continuationToken is not null && !stoppingToken.IsCancellationRequested);

        logger.LogInformation(
            "Limpeza concluída no bucket temporário {BucketName}. Foram encontrados {StaleObjectsCount} arquivos elegíveis e excluídos {DeletedObjectsCount}",
            _temporaryBucket,
            staleObjectsCount,
            deletedObjectsCount);
    }

    private async Task<int> DeleteStaleObjectsAsync(
        FileFlowDbContext dbContext,
        IReadOnlyCollection<S3Object> staleObjects,
        CancellationToken stoppingToken)
    {
        var staleObjectKeys = staleObjects
            .Select(s3Object => s3Object.Key)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var logsByPath = (await dbContext.MediaAssetLogs
            .AsNoTracking()
            .Where(log => log.TempBucket == _temporaryBucket && staleObjectKeys.Contains(log.TempPath))
            .Select(log => new CleanupCandidateLog(log.MediaAssetId, log.TempPath, log.EventType))
            .ToListAsync(stoppingToken))
            .ToLookup(log => log.TempPath, StringComparer.Ordinal);

        var deletedObjectsCount = 0;
        var createdLogsCount = 0;

        foreach (var staleObject in staleObjects)
        {
            var relatedLogs = logsByPath[staleObject.Key].ToList();
            var hasMigrationCompletedLog = relatedLogs.Any(log => log.EventType == MediaAssetEventType.MIGRATION_COMPLETED);

            if (relatedLogs.Count > 0 && !hasMigrationCompletedLog)
            {
                continue;
            }

            await s3Client.DeleteObjectAsync(
                new DeleteObjectRequest
                {
                    BucketName = _temporaryBucket,
                    Key = staleObject.Key,
                },
                stoppingToken);

            deletedObjectsCount++;

            if (hasMigrationCompletedLog && relatedLogs.All(log => log.EventType != MediaAssetEventType.DELETED))
            {
                var completedLog = relatedLogs
                    .First(log => log.EventType == MediaAssetEventType.MIGRATION_COMPLETED);

                dbContext.MediaAssetLogs.Add(
                    MediaAssetLog.Create(
                        completedLog.MediaAssetId,
                        MediaAssetEventType.DELETED,
                        "Arquivo excluído do bucket temporário pela limpeza automática",
                        staleObject.Key,
                        _temporaryBucket,
                        DateTime.UtcNow));
                createdLogsCount++;
            }

            logger.LogInformation(
                "Arquivo {ObjectKey} excluído do bucket temporário {BucketName}",
                staleObject.Key,
                _temporaryBucket);
        }

        if (createdLogsCount > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        return deletedObjectsCount;
    }

    private static TimeSpan GetPositiveTimeSpan(
        IConfiguration configuration,
        string key,
        int defaultValueInMinutes)
    {
        var minutes = configuration.GetValue<int?>(key) ?? defaultValueInMinutes;

        if (minutes <= 0)
        {
            throw new InvalidOperationException($"A configuração '{key}' deve ser maior que zero.");
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private static int GetPositiveInt(
        IConfiguration configuration,
        string key,
        int defaultValue,
        int maxValue)
    {
        var value = configuration.GetValue<int?>(key) ?? defaultValue;

        if (value <= 0 || value > maxValue)
        {
            throw new InvalidOperationException(
                $"A configuração '{key}' deve estar entre 1 e {maxValue}.");
        }

        return value;
    }

    private sealed record CleanupCandidateLog(
        Guid MediaAssetId,
        string TempPath,
        MediaAssetEventType EventType);
}
