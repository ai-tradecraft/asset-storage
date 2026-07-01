namespace AssetStorage.Domain;

/// <summary>Configurable limits enforced by the asset-storage domain.</summary>
public sealed class AssetStorageOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "AssetStorage";

    /// <summary>Gets or sets the maximum object size.</summary>
    public long MaximumObjectBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>Gets or sets the maximum HTTP request size.</summary>
    public long MaximumRequestBytes { get; set; } = 65 * 1024 * 1024;

    /// <summary>Gets or sets the maximum folder entry count.</summary>
    public int MaximumFolderEntries { get; set; } = 10_000;

    /// <summary>Gets or sets the maximum normalized path length.</summary>
    public int MaximumPathLength { get; set; } = 1_024;

    /// <summary>Gets or sets the maximum serialized metadata length.</summary>
    public int MaximumMetadataBytes { get; set; } = 64 * 1024;
}
