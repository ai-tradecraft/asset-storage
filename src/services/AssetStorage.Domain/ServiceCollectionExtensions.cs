using AssetStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssetStorage.Domain;

/// <summary>Registers asset-storage domain services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds the domain service and validated limits.</summary>
    public static IServiceCollection AddAssetStorageDomain(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AssetStorageOptions>()
            .Bind(configuration.GetSection(AssetStorageOptions.SectionName))
            .Validate(options => options.MaximumObjectBytes > 0, "MaximumObjectBytes must be positive.")
            .Validate(options => options.MaximumRequestBytes > 0, "MaximumRequestBytes must be positive.")
            .Validate(options => options.MaximumFolderEntries > 0, "MaximumFolderEntries must be positive.")
            .Validate(options => options.MaximumPathLength > 0, "MaximumPathLength must be positive.")
            .Validate(options => options.MaximumMetadataBytes > 0, "MaximumMetadataBytes must be positive.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAssetStorageService, AssetStorageService>();
        return services;
    }
}
