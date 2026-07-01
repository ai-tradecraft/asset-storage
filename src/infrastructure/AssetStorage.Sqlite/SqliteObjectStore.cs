using System.Security.Cryptography;
using AssetStorage.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace AssetStorage.Sqlite;

/// <summary>Stores immutable object content in SQLite by SHA-256 digest.</summary>
public sealed class SqliteObjectStore(IOptions<SqliteStorageOptions> options) : IObjectStore
{
    private readonly string connectionString = options.Value.ConnectionString;

    /// <inheritdoc />
    public async Task<StoredObject> PutAsync(
        Stream content,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            await using var buffer = new MemoryStream();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var chunk = new byte[81920];
            long length = 0;
            int read;
            while ((read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                length += read;
                if (length > maximumLength)
                {
                    throw new AssetLimitExceededException("object bytes", maximumLength);
                }

                hash.AppendData(chunk, 0, read);
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            var digest = Convert.ToHexStringLower(hash.GetHashAndReset());
            var key = $"sha256:{digest}";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO stored_objects (object_key, sha256, content_length, content)
                VALUES ($key, $hash, $length, $content)
                ON CONFLICT(object_key) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$hash", digest);
            command.Parameters.AddWithValue("$length", length);
            command.Parameters.AddWithValue("$content", buffer.ToArray());
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new(key, digest, length);
        }
        catch (AssetLimitExceededException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            throw new ObjectStorageException("store immutable content", exception);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT content FROM stored_objects WHERE object_key = $key;";
            command.Parameters.AddWithValue("$key", key);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is byte[] bytes
                ? new MemoryStream(bytes, writable: false)
                : throw new KeyNotFoundException($"Stored object '{key}' was not found.");
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            throw new ObjectStorageException("read immutable content", exception);
        }
    }
}
