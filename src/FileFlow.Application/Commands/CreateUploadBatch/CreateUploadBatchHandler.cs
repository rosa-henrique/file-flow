using FileFlow.Data.Context;
using FileFlow.Data.Entities;

using MediatR;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public class CreateUploadBatchHandler(FileFlowDbContext dbContext) : IRequestHandler<CreateUploadBatchRequest, Guid>
{
    public async Task<Guid> Handle(CreateUploadBatchRequest request, CancellationToken cancellationToken)
    {
        var uploadBatch = UploadBatch.Create(request.Name);

        dbContext.UploadBatches.Add(uploadBatch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return uploadBatch.Id;
    }
}