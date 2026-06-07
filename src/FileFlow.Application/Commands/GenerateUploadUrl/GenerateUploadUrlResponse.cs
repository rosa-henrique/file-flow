namespace FileFlow.Application.Commands.GenerateUploadUrl;

public record GenerateUploadUrlResponse(TypeUpload Type, string ObjectKey);

public record GenerateUploadUrlSimpleResponse(string UploadUrl, string ObjectKey) : GenerateUploadUrlResponse(TypeUpload.SIMPLE, ObjectKey);

public record GenerateUploadUrlMultiPartResponse(string UploadId, string ObjectKey, long PartSize, IEnumerable<FileUrlResponse> FileUrls) : GenerateUploadUrlResponse(TypeUpload.MULITPART, ObjectKey);

public record FileUrlResponse(int PartNumber, long PartSize, string PreSignedUrl);

public enum TypeUpload
{
    SIMPLE,
    MULITPART,
}