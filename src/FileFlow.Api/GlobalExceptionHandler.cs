using FileFlow.Shared.Exceptions;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FileFlow.Api;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception: {Message}",
            exception.Message);

        var problemDetails = CreateProblemDetails(
            httpContext,
            exception);

        httpContext.Response.StatusCode =
            problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }

    private static ApiProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception)
    {
        if (exception is AppException appException)
        {
            return CreateAppProblemDetails(
                httpContext,
                appException);
        }

        return CreateUnexpectedProblemDetails(httpContext);
    }

    private static ApiProblemDetails CreateAppProblemDetails(
        HttpContext httpContext,
        AppException exception)
    {
        var problemDetails = new ApiProblemDetails
        {
            Title = exception.Title,
            Detail = exception.Message,
            Status = exception.StatusCode,
            Type = exception.Type,
            Instance = httpContext.Request.Path,
            ErrorCode = exception.ErrorCode,
            TraceId = httpContext.TraceIdentifier,
        };

        if (exception is AppValidationException validationException)
        {
            problemDetails.Errors =
                validationException.Errors;
        }

        return problemDetails;
    }

    private static ApiProblemDetails CreateUnexpectedProblemDetails(
        HttpContext httpContext)
    {
        return new ApiProblemDetails
        {
            Title = "Unexpected error",
            Detail = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://httpstatuses.com/500",
            Instance = httpContext.Request.Path,
            ErrorCode = "INTERNAL_SERVER_ERROR",
            TraceId = httpContext.TraceIdentifier,
        };
    }
}

public sealed class ApiProblemDetails : ProblemDetails
{
    public string? ErrorCode { get; set; }

    public string? TraceId { get; set; }

    public IDictionary<string, string[]>? Errors { get; set; }
}