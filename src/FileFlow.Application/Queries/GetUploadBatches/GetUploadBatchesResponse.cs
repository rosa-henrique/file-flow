using FileFlow.Data.Entities;

namespace FileFlow.Application.Queries.GetUploadBatches;

public record GetUploadBatchesResponse(Guid Id, string Name, UploadBatchStatus UploadBatchStatus, DateTime CreatedAt, DateTime? CompletedAt, int TotalFile);