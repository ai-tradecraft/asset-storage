using System.Globalization;
using AssetStorage.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace AssetStorage.Sqlite;

/// <summary>Persists relational asset metadata and immutable manifests in SQLite.</summary>
public sealed class SqliteMetadataStore(IOptions<SqliteStorageOptions> options) : IMetadataStore
{
    private readonly string connectionString = options.Value.ConnectionString;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS stored_objects (
                object_key TEXT PRIMARY KEY,
                sha256 TEXT NOT NULL UNIQUE,
                content_length INTEGER NOT NULL,
                content BLOB NOT NULL
            );
            CREATE TABLE IF NOT EXISTS documents (
                document_id TEXT PRIMARY KEY,
                team TEXT NOT NULL,
                logical_path TEXT NULL,
                normalized_path TEXT NULL,
                created_at TEXT NOT NULL,
                UNIQUE(team, normalized_path)
            );
            CREATE TABLE IF NOT EXISTS document_versions (
                document_version_id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL REFERENCES documents(document_id),
                version_major INTEGER NOT NULL,
                version_minor INTEGER NOT NULL,
                version_patch INTEGER NOT NULL,
                object_key TEXT NOT NULL REFERENCES stored_objects(object_key),
                sha256 TEXT NOT NULL,
                content_length INTEGER NOT NULL,
                content_type TEXT NOT NULL,
                metadata_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                UNIQUE(document_id, version_major, version_minor, version_patch)
            );
            CREATE TABLE IF NOT EXISTS folders (
                folder_id TEXT PRIMARY KEY,
                team TEXT NOT NULL,
                logical_path TEXT NULL,
                normalized_path TEXT NULL,
                created_at TEXT NOT NULL,
                UNIQUE(team, normalized_path)
            );
            CREATE TABLE IF NOT EXISTS folder_versions (
                folder_version_id TEXT PRIMARY KEY,
                folder_id TEXT NOT NULL REFERENCES folders(folder_id),
                version_major INTEGER NOT NULL,
                version_minor INTEGER NOT NULL,
                version_patch INTEGER NOT NULL,
                manifest_sha256 TEXT NOT NULL,
                created_at TEXT NOT NULL,
                UNIQUE(folder_id, version_major, version_minor, version_patch)
            );
            CREATE TABLE IF NOT EXISTS folder_entries (
                folder_version_id TEXT NOT NULL REFERENCES folder_versions(folder_version_id),
                entry_path TEXT NOT NULL,
                normalized_path TEXT NOT NULL,
                document_version_id TEXT NOT NULL REFERENCES document_versions(document_version_id),
                PRIMARY KEY(folder_version_id, normalized_path)
            );
            CREATE TABLE IF NOT EXISTS idempotency (
                team TEXT NOT NULL,
                idempotency_key TEXT NOT NULL,
                result_kind TEXT NOT NULL,
                result_version_id TEXT NOT NULL,
                PRIMARY KEY(team, idempotency_key, result_kind)
            );
            """;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Document?> GetDocumentAsync(
        string team,
        Guid documentId,
        CancellationToken cancellationToken) =>
        QueryDocumentAsync(
            "WHERE team = $team AND document_id = $id",
            [("$team", team), ("$id", documentId.ToString("D"))],
            cancellationToken);

    /// <inheritdoc />
    public Task<Document?> GetDocumentByPathAsync(
        string team,
        string normalizedPath,
        CancellationToken cancellationToken) =>
        QueryDocumentAsync(
            "WHERE team = $team AND normalized_path = $path",
            [("$team", team), ("$path", normalizedPath)],
            cancellationToken);

    /// <inheritdoc />
    public Task<DocumentVersion?> GetCurrentDocumentVersionAsync(
        Guid documentId,
        CancellationToken cancellationToken) =>
        QueryDocumentVersionAsync(
            "WHERE document_id = $id ORDER BY version_major DESC, version_minor DESC, version_patch DESC LIMIT 1",
            [("$id", documentId.ToString("D"))],
            cancellationToken);

    /// <inheritdoc />
    public Task<DocumentVersion?> GetDocumentVersionAsync(
        Guid documentId,
        VersionSelector selector,
        CancellationToken cancellationToken)
    {
        var filters = new List<string> { "document_id = $id" };
        var parameters = new List<(string, object)> { ("$id", documentId.ToString("D")) };
        AddVersionFilters(filters, parameters, selector);
        return QueryDocumentVersionAsync(
            $"WHERE {string.Join(" AND ", filters)} ORDER BY version_major DESC, version_minor DESC, version_patch DESC LIMIT 1",
            parameters,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<DocumentVersion?> GetDocumentVersionByIdAsync(
        Guid versionId,
        CancellationToken cancellationToken) =>
        QueryDocumentVersionAsync(
            "WHERE document_version_id = $id",
            [("$id", versionId.ToString("D"))],
            cancellationToken);

    /// <inheritdoc />
    public async Task<DocumentSnapshot> CreateDocumentAsync(
        Document document,
        DocumentVersion version,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await EnsurePathAvailableAsync(
                connection, transaction, document.Team, document.NormalizedLogicalPath, cancellationToken)
                .ConfigureAwait(false);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO documents (document_id, team, logical_path, normalized_path, created_at)
                VALUES ($id, $team, $path, $normalized, $created);
                """,
                [
                    ("$id", document.Id.ToString("D")),
                    ("$team", document.Team),
                    ("$path", DbValue(document.LogicalPath)),
                    ("$normalized", DbValue(document.NormalizedLogicalPath)),
                    ("$created", Format(document.CreatedAt))
                ],
                cancellationToken).ConfigureAwait(false);
            await InsertDocumentVersionAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
            await InsertIdempotencyAsync(
                connection, transaction, document.Team, idempotencyKey, "document",
                version.Id, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(document, version);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new AssetConflictException("The document path, version, or idempotency key already exists.", exception);
        }
        catch (SqliteException exception)
        {
            throw new MetadataStorageException("Failed to create document metadata.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<DocumentVersion> AppendDocumentVersionAsync(
        string team,
        DocumentVersion version,
        AssetVersion expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await EnsureCurrentDocumentVersionAsync(
                connection, transaction, version.DocumentId, expectedVersion, cancellationToken).ConfigureAwait(false);
            await InsertDocumentVersionAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
            await InsertIdempotencyAsync(
                connection, transaction, team, idempotencyKey, "document", version.Id, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return version;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new AssetConflictException("The document version or idempotency key already exists.", exception);
        }
    }

    /// <inheritdoc />
    public Task<DocumentVersion?> GetDocumentIdempotencyResultAsync(
        string team,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        QueryDocumentVersionAsync(
            """
            WHERE document_version_id = (
                SELECT result_version_id FROM idempotency
                WHERE team = $team AND idempotency_key = $key AND result_kind = 'document')
            """,
            [("$team", team), ("$key", idempotencyKey)],
            cancellationToken);

    /// <inheritdoc />
    public async Task<Folder?> GetFolderAsync(
        string team,
        Guid folderId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT folder_id, team, logical_path, normalized_path, created_at
            FROM folders WHERE team = $team AND folder_id = $id;
            """;
        command.Parameters.AddWithValue("$team", team);
        command.Parameters.AddWithValue("$id", folderId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadFolder(reader) : null;
    }

    /// <inheritdoc />
    public async Task<Folder?> GetFolderByPathPrefixAsync(
        string team,
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT folder_id, team, logical_path, normalized_path, created_at
            FROM folders
            WHERE team = $team
              AND normalized_path IS NOT NULL
              AND ($path = normalized_path OR $path LIKE normalized_path || '/%')
            ORDER BY length(normalized_path) DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$team", team);
        command.Parameters.AddWithValue("$path", normalizedPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadFolder(reader) : null;
    }

    /// <inheritdoc />
    public async Task<FolderVersion?> GetFolderVersionAsync(
        Guid folderId,
        VersionSelector selector,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var filters = new List<string> { "folder_id = $id" };
        var parameters = new List<(string, object)> { ("$id", folderId.ToString("D")) };
        AddVersionFilters(filters, parameters, selector);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT folder_version_id, folder_id, version_major, version_minor, version_patch,
                    manifest_sha256, created_at
             FROM folder_versions
             WHERE {string.Join(" AND ", filters)}
             ORDER BY version_major DESC, version_minor DESC, version_patch DESC LIMIT 1;
             """;
        AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var id = Guid.Parse(reader.GetString(0));
        var version = new AssetVersion(reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
        var hash = reader.GetString(5);
        var created = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture);
        await reader.DisposeAsync().ConfigureAwait(false);
        var entries = await ReadEntriesAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return new(id, Guid.Parse(command.Parameters["$id"].Value!.ToString()!), version, hash, entries, created);
    }

    /// <inheritdoc />
    public async Task<FolderSnapshot> CreateFolderAsync(
        Folder folder,
        FolderVersion version,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await EnsurePathAvailableAsync(
                connection, transaction, folder.Team, folder.NormalizedLogicalPath, cancellationToken)
                .ConfigureAwait(false);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO folders (folder_id, team, logical_path, normalized_path, created_at)
                VALUES ($id, $team, $path, $normalized, $created);
                """,
                [
                    ("$id", folder.Id.ToString("D")),
                    ("$team", folder.Team),
                    ("$path", DbValue(folder.LogicalPath)),
                    ("$normalized", DbValue(folder.NormalizedLogicalPath)),
                    ("$created", Format(folder.CreatedAt))
                ],
                cancellationToken).ConfigureAwait(false);
            await InsertFolderVersionAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
            await InsertIdempotencyAsync(
                connection, transaction, folder.Team, idempotencyKey, "folder", version.Id, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(folder, version);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new AssetConflictException("The folder path, version, or idempotency key already exists.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<FolderVersion> AppendFolderVersionAsync(
        string team,
        FolderVersion version,
        AssetVersion expectedVersion,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await EnsureCurrentFolderVersionAsync(
                connection, transaction, version.FolderId, expectedVersion, cancellationToken).ConfigureAwait(false);
            await InsertFolderVersionAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
            await InsertIdempotencyAsync(
                connection, transaction, team, idempotencyKey, "folder", version.Id, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return version;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new AssetConflictException("The folder version or idempotency key already exists.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<FolderVersion?> GetFolderIdempotencyResultAsync(
        string team,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT fv.folder_id, fv.version_major, fv.version_minor, fv.version_patch
            FROM folder_versions fv
            JOIN idempotency i ON i.result_version_id = fv.folder_version_id
            WHERE i.team = $team AND i.idempotency_key = $key AND i.result_kind = 'folder';
            """;
        command.Parameters.AddWithValue("$team", team);
        command.Parameters.AddWithValue("$key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var folderId = Guid.Parse(reader.GetString(0));
        var selector = new VersionSelector(reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
        await reader.DisposeAsync().ConfigureAwait(false);
        return await GetFolderVersionAsync(folderId, selector, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Document?> QueryDocumentAsync(
        string where,
        IReadOnlyList<(string, object)> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT document_id, team, logical_path, normalized_path, created_at FROM documents {where};";
        AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var logicalPath = reader.IsDBNull(2) ? null : reader.GetString(2);
        var normalizedPath = reader.IsDBNull(3) ? null : reader.GetString(3);
        return new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            logicalPath,
            normalizedPath,
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));
    }

    private async Task<DocumentVersion?> QueryDocumentVersionAsync(
        string where,
        IReadOnlyList<(string, object)> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT document_version_id, document_id, version_major, version_minor, version_patch,
                    object_key, sha256, content_length, content_type, metadata_json, created_at
             FROM document_versions {where};
             """;
        AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadDocumentVersion(reader)
            : null;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static DocumentVersion ReadDocumentVersion(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            new(reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)),
            new(reader.GetString(5), reader.GetString(6), reader.GetInt64(7)),
            reader.GetString(8),
            reader.GetString(9),
            DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture));

    private static Folder ReadFolder(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));

    private static async Task InsertDocumentVersionAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        DocumentVersion version,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO document_versions (
                document_version_id, document_id, version_major, version_minor, version_patch,
                object_key, sha256, content_length, content_type, metadata_json, created_at)
            VALUES ($id, $document, $major, $minor, $patch, $object, $hash, $length, $type, $metadata, $created);
            """,
            [
                ("$id", version.Id.ToString("D")),
                ("$document", version.DocumentId.ToString("D")),
                ("$major", version.Version.Major),
                ("$minor", version.Version.Minor),
                ("$patch", version.Version.Patch),
                ("$object", version.Content.Key),
                ("$hash", version.Content.Sha256),
                ("$length", version.Content.Length),
                ("$type", version.ContentType),
                ("$metadata", version.MetadataJson),
                ("$created", Format(version.CreatedAt))
            ],
            cancellationToken).ConfigureAwait(false);

    private static async Task InsertFolderVersionAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        FolderVersion version,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO folder_versions (
                folder_version_id, folder_id, version_major, version_minor, version_patch,
                manifest_sha256, created_at)
            VALUES ($id, $folder, $major, $minor, $patch, $hash, $created);
            """,
            [
                ("$id", version.Id.ToString("D")),
                ("$folder", version.FolderId.ToString("D")),
                ("$major", version.Version.Major),
                ("$minor", version.Version.Minor),
                ("$patch", version.Version.Patch),
                ("$hash", version.ManifestSha256),
                ("$created", Format(version.CreatedAt))
            ],
            cancellationToken).ConfigureAwait(false);
        foreach (var entry in version.Entries)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO folder_entries (
                    folder_version_id, entry_path, normalized_path, document_version_id)
                VALUES ($folderVersion, $path, $normalized, $documentVersion);
                """,
                [
                    ("$folderVersion", version.Id.ToString("D")),
                    ("$path", entry.Path),
                    ("$normalized", entry.NormalizedPath),
                    ("$documentVersion", entry.DocumentVersionId.ToString("D"))
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<FolderEntry>> ReadEntriesAsync(
        SqliteConnection connection,
        Guid folderVersionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT entry_path, normalized_path, document_version_id
            FROM folder_entries WHERE folder_version_id = $id ORDER BY normalized_path;
            """;
        command.Parameters.AddWithValue("$id", folderVersionId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<FolderEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new(reader.GetString(0), reader.GetString(1), Guid.Parse(reader.GetString(2))));
        }

        return entries;
    }

    private static async Task EnsurePathAvailableAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string team,
        string? path,
        CancellationToken cancellationToken)
    {
        if (path is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText =
            """
            SELECT EXISTS(
                SELECT 1 FROM documents
                WHERE team = $team AND (
                    normalized_path = $path OR normalized_path LIKE $path || '/%')
                UNION ALL
                SELECT 1 FROM folders
                WHERE team = $team AND (
                    normalized_path = $path
                    OR normalized_path LIKE $path || '/%'
                    OR $path LIKE normalized_path || '/%'));
            """;
        command.Parameters.AddWithValue("$team", team);
        command.Parameters.AddWithValue("$path", path);
        var exists = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        if (exists != 0)
        {
            throw new AssetConflictException($"Logical path '{path}' overlaps an existing resource.");
        }
    }

    private static Task EnsureCurrentDocumentVersionAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid documentId,
        AssetVersion expected,
        CancellationToken cancellationToken) =>
        EnsureCurrentVersionAsync(
            connection, transaction, "document_versions", "document_id", documentId,
            expected, cancellationToken);

    private static Task EnsureCurrentFolderVersionAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid folderId,
        AssetVersion expected,
        CancellationToken cancellationToken) =>
        EnsureCurrentVersionAsync(
            connection, transaction, "folder_versions", "folder_id", folderId,
            expected, cancellationToken);

    private static async Task EnsureCurrentVersionAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string table,
        string idColumn,
        Guid id,
        AssetVersion expected,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText =
            $"""
             SELECT version_major, version_minor, version_patch
             FROM {table} WHERE {idColumn} = $id
             ORDER BY version_major DESC, version_minor DESC, version_patch DESC LIMIT 1;
             """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            || reader.GetInt32(0) != expected.Major
            || reader.GetInt32(1) != expected.Minor
            || reader.GetInt32(2) != expected.Patch)
        {
            throw new AssetConflictException($"The expected version '{expected}' is no longer current.");
        }
    }

    private static Task InsertIdempotencyAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string team,
        string key,
        string kind,
        Guid resultId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO idempotency (team, idempotency_key, result_kind, result_version_id)
            VALUES ($team, $key, $kind, $result);
            """,
            [
                ("$team", team),
                ("$key", key),
                ("$kind", kind),
                ("$result", resultId.ToString("D"))
            ],
            cancellationToken);

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string sql,
        IReadOnlyList<(string, object)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddVersionFilters(
        List<string> filters,
        List<(string, object)> parameters,
        VersionSelector selector)
    {
        if (selector.Major is int major)
        {
            filters.Add("version_major = $major");
            parameters.Add(("$major", major));
        }
        if (selector.Minor is int minor)
        {
            filters.Add("version_minor = $minor");
            parameters.Add(("$minor", minor));
        }
        if (selector.Patch is int patch)
        {
            filters.Add("version_patch = $patch");
            parameters.Add(("$patch", patch));
        }
    }

    private static void AddParameters(
        SqliteCommand command,
        IEnumerable<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }

    private static object DbValue(string? value) => (object?)value ?? DBNull.Value;

    private static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);
}
