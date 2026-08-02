using MediatR;

namespace FileFlow.Application.Queries.GetUploadBatchById;

public record GetUploadBatchByIdQuery(Guid Id) : IRequest<GetUploadBatchByIdResponse>;