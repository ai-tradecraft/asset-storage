using Microsoft.Extensions.DependencyInjection;

namespace AssetStorage.WebControllers;

/// <summary>Registers the asset-storage HTTP translation layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds asset-storage controllers.</summary>
    public static IMvcBuilder AddAssetStorageControllers(this IServiceCollection services) =>
        services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
            .AddApplicationPart(typeof(DocumentsController).Assembly);
}
