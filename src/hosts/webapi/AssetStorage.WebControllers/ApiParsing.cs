using System.Globalization;
using AssetStorage.Abstractions;

namespace AssetStorage.WebControllers;

/// <summary>Parses API query parameters for versions and bumps.</summary>
internal static class ApiParsing
{
    /// <summary>Parses an exact semantic version string.</summary>
    /// <param name="value">The version string in major.minor.patch format.</param>
    /// <returns>An exact asset version.</returns>
    /// <exception cref="AssetValidationException">Thrown when the version is not in exact format.</exception>
    internal static AssetVersion ParseExactVersion(string value)
    {
        var selector = ParseSelector(value);
        return selector is { Major: int major, Minor: int minor, Patch: int patch }
            ? new(major, minor, patch)
            : throw new AssetValidationException($"Version '{value}' must be exact (major.minor.patch).");
    }

    /// <summary>Parses a version selector from a query parameter.</summary>
    /// <param name="value">The optional version string (major, major.minor, or major.minor.patch).</param>
    /// <returns>A version selector for querying documents.</returns>
    /// <exception cref="AssetValidationException">Thrown when the selector format is invalid.</exception>
    internal static VersionSelector ParseSelector(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new();
        }

        var parts = value.Split('.', StringSplitOptions.TrimEntries);
        if (parts is { Length: < 1 or > 3 })
        {
            throw new AssetValidationException($"Version selector '{value}' is invalid.");
        }

        var values = new int[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out values[index])
                || values[index] < 0)
            {
                throw new AssetValidationException($"Version selector '{value}' is invalid.");
            }
        }

        return new(values[0], values.Length > 1 ? values[1] : null, values.Length > 2 ? values[2] : null);
    }

    /// <summary>Parses a version bump component name.</summary>
    /// <param name="value">The bump string (major, minor, or patch).</param>
    /// <returns>A version bump enum value.</returns>
    /// <exception cref="AssetValidationException">Thrown when the bump value is not recognized.</exception>
    internal static VersionBump ParseBump(string value) =>
        Enum.TryParse<VersionBump>(value, ignoreCase: true, out var bump)
            ? bump
            : throw new AssetValidationException($"Version bump '{value}' must be major, minor, or patch.");
}
