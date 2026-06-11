using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace FileFlow.Data.Entities;

[Table("upload_batches")]
public class UploadBatch : Entity
{
    [Column("name")]
    [Required]
    [MaxLength(255)]
    public string Name { get; private set; } = null!;

    [Column("status", TypeName = "varchar(50)")]
    [Required]
    public UploadBatchStatus Status { get; private set; }

    [Column("created_at")]
    [Required]
    public DateTime CreatedAt { get; private set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; private set; }

    [InverseProperty(nameof(MediaAsset.UploadBatch))]
    private readonly List<MediaAsset> _mediaAssets = [];

    public IReadOnlyCollection<MediaAsset> MediaAssets => _mediaAssets;

    public static UploadBatch Create(string name)
    {
        return new UploadBatch
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = UploadBatchStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
        };
    }

    public MediaAsset AddMediaAsset(
        string originalFileName,
        string mimeType,
        long size,
        string title,
        List<string>? tags,
        JsonDocument? metadata)
    {
        var mediaAsset = MediaAsset.Create(
            Id,
            originalFileName,
            mimeType,
            size,
            title,
            tags,
            metadata);

        _mediaAssets.Add(mediaAsset);

        return mediaAsset;
    }

    public void MarkCompleted(DateTime completedAt, UploadBatchStatus status = UploadBatchStatus.COMPLETED)
    {
        CompletedAt = completedAt;
        Status = status;
    }

    protected UploadBatch() { }
}

public enum UploadBatchStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    PARTIAL,
    FAILED,
}