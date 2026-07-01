namespace AssetStorage.Abstractions;

/// <summary>Stores and retrieves immutable object bytes.</summary>
public interface IObjectStore
{
    /// <summary>Stores content after calculating its SHA-256 digest.</summary>
    /// <param name="content">The content stream to store.</param>
    /// <param name="maximumLength">The maximum allowed content length in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stored object descriptor with key, digest, and length.</returns>
    /// <exception cref="AssetLimitExceededException">Thrown when content exceeds maximum length.</exception>
    Task<StoredObject> PutAsync(Stream content, long maximumLength, CancellationToken cancellationToken);

    /// <summary>Opens immutable content for reading.</summary>
    /// <param name="key">The content-addressable storage key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only stream of the stored content.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key does not exist.</exception>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Persists relational identities, versions, paths, and manifests.</summary>
public interface IMetadataStore
{
    /// <summary>Initializes the metadata schema.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Gets a document by stable identity.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, otherwise null.</returns>
    Task<Document?> GetDocumentAsync(string team, Guid documentId, CancellationToken cancellationToken);

    /// <summary>Gets a document by normalized logical path.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="normalizedPath">The normalized logical path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, otherwise null.</returns>
    Task<Document?> GetDocumentByPathAsync(string team, string normalizedPath, CancellationToken cancellationToken);

    /// <summary>Gets the current document version.</summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version if found, otherwise null.</returns>
    Task<DocumentVersion?> GetCurrentDocumentVersionAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Gets a selected document version.</summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="selector">The version selector criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching version if found, otherwise null.</returns>
    Task<DocumentVersion?> GetDocumentVersionAsync(
        Guid documentId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Gets an exact document version by internal ID.</summary>
    /// <param name="versionId">The document version identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version if found, otherwise null.</returns>
    Task<DocumentVersion?> GetDocumentVersionByIdAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>Creates a document and its initial version atomically.</summary>
    /// <param name="document">The document metadata.</param>
    /// <param name="version">The initial version.</param>
    /// <param name="idempotencyKey">The idempotency key for duplicate detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created document snapshot.</returns>
    /// <exception cref="AssetConflictException">Thrown when path, version, or key conflicts.</exception>
    Task<DocumentSnapshot> CreateDocumentAsync(
        Document document,
        DocumentVersion version,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Appends a document version with optimistic concurrency.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="version">The new version to append.</param>
    /// <param name="expectedVersion">The expected current version.</param>
    /// <param name="idempotencyKey">The idempotency key for duplicate detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The appended version.</returns>
    /// <exception cref="AssetConflictException">Thrown when version or key conflicts.</exception>
    Task<DocumentVersion> AppendDocumentVersionAsync(
        string team,
        DocumentVersion version,
        AssetVersion expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Finds a prior idempotent document result.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="idempotencyKey">The idempotency key to search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prior result if found, otherwise null.</returns>
    Task<DocumentVersion?> GetDocumentIdempotencyResultAsync(
        string team,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Gets a folder by stable identity.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="folderId">The folder identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder if found, otherwise null.</returns>
    Task<Folder?> GetFolderAsync(string team, Guid folderId, CancellationToken cancellationToken);

    /// <summary>Gets a folder by normalized logical path or owning prefix.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="normalizedPath">The normalized path or prefix.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder if found, otherwise null.</returns>
    Task<Folder?> GetFolderByPathPrefixAsync(string team, string normalizedPath, CancellationToken cancellationToken);

    /// <summary>Gets a selected folder manifest.</summary>
    /// <param name="folderId">The folder identifier.</param>
    /// <param name="selector">The version selector criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching folder version if found, otherwise null.</returns>
    Task<FolderVersion?> GetFolderVersionAsync(
        Guid folderId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Creates a folder and initial manifest atomically.</summary>
    /// <param name="folder">The folder metadata.</param>
    /// <param name="version">The initial manifest version.</param>
    /// <param name="idempotencyKey">The idempotency key for duplicate detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created folder snapshot.</returns>
    /// <exception cref="AssetConflictException">Thrown when path, version, or key conflicts.</exception>
    Task<FolderSnapshot> CreateFolderAsync(
        Folder folder,
        FolderVersion version,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Appends a folder manifest with optimistic concurrency.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="version">The new manifest version to append.</param>
    /// <param name="expectedVersion">The expected current version.</param>
    /// <param name="idempotencyKey">The idempotency key for duplicate detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The appended folder version.</returns>
    /// <exception cref="AssetConflictException">Thrown when version or key conflicts.</exception>
    Task<FolderVersion> AppendFolderVersionAsync(
        string team,
        FolderVersion version,
        AssetVersion expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Finds a prior idempotent folder result.</summary>
    /// <param name="team">The normalized team name.</param>
    /// <param name="idempotencyKey">The idempotency key to search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prior result if found, otherwise null.</returns>
    Task<FolderVersion?> GetFolderIdempotencyResultAsync(
        string team,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>Exposes document and immutable folder operations.</summary>
public interface IAssetStorageService
{
    /// <summary>Creates a document and its first version.</summary>
    /// <param name="command">The creation command with content and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A write result indicating if the document was created or reused via idempotency.</returns>
    /// <exception cref="AssetValidationException">Thrown when command validation fails.</exception>
    /// <exception cref="AssetConflictException">Thrown when path conflicts occur.</exception>
    /// <exception cref="AssetLimitExceededException">Thrown when size limits are exceeded.</exception>
    Task<VersionWriteResult<DocumentSnapshot>> CreateDocumentAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken);

    /// <summary>Appends a document version or returns the unchanged current version.</summary>
    /// <param name="command">The append command with new content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A write result indicating if a new version was created or the current one was reused.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the document does not exist.</exception>
    /// <exception cref="AssetConflictException">Thrown when version concurrency conflicts occur.</exception>
    /// <exception cref="AssetLimitExceededException">Thrown when size limits are exceeded.</exception>
    Task<VersionWriteResult<DocumentSnapshot>> AppendDocumentVersionAsync(
        AppendDocumentVersionCommand command,
        CancellationToken cancellationToken);

    /// <summary>Gets a selected document by identity.</summary>
    /// <param name="team">The team name.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="selector">The version selector criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document snapshot if found, otherwise null.</returns>
    Task<DocumentSnapshot?> GetDocumentAsync(
        string team,
        Guid documentId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Gets a selected document or mounted folder entry by logical path.</summary>
    /// <param name="team">The team name.</param>
    /// <param name="logicalPath">The logical path to resolve.</param>
    /// <param name="selector">The version selector criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved document snapshot if found, otherwise null.</returns>
    Task<DocumentSnapshot?> ResolvePathAsync(
        string team,
        string logicalPath,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Creates a folder and its initial manifest.</summary>
    /// <param name="command">The creation command with entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A write result indicating if the folder was created or reused via idempotency.</returns>
    /// <exception cref="AssetValidationException">Thrown when entries reference invalid versions.</exception>
    /// <exception cref="AssetConflictException">Thrown when path or entry conflicts occur.</exception>
    /// <exception cref="AssetLimitExceededException">Thrown when entry count limits are exceeded.</exception>
    Task<VersionWriteResult<FolderSnapshot>> CreateFolderAsync(
        CreateFolderCommand command,
        CancellationToken cancellationToken);

    /// <summary>Appends a complete folder manifest.</summary>
    /// <param name="command">The append command with updated entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A write result indicating if a new manifest was created or the current one was reused.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the folder does not exist.</exception>
    /// <exception cref="AssetConflictException">Thrown when version concurrency conflicts occur.</exception>
    /// <exception cref="AssetLimitExceededException">Thrown when entry count limits are exceeded.</exception>
    Task<VersionWriteResult<FolderSnapshot>> AppendFolderVersionAsync(
        AppendFolderVersionCommand command,
        CancellationToken cancellationToken);

    /// <summary>Gets a selected folder by identity.</summary>
    /// <param name="team">The team name.</param>
    /// <param name="folderId">The folder identifier.</param>
    /// <param name="selector">The version selector criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder snapshot if found, otherwise null.</returns>
    Task<FolderSnapshot?> GetFolderAsync(
        string team,
        Guid folderId,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Gets an exact entry from a selected folder manifest.</summary>
    /// <param name="team">The team name.</param>
    /// <param name="folderId">The folder identifier.</param>
    /// <param name="entryPath">The folder-relative entry path.</param>
    /// <param name="selector">The version selector criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry document snapshot if found, otherwise null.</returns>
    Task<DocumentSnapshot?> GetFolderEntryAsync(
        string team,
        Guid folderId,
        string entryPath,
        VersionSelector selector,
        CancellationToken cancellationToken);

    /// <summary>Opens a selected document's bytes.</summary>
    /// <param name="version">The document version to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A readable stream of the document content.</returns>
    Task<Stream> OpenContentAsync(DocumentVersion version, CancellationToken cancellationToken);
}
