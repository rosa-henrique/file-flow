using MediatR;

namespace FileFlow.Application.Queries.GetUploadBatches;

public record GetUploadBatchesQuery : IRequest<IEnumerable<GetUploadBatchesResponse>>;