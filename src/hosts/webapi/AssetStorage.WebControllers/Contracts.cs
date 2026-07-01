using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AssetStorage.WebControllers;

/// <summary>Describes one exact document version in a folder manifest.</summary>
public sealed record FolderEntryRequest
{
    /// <summary>Gets the folder-relative path.</summary>
    [Required]
    public required string Path { get; init; }

    /// <summary>Gets the exact document-version identifier.</summary>
    public required Guid DocumentVersionId { get; init; }
}

/// <summary>Creates a folder and its initial complete manifest.</summary>
public sealed record CreateFolderRequest
{
    /// <summary>Gets the optional logical mount path.</summary>
    public string? LogicalPath { get; init; }

    /// <summary>Gets the idempotency key.</summary>
    [Required]
    public required string IdempotencyKey { get; init; }

    /// <summary>Gets every entry in the complete manifest.</summary>
    [Required]
    public required IReadOnlyList<FolderEntryRequest> Entries { get; init; }
}

/// <summary>Appends a complete immutable folder manifest.</summary>
public sealed record AppendFolderVersionRequest
{
    /// <summary>Gets the expected current semantic version.</summary>
    [Required]
    public required string ExpectedVersion { get; init; }

    /// <summary>Gets the semantic component to increment.</summary>
    [Required]
    public required string Bump { get; init; }

    /// <summary>Gets the idempotency key.</summary>
    [Required]
    public required string IdempotencyKey { get; init; }

    /// <summary>Gets every entry in the complete manifest.</summary>
    [Required]
    public required IReadOnlyList<FolderEntryRequest> Entries { get; init; }
}

/// <summary>Returns immutable document metadata.</summary>
public sealed record DocumentResponse(
    Guid DocumentId,
    Guid DocumentVersionId,
    string Team,
    string? LogicalPath,
    string Version,
    string Sha256,
    long ContentLength,
    string ContentType,
    JsonElement Metadata,
    DateTimeOffset CreatedAt,
    bool Created);

/// <summary>Returns an immutable folder manifest.</summary>
public sealed record FolderResponse(
    Guid FolderId,
    Guid FolderVersionId,
    string Team,
    string? LogicalPath,
    string Version,
    string ManifestSha256,
    IReadOnlyList<FolderEntryRequest> Entries,
    DateTimeOffset CreatedAt,
    bool Created);
