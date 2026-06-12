namespace FileFlow.Shared.Contracts;

public class FileCleanedEvent
{
    public Guid MediaAssetId { get; set; }
    public string TempPath { get; set; } = null!;
    public DateTime CleanedAt { get; set; }
}