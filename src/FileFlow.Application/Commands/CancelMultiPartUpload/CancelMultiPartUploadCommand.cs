using MediatR;

namespace FileFlow.Application.Commands.CancelMultiPartUpload;

public record CancelMultiPartUploadCommand(string UploadId, string ObjectKey) : IRequest;