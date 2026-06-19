namespace FileFlow.Shared.Contracts;

public class FileUploadedEvent
{
    public Guid MediaAssetId { get; set; }
    public Guid UploadBatchId { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public long Size { get; set; }
    public required string TempBucket { get; set; } = null!;
    public string TempPath { get; set; } = null!;
    public required string FinalBucket { get; set; } = null!;
    public int RetryCount { get; set; } = 0;
    public string? Title { get; set; }
    public List<string>? Tags { get; set; }
}