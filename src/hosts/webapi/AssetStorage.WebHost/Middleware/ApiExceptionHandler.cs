using AssetStorage.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AssetStorage.WebHost.Middleware;

/// <summary>Handles asset-storage domain exceptions and maps them to HTTP problem details.</summary>
internal sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Attempts to handle an asset-storage exception by returning an HTTP problem details response.</summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the exception was handled, false to continue the exception pipeline.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            AssetValidationException => (
                StatusCodes.Status400BadRequest, "Invalid asset request", exception.Message),
            JsonException => (
                StatusCodes.Status400BadRequest, "Invalid JSON", "The request contains invalid JSON."),
            AssetLimitExceededException => (
                StatusCodes.Status413PayloadTooLarge, "Storage limit exceeded", exception.Message),
            AssetConflictException => (
                StatusCodes.Status409Conflict, "Asset conflict", exception.Message),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound, "Asset not found", exception.Message),
            ObjectStorageException or MetadataStorageException => (
                StatusCodes.Status503ServiceUnavailable,
                "Storage unavailable",
                "The storage backend could not complete the request."),
            _ => (0, string.Empty, string.Empty)
        };
        if (status == 0)
        {
            return false;
        }

        LogHandledException(logger, exception, status);
        httpContext.Response.StatusCode = status;
        await Results.Problem(
            detail: detail,
            instance: httpContext.Request.Path,
            statusCode: status,
            title: title).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Handled asset-storage exception with HTTP status {StatusCode}")]
    private static partial void LogHandledException(ILogger logger, Exception exception, int statusCode);
}
