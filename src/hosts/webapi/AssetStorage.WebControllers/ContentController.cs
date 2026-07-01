using AssetStorage.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AssetStorage.WebControllers;

/// <summary>Serves logical documents and immutable folder entries.</summary>
[ApiController]
[Route("")]
public sealed class ContentController(IAssetStorageService service) : ControllerBase
{
    /// <summary>Serves an entry from a folder addressed by stable ID.</summary>
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
