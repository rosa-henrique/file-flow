using FileFlow.Data.Context;
using FileFlow.Data.Entities;

using MediatR;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public class CreateUploadBatchCommandHandler(FileFlowDbContext dbContext) : IRequestHandler<CreateUploadBatchCommand, Guid>
{
    public async Task<Guid> Handle(CreateUploadBatchCommand command, CancellationToken cancellationToken)
    {
        var uploadBatch = UploadBatch.Create(command.Name);

        foreach (var fileInfo in command.FilesInfo)
        {
            uploadBatch.AddMediaAsset(
                fileInfo.OriginalFileName,
                fileInfo.MimeType,
                fileInfo.Size,
                fileInfo.Title,
                fileInfo.Tags,
                fileInfo.Metadata);
        }

        dbContext.UploadBatches.Add(uploadBatch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return uploadBatch.Id;
    }
}