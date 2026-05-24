using MediatR;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public record CreateUploadBatchRequest(string Name) : IRequest<Guid>;