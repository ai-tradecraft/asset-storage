using System.Net;
using System.Net.Http.Json;
using System.Text;
using AssetStorage.WebControllers;
using AwesomeAssertions;

namespace AssetStorage.WebApi.Tests;

public sealed class FolderApiTests(AssetStorageWebApplicationFactory factory)
    : IClassFixture<AssetStorageWebApplicationFactory>
{
    [Fact]
    public async Task FolderEndpoints_WhenManifestIsMounted_ThenLogicalChildReturnsExactContent()
    {
        // Arrange
        using var client = factory.CreateClient();
        using var upload = new HttpRequestMessage(
            HttpMethod.Post,
            "/team/_api/v1/documents?idempotencyKey=folder-document");
        upload.Content = new StringContent("folder content", Encoding.UTF8, "text/plain");
        using var uploadResponse = await client.SendAsync(upload, CancellationToken.None);
        var document = await uploadResponse.Content.ReadFromJsonAsync<DocumentResponse>(
            CancellationToken.None);
        var request = new CreateFolderRequest
        {
            LogicalPath = "mounted",
            IdempotencyKey = "folder-manifest",
            Entries =
            [
                new FolderEntryRequest
                {
                    Path = "nested/file.txt",
                    DocumentVersionId = document!.DocumentVersionId
                }
            ]
        };

        // Act
        using var folderResponse = await client.PostAsJsonAsync(
            new Uri("/team/_api/v1/folders", UriKind.Relative),
            request,
            CancellationToken.None);
        using var entryResponse = await client.GetAsync(
            new Uri("/team/mounted/nested/file.txt?raw=true", UriKind.Relative),
            CancellationToken.None);

        // Assert
        folderResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "submitting a complete manifest creates an immutable folder version");
        (await entryResponse.Content.ReadAsStringAsync(CancellationToken.None)).Should().Be(
            "folder content",
            "mounted paths must resolve the exact document version in the manifest");
    }
}
