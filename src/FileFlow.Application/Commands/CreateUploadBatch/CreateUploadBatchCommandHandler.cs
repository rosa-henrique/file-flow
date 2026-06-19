using DotNetCore.CAP;

using FileFlow.Data.Context;
using FileFlow.Data.Entities;
using FileFlow.Shared.Contracts;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public class CreateUploadBatchCommandHandler(FileFlowDbContext dbContext,
            ICapPublisher capPublisher,
            IConfiguration configuration,
            ILogger<CreateUploadBatchCommandHandler> logger) : IRequestHandler<CreateUploadBatchCommand, Guid>
{
    private readonly string _bucketTemporary = configuration.GetValue<string>("S3:BucketTemporary")!;
    private readonly string _bucketFinal = configuration.GetValue<string>("S3:BucketFinal")!;

    public async Task<Guid> Handle(CreateUploadBatchCommand command, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(
                    capPublisher,
                    autoCommit: false,
                    ct);

            try
            {
                var uploadBatch = UploadBatch.Create(command.Name);

                foreach (var fileInfo in command.FilesInfo)
                {
                    var mediaAsset = uploadBatch.AddMediaAsset(
                        fileInfo.OriginalFileName,
                        fileInfo.MimeType,
                        fileInfo.Size,
                        fileInfo.Title,
                        fileInfo.Tags,
                        fileInfo.Metadata);

                    await capPublisher.PublishAsync(
                        "file.uploaded",
                        new FileUploadedEvent
                        {
                            MediaAssetId = mediaAsset.Id,
                            UploadBatchId = uploadBatch.Id,
                            OriginalFileName = mediaAsset.OriginalFileName,
                            MimeType = mediaAsset.MimeType,
                            Size = mediaAsset.Size,
                            TempPath = fileInfo.ObjectKey,
                            Title = mediaAsset.Title,
                            Tags = mediaAsset.Tags,
                            TempBucket = _bucketTemporary,
                            FinalBucket = _bucketFinal,
                        },
                        cancellationToken: ct);
                }

                dbContext.UploadBatches.Add(uploadBatch);

                await dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);

                return uploadBatch.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                logger.LogError(ex, "Erro ao publicar mensagens para processamento de arquivos");
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}