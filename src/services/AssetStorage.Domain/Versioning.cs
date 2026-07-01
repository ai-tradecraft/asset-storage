using AssetStorage.Abstractions;

namespace AssetStorage.Domain;

/// <summary>Provides deterministic semantic-version behavior.</summary>
public static class SemanticVersions
{
    /// <summary>Increments one semantic-version component.</summary>
    public static AssetVersion Bump(AssetVersion current, VersionBump bump) => bump switch
    {
        VersionBump.Major => new(current.Major + 1, 0, 0),
        VersionBump.Minor => new(current.Major, current.Minor + 1, 0),
        VersionBump.Patch => new(current.Major, current.Minor, current.Patch + 1),
        _ => throw new AssetValidationException($"Unsupported version bump '{bump}'.")
    };

    /// <summary>Parses an omitted, major, major-minor, or exact selector.</summary>
    public static VersionSelector ParseSelector(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new();
        }

        var parts = value.Split('.', StringSplitOptions.TrimEntries);
        if (parts is { Length: < 1 or > 3 }
            || parts.Any(part => !int.TryParse(
                part,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var component) || component < 0))
        {
            throw new AssetValidationException(
                $"Version selector '{value}' must contain one to three non-negative integers.");
        }

        return new(
            int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            parts.Length > 1
                ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)
                : null,
            parts.Length > 2
                ? int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)
                : null);
    }
}
