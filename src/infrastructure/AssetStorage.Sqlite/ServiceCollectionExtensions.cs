using AssetStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssetStorage.Sqlite;

/// <summary>Registers the SQLite metadata and object-store adapters.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds independently registered SQLite storage ports.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing AssetStorage:Sqlite section.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAssetStorageSqlite(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SqliteStorageOptions>()
            .Bind(configuration.GetSection(SqliteStorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "The SQLite connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<IMetadataStore, SqliteMetadataStore>();
        services.AddSingleton<IObjectStore, SqliteObjectStore>();
        return services;
    }
}
