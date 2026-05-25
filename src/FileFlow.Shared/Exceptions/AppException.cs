namespace FileFlow.Shared.Exceptions;

public abstract class AppException(
    string message,
    string errorCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;

    public abstract int StatusCode { get; }

    public abstract string Title { get; }

    public abstract string Type { get; }
}