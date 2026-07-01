using System.Net;
using System.Text;
using System.Text.Json;
using AssetStorage.Abstractions;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AssetStorage.WebControllers;

/// <summary>Creates IActionResult instances for streaming and rendering stored content.</summary>
internal static class ContentResults
{
    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly HashSet<string> RenderableImages = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/gif",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    /// <summary>Returns raw document bytes as a file stream result.</summary>
    /// <param name="service">The asset storage service.</param>
    /// <param name="snapshot">The document snapshot to stream.</param>
    /// <param name="download">Indicates whether to set Content-Disposition for download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A file stream result for the document content.</returns>
    internal static async Task<IActionResult> RawAsync(
        IAssetStorageService service,
        DocumentSnapshot snapshot,
        bool download,
        CancellationToken cancellationToken)
    {
        var stream = await service.OpenContentAsync(snapshot.Version, cancellationToken).ConfigureAwait(false);
        return new FileStreamResult(stream, snapshot.Version.ContentType)
        {
            EnableRangeProcessing = true,
            EntityTag = new EntityTagHeaderValue($"\"{snapshot.Version.Content.Sha256}\""),
            FileDownloadName = download
                ? Path.GetFileName(snapshot.Document.LogicalPath ?? $"{snapshot.Document.Id:N}")
                : null,
            LastModified = snapshot.Version.CreatedAt
        };
    }

    /// <summary>Returns content using HTTP content negotiation for HTML rendering when applicable.</summary>
    /// <param name="service">The asset storage service.</param>
    /// <param name="snapshot">The document snapshot to stream or render.</param>
    /// <param name="request">The HTTP request for Accept header inspection.</param>
    /// <param name="raw">Forces raw output instead of rendering.</param>
    /// <param name="download">Indicates whether to set Content-Disposition for download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Either a rendered HTML result or raw file stream.</returns>
    internal static async Task<IActionResult> NegotiatedAsync(
        IAssetStorageService service,
        DocumentSnapshot snapshot,
        HttpRequest request,
        bool raw,
        bool download,
        CancellationToken cancellationToken)
    {
        if (raw || download || !AcceptsHtml(request))
        {
            return await RawAsync(service, snapshot, download, cancellationToken).ConfigureAwait(false);
        }

        var contentType = snapshot.Version.ContentType.Split(';', 2)[0].Trim();
        if (RenderableImages.Contains(contentType))
        {
            var source = WebUtility.HtmlEncode($"{request.Path}{request.QueryString.Add("raw", "true")}");
            return new ContentResult
            {
                ContentType = "text/html; charset=utf-8",
                Content = Page($"<img src=\"{source}\" alt=\"Stored image\">")
            };
        }

        await using var stream = await service.OpenContentAsync(
            snapshot.Version, cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(
            stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var body = contentType switch
        {
            "text/markdown" or "text/x-markdown" =>
                new HtmlSanitizer().Sanitize(Markdown.ToHtml(text, MarkdownPipeline)),
            "application/json" => $"<pre>{WebUtility.HtmlEncode(PrettyJson(text))}</pre>",
            _ when contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) =>
                $"<pre>{WebUtility.HtmlEncode(text)}</pre>",
            _ => "<p>This content type is available only as a raw download.</p>"
        };
        return new ContentResult { ContentType = "text/html; charset=utf-8", Content = Page(body) };
    }

    private static bool AcceptsHtml(HttpRequest request) =>
        request.GetTypedHeaders().Accept?.Any(media =>
            media.MediaType.HasValue
            && string.Equals(media.MediaType.Value, "text/html", StringComparison.OrdinalIgnoreCase)) == true;

    private static string PrettyJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, IndentedJson);
    }

    private static string Page(string body) =>
        $"""
         <!doctype html>
         <html lang="en">
         <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Asset Storage</title></head>
         <body>{body}</body>
         </html>
         """;
}
