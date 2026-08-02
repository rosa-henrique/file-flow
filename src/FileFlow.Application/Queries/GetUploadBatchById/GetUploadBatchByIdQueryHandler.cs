using FileFlow.Data.Context;
using FileFlow.Shared.Exceptions;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace FileFlow.Application.Queries.GetUploadBatchById;

public class GetUploadBatchByIdQueryHandler(FileFlowDbContext dbContext) : IRequestHandler<GetUploadBatchByIdQuery, GetUploadBatchByIdResponse>
{
    public async Task<GetUploadBatchByIdResponse> Handle(GetUploadBatchByIdQuery request, CancellationToken cancellationToken)
    {
        var uploadBatch = await dbContext.UploadBatches
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (uploadBatch is null)
        {
            throw new NotFoundException(
                $"Não foi encontrado batch para id {request.Id}");
        }

        return new GetUploadBatchByIdResponse();
    }
}