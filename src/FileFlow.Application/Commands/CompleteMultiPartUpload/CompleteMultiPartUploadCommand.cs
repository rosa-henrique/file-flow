using MediatR;

namespace FileFlow.Application.Commands.CompleteMultiPartUpload;

public record CompleteMultiPartUploadCommand(string UploadId, string ObjectKey, IList<CompleteUploadMultiETags> ETags) : IRequest;

public record CompleteUploadMultiETags(int PartNumber, string ETag);