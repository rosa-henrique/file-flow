using Amazon.S3;
using Amazon.S3.Model;

using MediatR;

using Microsoft.Extensions.Configuration;

namespace FileFlow.Application.Commands.CompleteUploadMultiPart;

public class CompleteUploadMultiPartCommandHandler(IAmazonS3 s3Client, IConfiguration configuration) : IRequestHandler<CompleteUploadMultiPartCommand>
{
    private readonly string _bucketTemporary = configuration.GetValue<string>("S3:BucketTemporary")!;

    public async Task Handle(CompleteUploadMultiPartCommand request, CancellationToken cancellationToken)
    {
        var partETags = request.ETags
            .OrderBy(e => e.PartNumber)
            .Select(p => new PartETag(p.PartNumber, p.ETag))
            .ToList();

        var completeMultipartUploadRequest = new CompleteMultipartUploadRequest
        {
            BucketName = _bucketTemporary,
            Key = request.ObjectKey,
            UploadId = request.UploadId,
            PartETags = partETags,
        };

        await s3Client.CompleteMultipartUploadAsync(completeMultipartUploadRequest, cancellationToken);
    }
}