using System.Text.Json;
using AssetStorage.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AssetStorage.WebControllers;

/// <summary>Creates and retrieves immutable document versions.</summary>
[ApiController]
[Route("{team}/_api/v1/documents")]
public sealed class DocumentsController(IAssetStorageService service) : ControllerBase
{
    /// <summary>Creates a document from the raw request body.</summary>
    [HttpPost]
    [ProducesResponseType<DocumentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DocumentResponse>> CreateAsync(
        string team,
        [FromQuery] string? logicalPath,
        [FromQuery] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var metadata = ParseMetadata(Request.Headers["X-Asset-Metadata"].ToString());
        var command = new CreateDocumentCommand(
            team,
            logicalPath,
            Request.ContentType ?? "application/octet-stream",
            metadata,
            Request.Body,
            idempotencyKey);
        var result = await service.CreateDocumentAsync(command, cancellationToken).ConfigureAwait(false);
        var response = ResponseMapping.ToResponse(result.Value, result.Created);
        return result.Created
            ? CreatedAtAction(
                nameof(GetAsync),
                new { team, documentId = response.DocumentId },
                response)
            : Ok(response);
    }

    /// <summary>Appends an immutable document version from the raw request body.</summary>
    [HttpPost("{documentId:guid}/versions")]
    [ProducesResponseType<DocumentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DocumentResponse>> AppendVersionAsync(
        string team,
        Guid documentId,
        [FromQuery] string expectedVersion,
        [FromQuery] string bump,
        [FromQuery] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new AppendDocumentVersionCommand(
            team,
            documentId,
            ApiParsing.ParseExactVersion(expectedVersion),
            ApiParsing.ParseBump(bump),
            Request.ContentType ?? "application/octet-stream",
            ParseMetadata(Request.Headers["X-Asset-Metadata"].ToString()),
            Request.Body,
            idempotencyKey);
        var result = await service.AppendDocumentVersionAsync(command, cancellationToken).ConfigureAwait(false);
        var response = ResponseMapping.ToResponse(result.Value, result.Created);
        return result.Created
            ? CreatedAtAction(
                nameof(GetAsync),
                new { team, documentId, version = response.Version },
                response)
            : Ok(response);
    }

    /// <summary>Gets metadata for a selected document version.</summary>
    [HttpGet("{documentId:guid}")]
    [ProducesResponseType<DocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> GetAsync(
        string team,
        Guid documentId,
        [FromQuery] string? version,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.GetDocumentAsync(
            team, documentId, ApiParsing.ParseSelector(version), cancellationToken).ConfigureAwait(false);
        return snapshot is null ? NotFound() : Ok(ResponseMapping.ToResponse(snapshot, created: false));
    }

    /// <summary>Streams the raw bytes for a selected document version.</summary>
    [HttpGet("{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentAsync(
        string team,
        Guid documentId,
        [FromQuery] string? version,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var snapshot = await service.GetDocumentAsync(
            team, documentId, ApiParsing.ParseSelector(version), cancellationToken).ConfigureAwait(false);
        return snapshot is null
            ? NotFound()
            : await ContentResults.RawAsync(service, snapshot, download, cancellationToken).ConfigureAwait(false);
    }

    private static JsonElement ParseMetadata(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }
}
