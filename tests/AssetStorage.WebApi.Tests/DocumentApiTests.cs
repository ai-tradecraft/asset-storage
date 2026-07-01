using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AssetStorage.WebControllers;
using AwesomeAssertions;

namespace AssetStorage.WebApi.Tests;

public sealed class DocumentApiTests(AssetStorageWebApplicationFactory factory)
    : IClassFixture<AssetStorageWebApplicationFactory>
{
    [Fact]
    public async Task DocumentEndpoints_WhenMarkdownIsUploaded_ThenServeMetadataRawAndRenderedContent()
    {
        // Arrange
        using var client = factory.CreateClient();
        using var upload = new HttpRequestMessage(
            HttpMethod.Post,
            "/tradecraft/_api/v1/documents?logicalPath=agents/test.md&idempotencyKey=create-doc");
        upload.Content = new StringContent("# Hello", Encoding.UTF8, "text/markdown");
        upload.Headers.Add("X-Asset-Metadata", """{"kind":"skill"}""");

        // Act
        using var createResponse = await client.SendAsync(upload, CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<DocumentResponse>(
            CancellationToken.None);
        using var metadataResponse = await client.GetAsync(
            new Uri($"/tradecraft/_api/v1/documents/{created!.DocumentId}", UriKind.Relative),
            CancellationToken.None);
        using var rawResponse = await client.GetAsync(
            new Uri("/tradecraft/agents/test.md?raw=true", UriKind.Relative),
            CancellationToken.None);
        using var renderRequest = new HttpRequestMessage(HttpMethod.Get, "/tradecraft/agents/test.md");
        renderRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var renderedResponse = await client.SendAsync(renderRequest, CancellationToken.None);

        // Assert
        createResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "creating a document creates its initial immutable version");
        metadataResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the created document must be addressable by stable ID");
        (await rawResponse.Content.ReadAsStringAsync(CancellationToken.None)).Should().Be(
            "# Hello",
            "raw logical-path requests must return stored bytes");
        (await renderedResponse.Content.ReadAsStringAsync(CancellationToken.None)).Should().Contain(
            "<h1>Hello</h1>",
            "browser negotiation should safely render Markdown");
    }

    [Fact]
    public async Task DocumentEndpoints_WhenNormalizedPathAlreadyExists_ThenReturnConflictProblem()
    {
        // Arrange
        using var client = factory.CreateClient();
        using var first = CreateUpload("/Team/Readme.md", "first-path");
        using var second = CreateUpload("team/readme.MD", "second-path");
        _ = await client.SendAsync(first, CancellationToken.None);

        // Act
        using var response = await client.SendAsync(second, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "logical paths must be unique after portable case normalization");
        response.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/problem+json",
            "API failures should use RFC 7807 problem details");
    }

    [Fact]
    public async Task OpenApi_WhenDevelopmentHostStarts_ThenDocumentIsAvailable()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/openapi/v1.json", UriKind.Relative),
            CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the built-in OpenAPI document is part of the development API contract");
    }

    private static HttpRequestMessage CreateUpload(string logicalPath, string key) =>
        new(
            HttpMethod.Post,
            $"/tradecraft/_api/v1/documents?logicalPath={Uri.EscapeDataString(logicalPath)}&idempotencyKey={key}")
        {
            Content = new StringContent("content", Encoding.UTF8, "text/plain")
        };
}
