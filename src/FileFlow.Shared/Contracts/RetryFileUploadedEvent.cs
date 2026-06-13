using System.Text.Json;

namespace FileFlow.Shared.Contracts;

public class RetryFileUploadedEvent : FileUploadedEvent
{
    public JsonDocument Details { get; set; } = null!;
    public DateTime FailedAt { get; set; }
}