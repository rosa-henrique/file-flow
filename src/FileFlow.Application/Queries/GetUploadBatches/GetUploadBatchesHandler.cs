using FileFlow.Data.Context;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace FileFlow.Application.Queries.GetUploadBatches;

public class GetUploadBatchesHandler(FileFlowDbContext dbContext) : IRequestHandler<GetUploadBatchesQuery, IEnumerable<GetUploadBatchesResponse>>
{
    public async Task<IEnumerable<GetUploadBatchesResponse>> Handle(GetUploadBatchesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.UploadBatches
            .Select(u => new GetUploadBatchesResponse(
                u.Id,
                u.Name,
                u.Status,
                u.CreatedAt,
                u.CompletedAt,
                u.MediaAssets.Count))
            .ToListAsync(cancellationToken);
    }
}