using FluentAssertions;
using RealEstate.Domain.Entities;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class ListingTests
{
    [Fact]
    public void AssignCreator_ShouldSetCreatedByUserId_WhenUserIdIsValid()
    {
        // Arrange
        var listing = new Listing();
        var userId = Guid.NewGuid();

        // Act
        listing.AssignCreator(userId);

        // Assert
        listing.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void AssignCreator_ShouldThrowArgumentException_WhenUserIdIsEmpty()
    {
        // Arrange
        var listing = new Listing();

        // Act
        var act = () => listing.AssignCreator(Guid.Empty);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void AssignAgency_ShouldSetAgencyId_WhenAgencyIdIsValid()
    {
        // Arrange
        var listing = new Listing();
        var agencyId = Guid.NewGuid();

        // Act
        listing.AssignAgency(agencyId);

        // Assert
        listing.AgencyId.Should().Be(agencyId);
    }

    [Fact]
    public void AssignAgency_ShouldThrowArgumentException_WhenAgencyIdIsEmpty()
    {
        // Arrange
        var listing = new Listing();

        // Act
        var act = () => listing.AssignAgency(Guid.Empty);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("agencyId");
    }

    [Fact]
    public void CalculatePricePerSquareMeter_ShouldReturnPriceDividedByArea()
    {
        // Arrange
        var listing = new Listing
        {
            Price = 120_000m,
            AreaSquareMeters = 60m
        };

        // Act
        var result = listing.CalculatePricePerSquareMeter();

        // Assert
        result.Should().Be(2_000m);
    }

    [Fact]
    public void CalculatePricePerSquareMeter_ShouldReturnZero_WhenAreaIsZero()
    {
        // Arrange
        var listing = new Listing
        {
            Price = 120_000m,
            AreaSquareMeters = 0m
        };

        // Act
        var result = listing.CalculatePricePerSquareMeter();

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculatePricePerSquareMeter_ShouldReturnZero_WhenAreaIsNegative()
    {
        // Arrange
        var listing = new Listing
        {
            Price = 120_000m,
            AreaSquareMeters = -60m
        };

        // Act
        var result = listing.CalculatePricePerSquareMeter();

        // Assert
        result.Should().Be(0m);
    }
}
