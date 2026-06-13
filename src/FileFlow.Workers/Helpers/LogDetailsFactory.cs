using System.Text.Json;

using Amazon.Runtime;

namespace FileFlow.Workers.Helpers;

public static class LogDetailsFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public static JsonDocument CreateHttpError(
        HttpRequestMessage request,
        HttpResponseMessage response,
        string? requestBody = null,
        string? responseBody = null)
    {
        var details = new
        {
            type = "http",
            request = new
            {
                method = request.Method.Method,
                url = request.RequestUri?.ToString(),
                headers = request.Headers,
                body = requestBody,
            },
            response = new
            {
                statusCode = (int)response.StatusCode,
                reasonPhrase = response.ReasonPhrase,
                headers = response.Headers,
                body = responseBody,
            },
        };

        return JsonDocument.Parse(
            JsonSerializer.Serialize(details, JsonOptions));
    }

    public static JsonDocument CreateAwsError(
        AmazonWebServiceRequest request,
        AmazonWebServiceResponse response,
        string? responseBody = null)
    {
        var details = new
        {
            type = "http",
            request,
            response = new
            {
                statusCode = (int)response.HttpStatusCode,
                body = responseBody,
                Metadata = response.ResponseMetadata,
            },
        };

        return JsonDocument.Parse(
            JsonSerializer.Serialize(details, JsonOptions));
    }

    public static JsonDocument CreateException(
        Exception exception,
        object? context = null)
    {
        var details = new
        {
            type = "exception",
            request = context,
            response = BuildException(exception),
        };

        return JsonDocument.Parse(
            JsonSerializer.Serialize(details, JsonOptions));
    }

    private static object BuildException(Exception exception)
    {
        return new
        {
            exceptionType = exception.GetType().FullName,
            exception.Message,
            exception.StackTrace,
            innerException = exception.InnerException is null
                ? null
                : BuildException(exception.InnerException),
        };
    }
}