using Microsoft.Extensions.DependencyInjection;

namespace AssetStorage.WebControllers;

/// <summary>Registers the asset-storage HTTP translation layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds asset-storage controllers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The MVC builder for additional controller configuration.</returns>
    public static IMvcBuilder AddAssetStorageControllers(this IServiceCollection services) =>
        services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
            .AddApplicationPart(typeof(DocumentsController).Assembly);
}
