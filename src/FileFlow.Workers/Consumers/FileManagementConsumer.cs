using System.Net;

using Amazon.S3;
using Amazon.S3.Model;

using DotNetCore.CAP;

using FileFlow.Shared.Contracts;

namespace FileFlow.Workers.Consumers;

public class FileManagementConsumer(IAmazonS3 s3Client,
    IConfiguration configuration,
    ICapPublisher capPublisher,
    ILogger<FileManagementConsumer> logger) : ICapSubscribe
{
    private readonly string _bucketTemporary = configuration.GetValue<string>("S3:BucketTemporary")!;
    private readonly string _bucketPermanent = configuration.GetValue<string>("S3:BucketPermanent")!;

    [CapSubscribe("file.uploaded", Group = "fileflow.workers.management")]
    public async Task OnFileUploaded(FileUploadedEvent @event, [FromCap] CapHeader header)
    {
        logger.LogInformation("Iniciando migração de arquivo {@Event}", @event);

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
                await InitiateRetry(@event);
                return;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception ao migrar arquivo {@Event} e iniciando retry", @event);
            await InitiateRetry(@event);
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

    private Task InitiateRetry(FileUploadedEvent @event)
    {
        return capPublisher.PublishAsync("file.uploaded.retry", @event);
    }
}