using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssetStorage.Abstractions;
using Microsoft.Extensions.Options;

namespace AssetStorage.Domain;

/// <summary>Coordinates immutable document and folder operations.</summary>
public sealed class AssetStorageService(
    IMetadataStore metadataStore,
    IObjectStore objectStore,
    TimeProvider timeProvider,
    IOptions<AssetStorageOptions> options) : IAssetStorageService
{
    private readonly AssetStorageOptions limits = options.Value;

    /// <inheritdoc />
    public async Task<VersionWriteResult<DocumentSnapshot>> CreateDocumentAsync(
        CreateDocumentCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCommon(command.Team, command.ContentType, command.IdempotencyKey);
        var team = PathNormalizer.NormalizeTeam(command.Team);
        var prior = await metadataStore.GetDocumentIdempotencyResultAsync(
            team, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
        {
            var priorDocument = await metadataStore.GetDocumentAsync(
                team, prior.DocumentId, cancellationToken).ConfigureAwait(false);
            return new(new(priorDocument!, prior), false);
        }

        var path = NormalizeOptionalPath(command.LogicalPath);
        var metadata = ValidateMetadata(command.Metadata);
        var storedObject = await objectStore.PutAsync(
            command.Content, limits.MaximumObjectBytes, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var document = new Document(Guid.NewGuid(), team, command.LogicalPath, path, now);
        var version = new DocumentVersion(
            Guid.NewGuid(), document.Id, AssetVersion.Initial, storedObject,
            command.ContentType, metadata, now);
        var snapshot = await metadataStore.CreateDocumentAsync(
            document, version, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return new(snapshot, true);
    }

    /// <inheritdoc />
    public async Task<VersionWriteResult<DocumentSnapshot>> AppendDocumentVersionAsync(
        AppendDocumentVersionCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCommon(command.Team, command.ContentType, command.IdempotencyKey);
        var team = PathNormalizer.NormalizeTeam(command.Team);
        var document = await metadataStore.GetDocumentAsync(
            team, command.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Document '{command.DocumentId}' was not found in team '{team}'.");
        var prior = await metadataStore.GetDocumentIdempotencyResultAsync(
            team, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
        {
            return new(new(document, prior), false);
        }

        var current = await metadataStore.GetCurrentDocumentVersionAsync(
            document.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Document '{document.Id}' has no versions.");
        if (!current.Version.Equals(command.ExpectedVersion))
        {
            throw new AssetConflictException(
                $"Expected document version '{command.ExpectedVersion}', but current is '{current.Version}'.");
        }

        var metadata = ValidateMetadata(command.Metadata);
        var storedObject = await objectStore.PutAsync(
            command.Content, limits.MaximumObjectBytes, cancellationToken).ConfigureAwait(false);
        if (string.Equals(storedObject.Sha256, current.Content.Sha256, StringComparison.Ordinal)
            && string.Equals(metadata, current.MetadataJson, StringComparison.Ordinal)
            && string.Equals(command.ContentType, current.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return new(new(document, current), false);
        }

        var version = new DocumentVersion(
            Guid.NewGuid(),
            document.Id,
            SemanticVersions.Bump(current.Version, command.Bump),
            storedObject,
            command.ContentType,
            metadata,
            timeProvider.GetUtcNow());
        var appended = await metadataStore.AppendDocumentVersionAsync(
            team, version, command.ExpectedVersion, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        return new(new(document, appended), true);
    }

    /// <inheritdoc />
    public async Task<DocumentSnapshot?> GetDocumentAsync(
        string team,
        Guid documentId,
        VersionSelector selector,
        CancellationToken cancellationToken)
    {
        var normalizedTeam = PathNormalizer.NormalizeTeam(team);
        var document = await metadataStore.GetDocumentAsync(
            normalizedTeam, documentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        var version = await metadataStore.GetDocumentVersionAsync(
            documentId, selector, cancellationToken).ConfigureAwait(false);
        return version is null ? null : new(document, version);
    }

    /// <inheritdoc />
    public async Task<DocumentSnapshot?> ResolvePathAsync(
        string team,
        string logicalPath,
        VersionSelector selector,
        CancellationToken cancellationToken)
    {
        var normalizedTeam = PathNormalizer.NormalizeTeam(team);
        var normalizedPath = PathNormalizer.Normalize(logicalPath, limits.MaximumPathLength);
        var document = await metadataStore.GetDocumentByPathAsync(
            normalizedTeam, normalizedPath, cancellationToken).ConfigureAwait(false);
        if (document is not null)
        {
            return await GetDocumentAsync(
                normalizedTeam, document.Id, selector, cancellationToken).ConfigureAwait(false);
        }

        var folder = await metadataStore.GetFolderByPathPrefixAsync(
            normalizedTeam, normalizedPath, cancellationToken).ConfigureAwait(false);
        if (folder?.NormalizedLogicalPath is null)
        {
            return null;
        }

        if (string.Equals(
            normalizedPath,
            folder.NormalizedLogicalPath,
            StringComparison.Ordinal))
        {
            return null;
        }

        var entryPath = normalizedPath[(folder.NormalizedLogicalPath.Length + 1)..];
        var folderVersion = await metadataStore.GetFolderVersionAsync(
            folder.Id, selector, cancellationToken).ConfigureAwait(false);
        var entry = folderVersion?.Entries.FirstOrDefault(
            candidate => string.Equals(candidate.NormalizedPath, entryPath, StringComparison.Ordinal));
        if (entry is null)
        {
            return null;
        }

        var documentVersion = await metadataStore.GetDocumentVersionByIdAsync(
            entry.DocumentVersionId, cancellationToken).ConfigureAwait(false);
        if (documentVersion is null)
        {
            return null;
        }

        var owningDocument = await metadataStore.GetDocumentAsync(
            normalizedTeam, documentVersion.DocumentId, cancellationToken).ConfigureAwait(false);
        return owningDocument is null ? null : new(owningDocument, documentVersion);
    }

    /// <inheritdoc />
    public async Task<VersionWriteResult<FolderSnapshot>> CreateFolderAsync(
        CreateFolderCommand command,
        CancellationToken cancellationToken)
    {
        ValidateKey(command.IdempotencyKey);
        var team = PathNormalizer.NormalizeTeam(command.Team);
        var prior = await metadataStore.GetFolderIdempotencyResultAsync(
            team, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
        {
            var priorFolder = await metadataStore.GetFolderAsync(
                team, prior.FolderId, cancellationToken).ConfigureAwait(false);
            return new(new(priorFolder!, prior), false);
        }

        var entries = await ValidateEntriesAsync(team, command.Entries, cancellationToken)
            .ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var folder = new Folder(
            Guid.NewGuid(), team, command.LogicalPath, NormalizeOptionalPath(command.LogicalPath), now);
        var version = new FolderVersion(
            Guid.NewGuid(), folder.Id, AssetVersion.Initial, HashManifest(entries), entries, now);
        var snapshot = await metadataStore.CreateFolderAsync(
            folder, version, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        return new(snapshot, true);
    }

    /// <inheritdoc />
    public async Task<VersionWriteResult<FolderSnapshot>> AppendFolderVersionAsync(
        AppendFolderVersionCommand command,
        CancellationToken cancellationToken)
    {
        ValidateKey(command.IdempotencyKey);
        var team = PathNormalizer.NormalizeTeam(command.Team);
        var folder = await metadataStore.GetFolderAsync(
            team, command.FolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Folder '{command.FolderId}' was not found in team '{team}'.");
        var prior = await metadataStore.GetFolderIdempotencyResultAsync(
            team, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
        {
            return new(new(folder, prior), false);
        }

        var current = await metadataStore.GetFolderVersionAsync(
            folder.Id, new(), cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Folder '{folder.Id}' has no versions.");
        if (!current.Version.Equals(command.ExpectedVersion))
        {
            throw new AssetConflictException(
                $"Expected folder version '{command.ExpectedVersion}', but current is '{current.Version}'.");
        }

        var entries = await ValidateEntriesAsync(team, command.Entries, cancellationToken)
            .ConfigureAwait(false);
        var hash = HashManifest(entries);
        if (string.Equals(hash, current.ManifestSha256, StringComparison.Ordinal))
        {
            return new(new(folder, current), false);
        }

        var version = new FolderVersion(
            Guid.NewGuid(),
            folder.Id,
            SemanticVersions.Bump(current.Version, command.Bump),
            hash,
            entries,
            timeProvider.GetUtcNow());
        var appended = await metadataStore.AppendFolderVersionAsync(
            team, version, command.ExpectedVersion, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        return new(new(folder, appended), true);
    }

    /// <inheritdoc />
    public async Task<FolderSnapshot?> GetFolderAsync(
        string team,
        Guid folderId,
        VersionSelector selector,
        CancellationToken cancellationToken)
    {
        var normalizedTeam = PathNormalizer.NormalizeTeam(team);
        var folder = await metadataStore.GetFolderAsync(
            normalizedTeam, folderId, cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        var version = await metadataStore.GetFolderVersionAsync(
            folderId, selector, cancellationToken).ConfigureAwait(false);
        return version is null ? null : new(folder, version);
    }

    /// <inheritdoc />
    public async Task<DocumentSnapshot?> GetFolderEntryAsync(
        string team,
        Guid folderId,
        string entryPath,
        VersionSelector selector,
        CancellationToken cancellationToken)
    {
        var normalizedTeam = PathNormalizer.NormalizeTeam(team);
        var folder = await GetFolderAsync(
            normalizedTeam, folderId, selector, cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        var normalizedEntryPath = PathNormalizer.Normalize(entryPath, limits.MaximumPathLength);
        var entry = folder.Version.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.NormalizedPath, normalizedEntryPath, StringComparison.Ordinal));
        if (entry is null)
        {
            return null;
        }

        var version = await metadataStore.GetDocumentVersionByIdAsync(
            entry.DocumentVersionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return null;
        }

        var document = await metadataStore.GetDocumentAsync(
            normalizedTeam, version.DocumentId, cancellationToken).ConfigureAwait(false);
        return document is null ? null : new(document, version);
    }

    /// <inheritdoc />
    public Task<Stream> OpenContentAsync(
        DocumentVersion version,
        CancellationToken cancellationToken) =>
        objectStore.OpenReadAsync(version.Content.Key, cancellationToken);

    private async Task<IReadOnlyList<FolderEntry>> ValidateEntriesAsync(
        string team,
        IReadOnlyList<CreateFolderEntry> requested,
        CancellationToken cancellationToken)
    {
        if (requested.Count > limits.MaximumFolderEntries)
        {
            throw new AssetLimitExceededException("folder entry count", limits.MaximumFolderEntries);
        }

        var entries = new List<FolderEntry>(requested.Count);
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requestedEntry in requested)
        {
            var path = PathNormalizer.Normalize(requestedEntry.Path, limits.MaximumPathLength);
            if (!normalized.Add(path))
            {
                throw new AssetConflictException($"Folder path '{requestedEntry.Path}' occurs more than once.");
            }

            var version = await metadataStore.GetDocumentVersionByIdAsync(
                requestedEntry.DocumentVersionId, cancellationToken).ConfigureAwait(false)
                ?? throw new AssetValidationException(
                    $"Document version '{requestedEntry.DocumentVersionId}' does not exist.");
            _ = await metadataStore.GetDocumentAsync(
                team, version.DocumentId, cancellationToken).ConfigureAwait(false)
                ?? throw new AssetValidationException(
                    $"Document version '{requestedEntry.DocumentVersionId}' belongs to another team.");

            entries.Add(new(requestedEntry.Path, path, requestedEntry.DocumentVersionId));
        }

        return entries;
    }

    private string? NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : PathNormalizer.Normalize(path, limits.MaximumPathLength);

    private string ValidateMetadata(JsonElement metadata)
    {
        var json = metadata.ValueKind is JsonValueKind.Undefined ? "{}" : metadata.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > limits.MaximumMetadataBytes)
        {
            throw new AssetLimitExceededException("metadata bytes", limits.MaximumMetadataBytes);
        }

        return json;
    }

    private static string HashManifest(IEnumerable<FolderEntry> entries)
    {
        var canonical = string.Join(
            '\n',
            entries.OrderBy(entry => entry.NormalizedPath, StringComparer.Ordinal)
                .Select(entry => $"{entry.NormalizedPath}\0{entry.DocumentVersionId:N}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ValidateCommon(string team, string contentType, string key)
    {
        _ = PathNormalizer.NormalizeTeam(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ValidateKey(key);
    }

    private static void ValidateKey(string key) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
}
