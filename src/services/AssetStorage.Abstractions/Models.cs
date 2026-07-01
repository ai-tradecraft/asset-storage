using System.Text.Json;

namespace AssetStorage.Abstractions;

/// <summary>Specifies which semantic-version component to increment.</summary>
public enum VersionBump
{
    /// <summary>Increment the patch component.</summary>
    Patch,
    /// <summary>Increment the minor component and reset patch.</summary>
    Minor,
    /// <summary>Increment the major component and reset minor and patch.</summary>
    Major
}

/// <summary>Represents a stable semantic version.</summary>
public sealed record AssetVersion(int Major, int Minor, int Patch)
{
    /// <summary>The first version assigned to a resource.</summary>
    public static AssetVersion Initial => new(1, 0, 0);

    /// <inheritdoc />
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>Selects the latest matching or an exact semantic version.</summary>
public sealed record VersionSelector(int? Major = null, int? Minor = null, int? Patch = null);

/// <summary>Describes immutable stored object content.</summary>
public sealed record StoredObject(string Key, string Sha256, long Length);

/// <summary>Represents a stable document identity.</summary>
public sealed record Document(
    Guid Id,
    string Team,
    string? LogicalPath,
    string? NormalizedLogicalPath,
    DateTimeOffset CreatedAt);

/// <summary>Represents an immutable document revision.</summary>
public sealed record DocumentVersion(
    Guid Id,
    Guid DocumentId,
    AssetVersion Version,
    StoredObject Content,
    string ContentType,
    string MetadataJson,
    DateTimeOffset CreatedAt);

/// <summary>Returns a document and one selected immutable version.</summary>
public sealed record DocumentSnapshot(Document Document, DocumentVersion Version);

/// <summary>Represents a stable folder identity.</summary>
public sealed record Folder(
    Guid Id,
    string Team,
    string? LogicalPath,
    string? NormalizedLogicalPath,
    DateTimeOffset CreatedAt);

/// <summary>Maps one folder-relative path to an exact document version.</summary>
public sealed record FolderEntry(string Path, string NormalizedPath, Guid DocumentVersionId);

/// <summary>Represents an immutable folder manifest.</summary>
public sealed record FolderVersion(
    Guid Id,
    Guid FolderId,
    AssetVersion Version,
    string ManifestSha256,
    IReadOnlyList<FolderEntry> Entries,
    DateTimeOffset CreatedAt);

/// <summary>Returns a folder and one selected immutable manifest.</summary>
public sealed record FolderSnapshot(Folder Folder, FolderVersion Version);

/// <summary>Describes content used to create a document.</summary>
public sealed record CreateDocumentCommand(
    string Team,
    string? LogicalPath,
    string ContentType,
    JsonElement Metadata,
    Stream Content,
    string IdempotencyKey);

/// <summary>Describes content used to append a document version.</summary>
public sealed record AppendDocumentVersionCommand(
    string Team,
    Guid DocumentId,
    AssetVersion ExpectedVersion,
    VersionBump Bump,
    string ContentType,
    JsonElement Metadata,
    Stream Content,
    string IdempotencyKey);

/// <summary>Describes an exact entry in a new folder manifest.</summary>
public sealed record CreateFolderEntry(string Path, Guid DocumentVersionId);

/// <summary>Creates a stable folder and its initial manifest.</summary>
public sealed record CreateFolderCommand(
    string Team,
    string? LogicalPath,
    IReadOnlyList<CreateFolderEntry> Entries,
    string IdempotencyKey);

/// <summary>Appends an immutable folder manifest.</summary>
public sealed record AppendFolderVersionCommand(
    string Team,
    Guid FolderId,
    AssetVersion ExpectedVersion,
    VersionBump Bump,
    IReadOnlyList<CreateFolderEntry> Entries,
    string IdempotencyKey);

/// <summary>Indicates whether an append created a version or reused the current one.</summary>
public sealed record VersionWriteResult<T>(T Value, bool Created);
