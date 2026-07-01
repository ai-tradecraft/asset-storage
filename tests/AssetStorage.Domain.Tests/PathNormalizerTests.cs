using AssetStorage.Abstractions;
using AssetStorage.Domain;
using AwesomeAssertions;

namespace AssetStorage.Domain.Tests;

public sealed class PathNormalizerTests
{
    [Fact]
    public void Normalize_WhenCaseAndSeparatorsDiffer_ThenReturnsPortableComparisonPath()
    {
        // Arrange
        const string path = "/Team\\Project/ReadMe.md/";

        // Act
        var result = PathNormalizer.Normalize(path, 100);

        // Assert
        result.Should().Be(
            "TEAM/PROJECT/README.MD",
            "logical-path uniqueness must be case-insensitive and separator-neutral");
    }

    [Theory]
    [InlineData("a//b")]
    [InlineData("a/../b")]
    [InlineData("a/./b")]
    public void Normalize_WhenPathContainsUnsafeSegment_ThenThrowsValidationException(string path)
    {
        // Arrange
        var action = () => PathNormalizer.Normalize(path, 100);

        // Act
        var exception = action.Should().Throw<AssetValidationException>(
            "empty, dot, and traversal segments cannot enter the storage model");

        // Assert
        exception.Which.Message.Should().Contain(
            "segment",
            "the error should explain which path rule was violated");
    }

    [Fact]
    public void Normalize_WhenPathExceedsConfiguredLimit_ThenThrowsLimitException()
    {
        // Arrange
        var action = () => PathNormalizer.Normalize("long-path", 4);

        // Act
        var exception = action.Should().Throw<AssetLimitExceededException>(
            "path limits must be enforced before persistence");

        // Assert
        exception.Which.Limit.Should().Be(4, "the configured limit should be reported");
    }
}
