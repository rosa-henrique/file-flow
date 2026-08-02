using FileFlow.Data.Entities;

namespace FileFlow.Application.Queries.GetUploadBatchStatus;

public record GetUploadBatchStatusResponse(Guid Id, UploadBatchStatus Status, bool TimedOut = false);