namespace AssetStorage.Abstractions;

/// <summary>Stores and retrieves immutable object bytes.</summary>
public interface IObjectStore
{
    /// <summary>Stores content after calculating its SHA-256 digest.</summary>
    Task<StoredObject> PutAsync(Stream content, long maximumLength, CancellationToken cancellationToken);

    /// <summary>Opens immutable content for reading.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Persists relational identities, versions, paths, and manifests.</summary>
public interface IMetadataStore
{
    /// <summary>Initializes the metadata schema.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Gets a document by stable identity.</summary>
    Task<Document?> GetDocumentAsync(string team, Guid documentId, CancellationToken cancellationToken);

    /// <summary>Gets a document by normalized logical path.</summary>
    Task<Document?> GetDocumentByPathAsync(string team, string normalizedPath, CancellationToken cancellationToken);

    /// <summary>Gets the current document version.</summary>
    Task<DocumentVersion?> GetCurrentDocumentVersionAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Gets a selected document version.</summary>
    Task<DocumentVersion?> GetDocumentVersionAsync(
        Guid documentId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Gets an exact document version by internal ID.</summary>
    Task<DocumentVersion?> GetDocumentVersionByIdAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>Creates a document and its initial version atomically.</summary>
    Task<DocumentSnapshot> CreateDocumentAsync(
        Document document,
        DocumentVersion version,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Appends a document version with optimistic concurrency.</summary>
    Task<DocumentVersion> AppendDocumentVersionAsync(
        string team,
        DocumentVersion version,
        AssetVersion expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Finds a prior idempotent document result.</summary>
    Task<DocumentVersion?> GetDocumentIdempotencyResultAsync(
        string team,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Gets a folder by stable identity.</summary>
    Task<Folder?> GetFolderAsync(string team, Guid folderId, CancellationToken cancellationToken);

    /// <summary>Gets a folder by normalized logical path or owning prefix.</summary>
    Task<Folder?> GetFolderByPathPrefixAsync(string team, string normalizedPath, CancellationToken cancellationToken);

    /// <summary>Gets a selected folder manifest.</summary>
    Task<FolderVersion?> GetFolderVersionAsync(
        Guid folderId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Creates a folder and initial manifest atomically.</summary>
    Task<FolderSnapshot> CreateFolderAsync(
        Folder folder,
        FolderVersion version,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Appends a folder manifest with optimistic concurrency.</summary>
    Task<FolderVersion> AppendFolderVersionAsync(
        string team,
        FolderVersion version,
        AssetVersion expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Finds a prior idempotent folder result.</summary>
    Task<FolderVersion?> GetFolderIdempotencyResultAsync(
        string team,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>Exposes document and immutable folder operations.</summary>
public interface IAssetStorageService
{
    /// <summary>Creates a document and its first version.</summary>
    Task<VersionWriteResult<DocumentSnapshot>> CreateDocumentAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken);

    /// <summary>Appends a document version or returns the unchanged current version.</summary>
    Task<VersionWriteResult<DocumentSnapshot>> AppendDocumentVersionAsync(
        AppendDocumentVersionCommand command,
        CancellationToken cancellationToken);

    /// <summary>Gets a selected document by identity.</summary>
    Task<DocumentSnapshot?> GetDocumentAsync(
        string team,
        Guid documentId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Gets a selected document or mounted folder entry by logical path.</summary>
    Task<DocumentSnapshot?> ResolvePathAsync(
        string team,
        string logicalPath,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Creates a folder and its initial manifest.</summary>
    Task<VersionWriteResult<FolderSnapshot>> CreateFolderAsync(
        CreateFolderCommand command,
        CancellationToken cancellationToken);

    /// <summary>Appends a complete folder manifest.</summary>
    Task<VersionWriteResult<FolderSnapshot>> AppendFolderVersionAsync(
        AppendFolderVersionCommand command,
        CancellationToken cancellationToken);

    /// <summary>Gets a selected folder by identity.</summary>
    Task<FolderSnapshot?> GetFolderAsync(
        string team,
        Guid folderId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Gets an exact entry from a selected folder manifest.</summary>
    Task<DocumentSnapshot?> GetFolderEntryAsync(
        string team,
        Guid folderId,
        string entryPath,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Opens a selected document's bytes.</summary>
    Task<Stream> OpenContentAsync(DocumentVersion version, CancellationToken cancellationToken);
}
