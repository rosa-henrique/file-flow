using MediatR;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public record CreateUploadBatchCommand(string Name) : IRequest<Guid>;