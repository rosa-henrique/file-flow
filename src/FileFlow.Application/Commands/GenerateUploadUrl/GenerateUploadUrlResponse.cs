namespace FileFlow.Application.Commands.GenerateUploadUrl;

public record GenerateUploadUrlResponse(TypeUpload Type);

public record GenerateUploadUrlSimpleResponse(string Url) : GenerateUploadUrlResponse(TypeUpload.SIMPLE);

public record GenerateUploadUrlMultiPartResponse(string UploadId, long PartSize, IList<FileUrlResponse> FileUrls) : GenerateUploadUrlResponse(TypeUpload.MULITPART);

public record FileUrlResponse(int PartNumber, long PartSize, string PreSignedUrl);

public enum TypeUpload
{
    SIMPLE,
    MULITPART,
}