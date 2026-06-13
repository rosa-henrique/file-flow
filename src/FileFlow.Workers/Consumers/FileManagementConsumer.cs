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
    private readonly string _bucketTemporary = configuration.GetValue<string>("S3:BucketTemporary")!;
    private readonly string _bucketPermanent = configuration.GetValue<string>("S3:BucketPermanent")!;
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
            BucketName = _bucketTemporary,
            Key = @event.TempPath,
        };

        await s3Client.DeleteObjectAsync(deleteObjectRequest);

        var fileCleanedEvent = new FileCleanedEvent
        {
            MediaAssetId = @event.MediaAssetId,
            CleanedAt = DateTime.UtcNow,
            TempPath = @event.TempPath,
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
            SourceBucket = _bucketTemporary,
            SourceKey = @event.TempPath,
            DestinationBucket = _bucketPermanent,
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
            TempPath = @event.TempPath,
        };

        await capPublisher.PublishAsync("file.migration.completed", fileMigrationCompletedEvent);
    }

    private Task ProcessErrorMigrateFile(FileUploadedEvent @event, JsonDocument details, int? numberOfRetries = null)
    {
        if (numberOfRetries == null || numberOfRetries <= _maxRetry)
        {
            numberOfRetries ??= 1;
            var delayTime = TimeSpan.FromSeconds((double)(numberOfRetries * 30));
            var retryFileUploadedEvent = new RetryFileUploadedEvent
            {
                MediaAssetId = @event.MediaAssetId,
                UploadBatchId = @event.UploadBatchId,
                OriginalFileName = @event.OriginalFileName,
                MimeType = @event.MimeType,
                Size = @event.Size,
                TempPath = @event.TempPath,
                RetryCount = @event.RetryCount,
                Title = @event.Title,
                Tags = @event.Tags,
                Details = details,
                FailedAt = DateTime.UtcNow,
            };

            var headers = new Dictionary<string, string?>
            {
                { "x-retry-count", (++numberOfRetries).ToString() },
            };

            return capPublisher.PublishDelayAsync(delayTime, "file.uploaded.retry", retryFileUploadedEvent, headers);
        }

        var fileMigrationFailedEvent = new FileMigrationFailedEvent
        {
            MediaAssetId = @event.MediaAssetId,
            TempPath = @event.TempPath,
            Details = details,
            FailedAt = DateTime.UtcNow,
        };

        return capPublisher.PublishAsync("file.uploaded.failed", fileMigrationFailedEvent);
    }
}