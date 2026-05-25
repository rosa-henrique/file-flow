using Microsoft.AspNetCore.Http;

namespace FileFlow.Shared.Exceptions;

public sealed class NotFoundException(string message)
    : AppException(message, "RESOURCE_NOT_FOUND")
{
    public override int StatusCode =>
        StatusCodes.Status404NotFound;

    public override string Title =>
        "Resource not found";

    public override string Type =>
        "https://httpstatuses.com/404";
}