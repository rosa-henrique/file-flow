using MediatR;

namespace FileFlow.Application.Commands.CompleteUploadMultiPart;

public record CompleteUploadMultiPartCommand(string UploadId, string ObjectKey, IList<CompleteUploadMultiETags> ETags) : IRequest;

public record CompleteUploadMultiETags(int PartNumber, string ETag);