using System.Text.Json;
using System.Text.Json.Serialization;
using AssetStorage.Abstractions;
using AssetStorage.Domain;
using AssetStorage.Sqlite;
using AssetStorage.WebControllers;
using AssetStorage.WebHost.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        builder.Configuration.GetValue<long?>("AssetStorage:MaximumRequestBytes")
        ?? 65 * 1024 * 1024;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddAssetStorageControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        options.JsonSerializerOptions.AllowDuplicateProperties = false;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddAssetStorageDomain(builder.Configuration);
builder.Services.AddAssetStorageSqlite(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapDefaultEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var metadata = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
    await metadata.InitializeAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposes the entry point to integration tests.</summary>
public partial class Program;
