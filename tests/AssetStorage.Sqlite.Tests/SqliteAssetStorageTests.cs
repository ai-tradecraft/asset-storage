using System.Text;
using System.Text.Json;
using AssetStorage.Abstractions;
using AssetStorage.Domain;
using AssetStorage.Sqlite;
using AwesomeAssertions;
using Microsoft.Extensions.Options;

namespace AssetStorage.Sqlite.Tests;

public sealed class SqliteAssetStorageTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"asset-storage-{Guid.NewGuid():N}.db");
    private IMetadataStore metadataStore = null!;
    private IAssetStorageService service = null!;

    public async Task InitializeAsync()
    {
        var sqliteOptions = Options.Create(
            new SqliteStorageOptions { ConnectionString = $"Data Source={databasePath}" });
        metadataStore = new SqliteMetadataStore(sqliteOptions);
        var objectStore = new SqliteObjectStore(sqliteOptions);
        service = new AssetStorageService(
            metadataStore,
            objectStore,
            TimeProvider.System,
            Options.Create(new AssetStorageOptions()));
        await metadataStore.InitializeAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DocumentWorkflow_WhenContentAndMetadataChange_ThenVersionsRemainImmutable()
    {
        // Arrange
        using var emptyMetadata = JsonDocument.Parse("{}");
        var created = await service.CreateDocumentAsync(
            new(
                "Tradecraft",
                "agents/dotnet.md",
                "text/markdown",
                emptyMetadata.RootElement,
                StreamOf("# One"),
                "create-1"),
            CancellationToken.None);

        // Act
        var unchanged = await service.AppendDocumentVersionAsync(
            new(
                "tradecraft",
                created.Value.Document.Id,
                AssetVersion.Initial,
                VersionBump.Minor,
                "text/markdown",
                emptyMetadata.RootElement,
                StreamOf("# One"),
                "append-no-op"),
            CancellationToken.None);
        using var changedMetadata = JsonDocument.Parse("""{"kind":"skill"}""");
        var metadataOnly = await service.AppendDocumentVersionAsync(
            new(
                "tradecraft",
                created.Value.Document.Id,
                AssetVersion.Initial,
                VersionBump.Minor,
                "text/markdown",
                changedMetadata.RootElement,
                StreamOf("# One"),
                "append-metadata"),
            CancellationToken.None);
        var contentChange = await service.AppendDocumentVersionAsync(
            new(
                "tradecraft",
                created.Value.Document.Id,
                new(1, 1, 0),
                VersionBump.Major,
                "text/markdown",
                changedMetadata.RootElement,
                StreamOf("# Two"),
                "append-content"),
            CancellationToken.None);

        // Assert
        unchanged.Created.Should().BeFalse("identical content and metadata must be a no-op");
        metadataOnly.Created.Should().BeTrue("metadata-only changes are immutable versions");
        metadataOnly.Value.Version.Content.Key.Should().Be(
            created.Value.Version.Content.Key,
            "metadata-only versions should reuse immutable object bytes");
        metadataOnly.Value.Version.Version.Should().Be(
            new AssetVersion(1, 1, 0),
            "a minor bump was requested");
        contentChange.Value.Version.Version.Should().Be(
            new AssetVersion(2, 0, 0),
            "a major bump must reset lower components");
        contentChange.Value.Version.Content.Sha256.Should().NotBe(
            created.Value.Version.Content.Sha256,
            "changed bytes must produce a different digest");
    }

    [Fact]
    public async Task FolderWorkflow_WhenNewManifestReusesEntries_ThenOldVersionKeepsExactPointers()
    {
        // Arrange
        using var metadata = JsonDocument.Parse("{}");
        var first = await CreateDocumentAsync("one.txt", "one", metadata.RootElement);
        var second = await CreateDocumentAsync("two.txt", "two", metadata.RootElement);
        var folder = await service.CreateFolderAsync(
            new(
                "team",
                "project",
                [
                    new("one.txt", first.Value.Version.Id),
                    new("two.txt", second.Value.Version.Id)
                ],
                "folder-create"),
            CancellationToken.None);
        var changed = await service.AppendDocumentVersionAsync(
            new(
                "team",
                first.Value.Document.Id,
                AssetVersion.Initial,
                VersionBump.Minor,
                "text/plain",
                metadata.RootElement,
                StreamOf("one changed"),
                "one-change"),
            CancellationToken.None);

        // Act
        var updated = await service.AppendFolderVersionAsync(
            new(
                "team",
                folder.Value.Folder.Id,
                AssetVersion.Initial,
                VersionBump.Minor,
                [
                    new("one.txt", changed.Value.Version.Id),
                    new("two.txt", second.Value.Version.Id)
                ],
                "folder-update"),
            CancellationToken.None);
        var oldEntry = await service.GetFolderEntryAsync(
            "team",
            folder.Value.Folder.Id,
            "one.txt",
            new(1, 0, 0),
            CancellationToken.None);

        // Assert
        updated.Value.Version.Entries[1].DocumentVersionId.Should().Be(
            second.Value.Version.Id,
            "unchanged files must reuse exact version pointers");
        oldEntry!.Version.Id.Should().Be(
            first.Value.Version.Id,
            "older manifests must remain exact immutable snapshots");
    }

    private async Task<VersionWriteResult<DocumentSnapshot>> CreateDocumentAsync(
        string path,
        string content,
        JsonElement metadata) =>
        await service.CreateDocumentAsync(
            new("team", path, "text/plain", metadata, StreamOf(content), $"create-{path}"),
            CancellationToken.None);

    private static MemoryStream StreamOf(string value) =>
        new(Encoding.UTF8.GetBytes(value), writable: false);
}
