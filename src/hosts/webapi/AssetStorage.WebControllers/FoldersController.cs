using AssetStorage.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AssetStorage.WebControllers;

/// <summary>Creates and retrieves immutable folder manifests.</summary>
[ApiController]
[Route("{team}/_api/v1/folders")]
public sealed class FoldersController(IAssetStorageService service) : ControllerBase
{
    /// <summary>Creates a folder and its first complete manifest.</summary>
    /// <param name="team">The team name from the route.</param>
    /// <param name="request">The creation request with entries and idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created folder response with 201 status, or 200 if idempotent.</returns>
    [HttpPost]
    [ProducesResponseType<FolderResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FolderResponse>> CreateAsync(
        string team,
        CreateFolderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateFolderCommand(
            team,
            request.LogicalPath,
            request.Entries.Select(entry => new CreateFolderEntry(entry.Path, entry.DocumentVersionId)).ToArray(),
            request.IdempotencyKey);
        var result = await service.CreateFolderAsync(command, cancellationToken).ConfigureAwait(false);
        var response = ResponseMapping.ToResponse(result.Value, result.Created);
        return result.Created
            ? CreatedAtAction(nameof(GetAsync), new { team, folderId = response.FolderId }, response)
            : Ok(response);
    }

    /// <summary>Appends a complete immutable folder manifest.</summary>
    /// <param name="team">The team name from the route.</param>
    /// <param name="folderId">The folder identifier from the route.</param>
    /// <param name="request">The append request with expected version, bump, entries, and idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The appended folder response with 201 status, or 200 if unchanged or idempotent.</returns>
    [HttpPost("{folderId:guid}/versions")]
    [ProducesResponseType<FolderResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FolderResponse>> AppendVersionAsync(
        string team,
        Guid folderId,
        AppendFolderVersionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AppendFolderVersionCommand(
            team,
            folderId,
            ApiParsing.ParseExactVersion(request.ExpectedVersion),
            ApiParsing.ParseBump(request.Bump),
            request.Entries.Select(entry => new CreateFolderEntry(entry.Path, entry.DocumentVersionId)).ToArray(),
            request.IdempotencyKey);
        var result = await service.AppendFolderVersionAsync(command, cancellationToken).ConfigureAwait(false);
        var response = ResponseMapping.ToResponse(result.Value, result.Created);
        return result.Created
            ? CreatedAtAction(
                nameof(GetAsync),
                new { team, folderId, version = response.Version },
                response)
            : Ok(response);
    }

    /// <summary>Gets a selected immutable folder manifest.</summary>
    /// <param name="team">The team name from the route.</param>
    /// <param name="folderId">The folder identifier from the route.</param>
    /// <param name="version">The optional version selector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder response with manifest, or 404 if not found.</returns>
    [HttpGet("{folderId:guid}")]
    [ProducesResponseType<FolderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FolderResponse>> GetAsync(
        string team,
        Guid folderId,
        [FromQuery] string? version,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.GetFolderAsync(
            team, folderId, ApiParsing.ParseSelector(version), cancellationToken).ConfigureAwait(false);
        return snapshot is null ? NotFound() : Ok(ResponseMapping.ToResponse(snapshot, created: false));
    }

    /// <summary>Streams one exact entry from a selected folder manifest.</summary>
    /// <param name="team">The team name from the route.</param>
    /// <param name="folderId">The folder identifier from the route.</param>
    /// <param name="entryPath">The folder-relative entry path from the route.</param>
    /// <param name="version">The optional version selector.</param>
    /// <param name="download">Indicates whether to set Content-Disposition for download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A file stream result with the entry content, or 404 if not found.</returns>
    [HttpGet("{folderId:guid}/entries/{**entryPath}")]
    public async Task<IActionResult> GetEntryAsync(
        string team,
        Guid folderId,
        string entryPath,
        [FromQuery] string? version,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.GetFolderEntryAsync(
            team, folderId, entryPath, ApiParsing.ParseSelector(version), cancellationToken).ConfigureAwait(false);
        return snapshot is null
            ? NotFound()
            : await ContentResults.RawAsync(service, snapshot, download, cancellationToken).ConfigureAwait(false);
    }
}
