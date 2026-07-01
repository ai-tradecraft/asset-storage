using System.Globalization;
using System.Text;
using AssetStorage.Abstractions;

namespace AssetStorage.Domain;

/// <summary>Normalizes portable logical and folder-relative paths.</summary>
public static class PathNormalizer
{
    /// <summary>Normalizes a team name for comparison and routing.</summary>
    /// <param name="team">The team name to normalize.</param>
    /// <returns>The normalized uppercase team name.</returns>
    /// <exception cref="ArgumentException">Thrown when team is null or whitespace.</exception>
    /// <exception cref="AssetValidationException">Thrown when team contains invalid characters.</exception>
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
    /// <param name="path">The path to normalize.</param>
    /// <param name="maximumLength">The maximum allowed path length.</param>
    /// <returns>The normalized uppercase path with forward slashes.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="AssetLimitExceededException">Thrown when path exceeds maximum length.</exception>
    /// <exception cref="AssetValidationException">Thrown when path contains invalid segments or characters.</exception>
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
