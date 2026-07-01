using System.Globalization;
using System.Text;
using AssetStorage.Abstractions;

namespace AssetStorage.Domain;

/// <summary>Normalizes portable logical and folder-relative paths.</summary>
public static class PathNormalizer
{
    /// <summary>Normalizes a team name for comparison and routing.</summary>
    public static string NormalizeTeam(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        var normalized = team.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        if (normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains('\\', StringComparison.Ordinal)
            || normalized is "." or "..")
        {
            throw new AssetValidationException($"Team name '{team}' is not a valid single path segment.");
        }

        return normalized;
    }

    /// <summary>Normalizes a logical or folder-relative path.</summary>
    public static string Normalize(string path, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var display = path.Replace('\\', '/').Trim('/');
        if (display.Length > maximumLength)
        {
            throw new AssetLimitExceededException("path length", maximumLength);
        }

        var segments = display.Split('/');
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new AssetValidationException($"Path '{path}' contains an empty, dot, or traversal segment.");
        }

        if (display.Contains('\0', StringComparison.Ordinal))
        {
            throw new AssetValidationException("Paths cannot contain NUL characters.");
        }

        return string.Join('/', segments)
            .Normalize(NormalizationForm.FormC)
            .ToUpper(CultureInfo.InvariantCulture);
    }
}
