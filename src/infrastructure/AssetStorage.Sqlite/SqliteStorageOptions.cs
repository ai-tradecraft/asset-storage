namespace AssetStorage.Sqlite;

/// <summary>Configures the local SQLite storage adapters.</summary>
public sealed class SqliteStorageOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "AssetStorage:Sqlite";

    /// <summary>Gets or sets the SQLite connection string.</summary>
    public string ConnectionString { get; set; } = "Data Source=asset-storage.db";
}
