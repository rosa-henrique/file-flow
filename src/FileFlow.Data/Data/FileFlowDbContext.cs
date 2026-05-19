using FileFlow.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileFlow.Data.Data;

public class FileFlowDbContext : DbContext
{
    public FileFlowDbContext(DbContextOptions<FileFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<UploadBatch> UploadBatches { get; set; } = null!;
    public DbSet<MediaAsset> MediaAssets { get; set; } = null!;

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
             .HasColumnType("jsonb");

            b.Property(p => p.Metadata)
             .HasColumnName("metadata")
             .HasColumnType("jsonb");

            b.Property(p => p.RetryCount)
             .HasColumnName("retry_count")
             .HasDefaultValue(0);
        });
    }
}