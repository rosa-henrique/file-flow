using System.Text.Json;

using FileFlow.Data.Entities;

namespace FileFlow.Application.Queries.GetUploadBatchById;

public record GetUploadBatchByIdResponse(
    Guid Id,
    string Name,
    UploadBatchStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IList<GetUploadBatchByIdMediaAssetResponse> MediaAssets);

public record GetUploadBatchByIdMediaAssetResponse(
    Guid Id,
    string OriginalFileName,
    string? Title,
    string MimeType,
    long Size,
    string? FinalPath,
    string? FinalBucket,
    MediaAssetStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    List<string>? Tags,
    JsonDocument? Metadata);