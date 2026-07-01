var builder = DistributedApplication.CreateBuilder(args);

var sqliteConnection = builder.AddParameter(
    "sqlite-connection",
    "Data Source=asset-storage.db");

builder.AddProject<Projects.AssetStorage_WebHost>("asset-storage")
    .WithEnvironment("AssetStorage__Sqlite__ConnectionString", sqliteConnection)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync().ConfigureAwait(false);
