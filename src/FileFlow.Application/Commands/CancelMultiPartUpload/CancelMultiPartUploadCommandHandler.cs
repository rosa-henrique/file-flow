using Amazon.S3;
using Amazon.S3.Model;

using MediatR;

using Microsoft.Extensions.Configuration;

namespace FileFlow.Application.Commands.CancelMultiPartUpload;

public class CancelMultiPartUploadCommandHandler(IAmazonS3 s3Client, IConfiguration configuration) : IRequestHandler<CancelMultiPartUploadCommand>
{
    private readonly string _bucketTemporary = configuration.GetValue<string>("S3:BucketTemporary")!;

    public async Task Handle(CancelMultiPartUploadCommand request, CancellationToken cancellationToken)
    {
        var abortMultipartUploadRequest = new AbortMultipartUploadRequest
        {
            BucketName = _bucketTemporary,
            Key = request.UploadId,
            UploadId = request.UploadId,
        };

        await s3Client.AbortMultipartUploadAsync(abortMultipartUploadRequest, cancellationToken);
    }
}