using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AssetStorage.WebApi.Tests;

public sealed class AssetStorageWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"asset-storage-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["AssetStorage:Sqlite:ConnectionString"] = $"Data Source={databasePath}"
                }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}
