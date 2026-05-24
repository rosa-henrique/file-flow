using System.Text.Json;

using FileFlow.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileFlow.Data.Context;

public class FileFlowDbContext : DbContext
{
    public FileFlowDbContext(DbContextOptions<FileFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<UploadBatch> UploadBatches { get; set; } = null!;
    public DbSet<MediaAsset> MediaAssets { get; set; } = null!;
    public DbSet<MediaAssetLog> MediaAssetLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UploadBatch>(b =>
        {
            b.Property(p => p.Status)
             .HasConversion<string>()
             .HasMaxLength(50)
             .HasColumnName("status")
             .HasColumnType("varchar(50)");

            b.HasMany(p => p.MediaAssets)
               .WithOne(m => m.UploadBatch)
               .HasForeignKey(m => m.UploadBatchId)
               .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaAsset>(b =>
        {
            b.Property(p => p.Status)
             .HasConversion<string>()
             .HasMaxLength(50)
             .HasColumnName("status")
             .HasColumnType("varchar(50)");

            b.Property(p => p.Tags)
             .HasColumnName("tags")
             .HasColumnType("jsonb")
             .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v),
                v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v) ?? new List<string>());

            b.Property(p => p.Metadata)
             .HasColumnName("metadata")
             .HasColumnType("jsonb");

            b.Property(p => p.RetryCount)
             .HasColumnName("retry_count")
             .HasDefaultValue(0);

            b.HasMany(m => m.Logs)
               .WithOne(l => l.MediaAsset)
               .HasForeignKey(l => l.MediaAssetId)
               .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaAssetLog>(b =>
        {
            b.Property(p => p.EventType)
             .HasConversion<string>()
             .HasMaxLength(100)
             .HasColumnName("event_type")
             .HasColumnType("varchar(100)");

            b.Property(p => p.Details)
             .HasColumnName("details")
             .HasColumnType("jsonb");
        });
    }
}