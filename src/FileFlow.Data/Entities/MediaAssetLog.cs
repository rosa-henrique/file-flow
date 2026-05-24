using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace FileFlow.Data.Entities;

[Table("media_asset_logs")]
public class MediaAssetLog
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    [Column("media_asset_id")]
    [Required]
    public Guid MediaAssetId { get; private set; }

    [ForeignKey(nameof(MediaAssetId))]
    public MediaAsset? MediaAsset { get; private set; }

    [Column("timestamp")]
    [Required]
    public DateTime Timestamp { get; private set; }

    [Column("event_type", TypeName = "varchar(100)")]
    [Required]
    public MediaAssetEventType EventType { get; private set; }

    [Column("message", TypeName = "text")]
    [Required]
    public string Message { get; private set; } = null!;

    [Column("temp_path")]
    [Required]
    [MaxLength(500)]
    public string TempPath { get; private set; } = null!;

    [Column("final_path")]
    [MaxLength(500)]
    public string? FinalPath { get; private set; }

    [Column("details", TypeName = "jsonb")]
    public JsonDocument? Details { get; private set; }

    private MediaAssetLog() { }

    public static MediaAssetLog Create(Guid mediaAssetId, MediaAssetEventType eventType, string message, string tempPath, JsonDocument? details = null)
    {
        return new MediaAssetLog
        {
            MediaAssetId = mediaAssetId,
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            TempPath = tempPath,
            Message = message,
            FinalPath = null,
            Details = details,
        };
    }
}

public enum MediaAssetEventType
{
    PRE_REGISTERED,
    UPLOAD_CONFIRMED,
    MIGRATION_STARTED,
    MIGRATION_COMPLETED,
    MIGRATION_FAILED,
    RETRY_INITIATED,
    DELETION_REQUESTED,
    DELETED,
}
