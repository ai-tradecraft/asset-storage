using AssetStorage.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AssetStorage.WebControllers;

/// <summary>Serves logical documents and immutable folder entries.</summary>
[ApiController]
[Route("")]
public sealed class ContentController(IAssetStorageService service) : ControllerBase
{
    /// <summary>Serves an entry from a folder addressed by stable ID.</summary>
    /// <param name="team">The team name from the route.</param>
    /// <param name="folderId">The folder identifier from the route.</param>
    /// <param name="entryPath">The folder-relative entry path from the route.</param>
    /// <param name="version">The optional version selector.</param>
    /// <param name="raw">Forces raw output instead of HTML rendering.</param>
    /// <param name="download">Indicates whether to set Content-Disposition for download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Either rendered HTML or raw file stream, or 404 if not found.</returns>
    [AcceptVerbs("GET", "HEAD")]
    [Route("{team}/_folders/{folderId:guid}/{**entryPath}")]
    public async Task<IActionResult> GetFolderEntryAsync(
        string team,
        Guid folderId,
        string entryPath,
        [FromQuery] string? version,
        [FromQuery] bool raw,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.GetFolderEntryAsync(
            team, folderId, entryPath, ApiParsing.ParseSelector(version), cancellationToken).ConfigureAwait(false);
        return snapshot is null
            ? NotFound()
            : await ContentResults.NegotiatedAsync(
                service, snapshot, Request, raw, download, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serves a document or mounted folder entry by logical path.</summary>
    /// <param name="team">The team name from the route.</param>
    /// <param name="logicalPath">The logical path to resolve from the route.</param>
    /// <param name="version">The optional version selector.</param>
    /// <param name="raw">Forces raw output instead of HTML rendering.</param>
    /// <param name="download">Indicates whether to set Content-Disposition for download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Either rendered HTML or raw file stream, or 404 if not found.</returns>
    [AcceptVerbs("GET", "HEAD")]
    [Route("{team}/{**logicalPath}", Order = 1000)]
    public async Task<IActionResult> GetLogicalContentAsync(
        string team,
        string logicalPath,
        [FromQuery] string? version,
        [FromQuery] bool raw,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.ResolvePathAsync(
            team, logicalPath, ApiParsing.ParseSelector(version), cancellationToken).ConfigureAwait(false);
        return snapshot is null
            ? NotFound()
            : await ContentResults.NegotiatedAsync(
                service, snapshot, Request, raw, download, cancellationToken).ConfigureAwait(false);
    }
}
