using System.Net;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using DotNetCore.CAP;

using FileFlow.Shared.Contracts;
using FileFlow.Workers.Helpers;

namespace FileFlow.Workers.Consumers;

public class FileManagementConsumer(IAmazonS3 s3Client,
    IConfiguration configuration,
    ICapPublisher capPublisher,
    ILogger<FileManagementConsumer> logger) : ICapSubscribe
{
    private readonly int _maxRetry = configuration.GetValue<int>("FileManagement:MaxRetry");

    [CapSubscribe("file.uploaded", Group = "fileflow.workers.management")]
    public async Task OnFileUploaded(FileUploadedEvent @event)
    {
        logger.LogInformation("Iniciando migração de arquivo {@Event}", @event);

        await MigrateFile(@event);
    }

    [CapSubscribe("file.migration.completed", Group = "fileflow.workers.management")]
    public async Task OnFileMigrated(FileMigrationCompletedEvent @event)
    {
        var deleteObjectRequest = new DeleteObjectRequest
        {
            BucketName = @event.TempBucket,
            Key = @event.TempPath,
        };

        await s3Client.DeleteObjectAsync(deleteObjectRequest);

        var fileCleanedEvent = new FileCleanedEvent
        {
            MediaAssetId = @event.MediaAssetId,
            CleanedAt = DateTime.UtcNow,
            TempPath = @event.TempPath,
            TempBucket = @event.TempBucket,
        };

        await capPublisher.PublishAsync("file.migration.cleaned", fileCleanedEvent);
    }

    [CapSubscribe("file.uploaded.retry", Group = "fileflow.workers.management")]
    public async Task OnRetryFileUploaded(RetryFileUploadedEvent @event, [FromCap] CapHeader header)
    {
        var retryFileUploadedEventCount = header.TryGetValue("x-retry-count", out var xRetryCount) && !string.IsNullOrWhiteSpace(xRetryCount)
                                                ? int.Parse(xRetryCount)
                                                : 0;
        await MigrateFile(@event, retryFileUploadedEventCount);
    }

    private async Task MigrateFile(FileUploadedEvent @event, int? numberOfRetries = null)
    {
        var folder = @event.UploadBatchId.ToString();
        var destinationKey = $"{folder}/{@event.TempPath}";
        var copyObjectRequest = new CopyObjectRequest
        {
            SourceBucket = @event.TempBucket,
            SourceKey = @event.TempPath,
            DestinationBucket = @event.FinalBucket,
            DestinationKey = destinationKey,
        };

        try
        {
            var response = await s3Client.CopyObjectAsync(copyObjectRequest);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Erro ao migrar arquivo {@Event} e iniciando retry", @event);
                await ProcessErrorMigrateFile(@event, LogDetailsFactory.CreateAwsError(copyObjectRequest, response), numberOfRetries);
                return;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception ao migrar arquivo {@Event} e iniciando retry", @event);
            await ProcessErrorMigrateFile(@event, LogDetailsFactory.CreateException(e, copyObjectRequest), numberOfRetries);
            return;
        }

        var fileMigrationCompletedEvent = new FileMigrationCompletedEvent
        {
            MediaAssetId = @event.MediaAssetId,
            CompletedAt = DateTime.UtcNow,
            FinalPath = destinationKey,
            FinalBucket = @event.FinalBucket,
            TempPath = @event.TempPath,
            TempBucket = @event.TempBucket,
        };

        await capPublisher.PublishAsync("file.migration.completed", fileMigrationCompletedEvent);
    }

    private Task ProcessErrorMigrateFile(FileUploadedEvent @event, JsonDocument details, int? numberOfRetries = null)
    {
        if (numberOfRetries == null || numberOfRetries <= _maxRetry)
        {
            return SendToRetry(@event, details, numberOfRetries);
        }

        var fileMigrationFailedEvent = new FileMigrationFailedEvent
        {
            MediaAssetId = @event.MediaAssetId,
            TempBucket = @event.TempBucket,
            TempPath = @event.TempPath,
            Details = details,
            FailedAt = DateTime.UtcNow,
        };

        return capPublisher.PublishAsync("file.uploaded.failed", fileMigrationFailedEvent);
    }

    private Task SendToRetry(FileUploadedEvent @event, JsonDocument details, int? numberOfRetries)
    {
        numberOfRetries ??= 0;
        var delayTime = TimeSpan.FromSeconds((long)((numberOfRetries + 1) * 30));
        var retryFileUploadedEvent = new RetryFileUploadedEvent
        {
            MediaAssetId = @event.MediaAssetId,
            UploadBatchId = @event.UploadBatchId,
            OriginalFileName = @event.OriginalFileName,
            MimeType = @event.MimeType,
            Size = @event.Size,
            TempPath = @event.TempPath,
            TempBucket = @event.TempBucket,
            FinalBucket = @event.FinalBucket,
            RetryCount = @event.RetryCount,
            Title = @event.Title,
            Tags = @event.Tags,
            Details = details,
            FailedAt = DateTime.UtcNow,
        };

        var headers = new Dictionary<string, string?>
        {
            { "x-retry-count", (numberOfRetries + 1).ToString() },
        };

        return capPublisher.PublishDelayAsync(delayTime, "file.uploaded.retry", retryFileUploadedEvent, headers);
    }
}