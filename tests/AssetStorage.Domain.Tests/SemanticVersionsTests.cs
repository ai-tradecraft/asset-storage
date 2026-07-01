using AssetStorage.Abstractions;
using AssetStorage.Domain;
using AwesomeAssertions;

namespace AssetStorage.Domain.Tests;

public sealed class SemanticVersionsTests
{
    [Theory]
    [InlineData(VersionBump.Major, 3, 0, 0)]
    [InlineData(VersionBump.Minor, 2, 5, 0)]
    [InlineData(VersionBump.Patch, 2, 4, 2)]
    public void Bump_WhenComponentIsRequested_ThenReturnsExpectedVersion(
        VersionBump bump,
        int major,
        int minor,
        int patch)
    {
        // Arrange
        var current = new AssetVersion(2, 4, 1);

        // Act
        var result = SemanticVersions.Bump(current, bump);

        // Assert
        result.Should().Be(
            new AssetVersion(major, minor, patch),
            "semantic version bumps must reset only the lower-order components");
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("2", 2, null, null)]
    [InlineData("2.3", 2, 3, null)]
    [InlineData("2.3.1", 2, 3, 1)]
    public void ParseSelector_WhenValidSelectorIsProvided_ThenReturnsExpectedComponents(
        string? value,
        int? major,
        int? minor,
        int? patch)
    {
        // Arrange

        // Act
        var result = SemanticVersions.ParseSelector(value);

        // Assert
        result.Should().Be(
            new VersionSelector(major, minor, patch),
            "selectors must support latest, major, major-minor, and exact resolution");
    }

    [Theory]
    [InlineData(".")]
    [InlineData("1.2.3.4")]
    [InlineData("one")]
    [InlineData("-1")]
    public void ParseSelector_WhenSelectorIsInvalid_ThenThrowsValidationException(string value)
    {
        // Arrange
        var action = () => SemanticVersions.ParseSelector(value);

        // Act
        var exception = action.Should().Throw<AssetValidationException>(
            "invalid selectors must be rejected at the domain boundary");

        // Assert
        exception.Which.Message.Should().Contain(
            "Version selector",
            "the validation error should identify the invalid input");
    }
}
