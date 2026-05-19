using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace FileFlow.Data.Entities;

[Table("media_assets")]
public class MediaAsset : Entity
{
    [Column("upload_batch_id")]
    [Required]
    public Guid UploadBatchId { get; private set; }

    [ForeignKey(nameof(UploadBatchId))]
    public UploadBatch? UploadBatch { get; private set; }

    [Column("user_id")]
    [Required]
    public Guid UserId { get; private set; }

    [Column("original_file_name")]
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; private set; } = null!;

    [Column("title")]
    [MaxLength(255)]
    public string? Title { get; private set; }

    [Column("mime_type")]
    [Required]
    [MaxLength(100)]
    public string MimeType { get; private set; } = null!;

    [Column("size")]
    [Required]
    public long Size { get; private set; }

    [Column("temp_minio_path")]
    [Required]
    [MaxLength(500)]
    public string TempMinIOPath { get; private set; } = null!;

    [Column("final_minio_path")]
    [MaxLength(500)]
    public string? FinalMinIOPath { get; private set; }

    [Column("status", TypeName = "varchar(50)")]
    [Required]
    public MediaAssetStatus Status { get; private set; }

    [Column("retry_count")]
    [Required]
    public int RetryCount { get; private set; }

    [Column("created_at")]
    [Required]
    public DateTime CreatedAt { get; private set; }

    [Column("last_attempt_at")]
    public DateTime? LastAttemptAt { get; private set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; private set; }

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; private set; }

    [Column("tags", TypeName = "jsonb")]
    public JsonDocument? Tags { get; private set; }

    [Column("metadata", TypeName = "jsonb")]
    public JsonDocument? Metadata { get; private set; }

    private MediaAsset() { }

    public static MediaAsset Create(
        Guid uploadBatchId,
        Guid userId,
        string originalFileName,
        string mimeType,
        long size,
        string tempMinIOPath,
        string? title = null,
        MediaAssetStatus status = MediaAssetStatus.PENDING,
        JsonDocument? tags = null,
        JsonDocument? metadata = null)
    {
        return new MediaAsset
        {
            Id = Guid.NewGuid(),
            UploadBatchId = uploadBatchId,
            UserId = userId,
            OriginalFileName = originalFileName,
            Title = title,
            MimeType = mimeType,
            Size = size,
            TempMinIOPath = tempMinIOPath,
            FinalMinIOPath = null,
            Status = status,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            LastAttemptAt = null,
            CompletedAt = null,
            ErrorMessage = null,
            Tags = tags,
            Metadata = metadata,
        };
    }

    public void IncrementRetry()
    {
        RetryCount++;
        LastAttemptAt = DateTime.UtcNow;
    }

    public void SetError(string? error)
    {
        ErrorMessage = error;
        Status = MediaAssetStatus.FAILED;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkMigrated(string finalMinioPath)
    {
        FinalMinIOPath = finalMinioPath;
        Status = MediaAssetStatus.MIGRATED;
        CompletedAt = DateTime.UtcNow;
    }
}

public enum MediaAssetStatus
{
    PENDING,
    MIGRATING,
    MIGRATED,
    FAILED,
    DELETION_PENDING,
    DELETED,
}