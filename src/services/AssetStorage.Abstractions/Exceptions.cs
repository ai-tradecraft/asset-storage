namespace AssetStorage.Abstractions;

/// <summary>Signals invalid client input at the domain boundary.</summary>
public class AssetValidationException : Exception
{
    /// <summary>Initializes an empty exception.</summary>
    public AssetValidationException() { }
    /// <summary>Initializes an exception with a message.</summary>
    public AssetValidationException(string message) : base(message) { }
    /// <summary>Initializes an exception with a message and cause.</summary>
    public AssetValidationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Signals optimistic concurrency or uniqueness conflicts.</summary>
public class AssetConflictException : Exception
{
    /// <summary>Initializes an empty exception.</summary>
    public AssetConflictException() { }
    /// <summary>Initializes an exception with a message.</summary>
    public AssetConflictException(string message) : base(message) { }
    /// <summary>Initializes an exception with a message and cause.</summary>
    public AssetConflictException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Signals that a configured storage limit was exceeded.</summary>
public class AssetLimitExceededException : Exception
{
    /// <summary>Initializes an empty exception.</summary>
    public AssetLimitExceededException() { }
    /// <summary>Initializes an exception with a message.</summary>
    public AssetLimitExceededException(string message) : base(message) { }
    /// <summary>Initializes an exception with a message and cause.</summary>
    public AssetLimitExceededException(string message, Exception innerException) : base(message, innerException) { }
    /// <summary>Initializes an exception for a named configured limit.</summary>
    public AssetLimitExceededException(string limitName, long limit)
        : base($"The configured {limitName} limit of {limit} was exceeded.")
    {
        LimitName = limitName;
        Limit = limit;
    }

    /// <summary>Gets the configured limit name.</summary>
    public string? LimitName { get; }
    /// <summary>Gets the configured limit.</summary>
    public long Limit { get; }
}

/// <summary>Wraps an object-storage adapter failure.</summary>
public class ObjectStorageException : Exception
{
    /// <summary>Initializes an empty exception.</summary>
    public ObjectStorageException() { }
    /// <summary>Initializes an exception with a message.</summary>
    public ObjectStorageException(string message) : base(message) { }
    /// <summary>Initializes an exception with a message and cause.</summary>
    public ObjectStorageException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Wraps a relational metadata adapter failure.</summary>
public class MetadataStorageException : Exception
{
    /// <summary>Initializes an empty exception.</summary>
    public MetadataStorageException() { }
    /// <summary>Initializes an exception with a message.</summary>
    public MetadataStorageException(string message) : base(message) { }
    /// <summary>Initializes an exception with a message and cause.</summary>
    public MetadataStorageException(string message, Exception innerException) : base(message, innerException) { }
}
