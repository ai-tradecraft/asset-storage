using System.Text.Json;
using AssetStorage.Abstractions;

namespace AssetStorage.WebControllers;

internal static class ResponseMapping
{
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
