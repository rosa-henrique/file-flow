using Microsoft.AspNetCore.Http;

namespace FileFlow.Shared.Exceptions;

public sealed class AppValidationException(
    IDictionary<string, string[]> errors)
    : AppException(
        "One or more validation errors occurred.",
        "VALIDATION_ERROR")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public override int StatusCode =>
        StatusCodes.Status422UnprocessableEntity;

    public override string Title =>
        "Validation failed";

    public override string Type =>
        "https://httpstatuses.com/422";
}