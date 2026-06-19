namespace FileFlow.Shared.Contracts;

public class FileMigrationCompletedEvent
{
    public Guid MediaAssetId { get; set; }
    public string FinalPath { get; set; } = null!;
    public required string FinalBucket { get; set; } = null!;
    public string TempPath { get; set; } = null!;
    public required string TempBucket { get; set; } = null!;
    public DateTime CompletedAt { get; set; }
}