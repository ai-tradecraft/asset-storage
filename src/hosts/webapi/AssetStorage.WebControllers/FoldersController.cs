using AssetStorage.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AssetStorage.WebControllers;

/// <summary>Creates and retrieves immutable folder manifests.</summary>
[ApiController]
[Route("{team}/_api/v1/folders")]
public sealed class FoldersController(IAssetStorageService service) : ControllerBase
{
    /// <summary>Creates a folder and its first complete manifest.</summary>
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
