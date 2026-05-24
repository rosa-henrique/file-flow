using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileFlow.Data.Entities;

[Table("upload_batches")]
public class UploadBatch : Entity
{
    [Column("user_id")]
    [Required]
    public Guid UserId { get; private set; }

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
    public List<MediaAsset> MediaAssets { get; private set; } = [];

    public static UploadBatch Create(Guid userId, string name)
    {
        return new UploadBatch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Status = UploadBatchStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
        };
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