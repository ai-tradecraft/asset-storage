using System.Text.Json;
using AssetStorage.Abstractions;

namespace AssetStorage.WebControllers;

/// <summary>Maps domain snapshots to API response contracts.</summary>
internal static class ResponseMapping
{
    /// <summary>Maps a document snapshot to an API response.</summary>
    /// <param name="snapshot">The document snapshot from the domain.</param>
    /// <param name="created">Indicates whether this was a creation or idempotent reuse.</param>
    /// <returns>A document response contract.</returns>
    internal static DocumentResponse ToResponse(DocumentSnapshot snapshot, bool created)
    {
        using var metadata = JsonDocument.Parse(snapshot.Version.MetadataJson);
        return new(
            snapshot.Document.Id,
            snapshot.Version.Id,
            snapshot.Document.Team,
            snapshot.Document.LogicalPath,
            snapshot.Version.Version.ToString(),
            snapshot.Version.Content.Sha256,
            snapshot.Version.Content.Length,
            snapshot.Version.ContentType,
            metadata.RootElement.Clone(),
            snapshot.Version.CreatedAt,
            created);
    }

    /// <summary>Maps a folder snapshot to an API response.</summary>
    /// <param name="snapshot">The folder snapshot from the domain.</param>
    /// <param name="created">Indicates whether this was a creation or idempotent reuse.</param>
    /// <returns>A folder response contract.</returns>
    internal static FolderResponse ToResponse(FolderSnapshot snapshot, bool created) =>
        new(
            snapshot.Folder.Id,
            snapshot.Version.Id,
            snapshot.Folder.Team,
            snapshot.Folder.LogicalPath,
            snapshot.Version.Version.ToString(),
            snapshot.Version.ManifestSha256,
            snapshot.Version.Entries
                .Select(entry => new FolderEntryRequest
                {
                    Path = entry.Path,
                    DocumentVersionId = entry.DocumentVersionId
                })
                .ToArray(),
            snapshot.Version.CreatedAt,
            created);
}
