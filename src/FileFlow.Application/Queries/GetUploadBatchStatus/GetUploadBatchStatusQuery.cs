using MediatR;

namespace FileFlow.Application.Queries.GetUploadBatchStatus;

public record GetUploadBatchStatusQuery(Guid Id) : IRequest<GetUploadBatchStatusResponse>;