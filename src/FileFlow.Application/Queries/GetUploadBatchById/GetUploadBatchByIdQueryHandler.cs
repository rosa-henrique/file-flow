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
            .Select(uploadBatch => new GetUploadBatchByIdResponse(
                uploadBatch.Id,
                uploadBatch.Name,
                uploadBatch.Status,
                uploadBatch.CreatedAt,
                uploadBatch.CompletedAt,
                uploadBatch.MediaAssets
                    .OrderBy(mediaAsset => mediaAsset.CreatedAt)
                    .Select(mediaAsset => new GetUploadBatchByIdMediaAssetResponse(
                        mediaAsset.Id,
                        mediaAsset.OriginalFileName,
                        mediaAsset.Title,
                        mediaAsset.MimeType,
                        mediaAsset.Size,
                        mediaAsset.FinalPath,
                        mediaAsset.FinalBucket,
                        mediaAsset.Status,
                        mediaAsset.CreatedAt,
                        mediaAsset.CompletedAt,
                        mediaAsset.ErrorMessage,
                        mediaAsset.Tags,
                        mediaAsset.Metadata))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        if (uploadBatch is null)
        {
            throw new NotFoundException(
                $"Não foi encontrado batch para id {request.Id}");
        }

        return uploadBatch;
    }
}