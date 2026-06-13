using System.Text.Json;

namespace FileFlow.Shared.Contracts;

public class FileMigrationFailedEvent
{
    public Guid MediaAssetId { get; set; }
    public JsonDocument Details { get; set; } = null!;
    public string TempPath { get; set; } = null!;
    public DateTime FailedAt { get; set; }
}