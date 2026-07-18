using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetComparableListings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class GetComparableListingsValidatorTests
{
    private readonly GetComparableListingsValidator _validator =
        new();

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(100)]
    public void Validate_WhenLimitIsOutsideAllowedRange_ReturnsError(
        int limit)
    {
        // Arrange
        var query = new GetComparableListingsQuery
        {
            Limit = limit
        };

        // Act
        string? error =
            _validator.Validate(query);

        // Assert
        error.Should().Be(
            GetComparableListingsValidator.InvalidLimitError);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Validate_WhenLimitIsWithinAllowedRange_ReturnsNoError(
        int limit)
    {
        // Arrange
        var query = new GetComparableListingsQuery
        {
            Limit = limit
        };

        // Act
        string? error =
            _validator.Validate(query);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void Query_DefaultsLanguageAndLimit()
    {
        // Arrange and act
        var query = new GetComparableListingsQuery();

        // Assert
        query.LanguageCode.Should().Be("mk");
        query.Limit.Should().Be(6);
    }
}
