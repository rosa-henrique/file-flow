using MediatR;

namespace FileFlow.Application.Commands.ReprocessUploadBatch;

public record ReprocessUploadBatchCommand(Guid UploadBatchId) : IRequest;