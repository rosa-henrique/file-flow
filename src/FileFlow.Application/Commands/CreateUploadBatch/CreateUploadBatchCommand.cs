using System.Text.Json;

using MediatR;

namespace FileFlow.Application.Commands.CreateUploadBatch;

public record CreateUploadBatchCommand(string Name, IEnumerable<CreateUploadBatchFileInfo> FilesInfo) : IRequest<Guid>;

public record CreateUploadBatchFileInfo(string ObjectKey, string OriginalFileName, string MimeType, long Size, string Title, List<string>? Tags, JsonDocument? Metadata);