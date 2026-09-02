using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class ListingTests
{
    [Fact]
    public void NewListing_DefaultsToDraft()
    {
        var listing = new Listing();

        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public void Status_HasNoPublicSetter()
    {
        typeof(Listing).GetProperty(nameof(Listing.Status))!
            .SetMethod!
            .IsPrivate.Should().BeTrue();
    }

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
    public void Publish_ShouldChangeStatusToActive_WhenListingIsDraft()
    {
        var listing = CreateListing(ListingStatus.Draft);

        ListingPublicationReadinessResult result = listing.Publish();

        result.IsReady.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public void Publish_ShouldKeepStatusActive_WhenListingIsAlreadyActive()
    {
        var listing = CreateListing(ListingStatus.Active);

        ListingPublicationReadinessResult result = listing.Publish();

        result.IsReady.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Theory]
    [InlineData(ListingStatus.Archived)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public void Publish_ShouldThrow_WhenListingStatusCannotBePublished(ListingStatus status)
    {
        var listing = CreateListing(status);

        var act = () => listing.Publish();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Unpublish_ShouldChangeStatusToDraft_WhenListingIsActive()
    {
        var listing = CreateListing(ListingStatus.Active);

        listing.Unpublish();

        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public void Unpublish_ShouldKeepStatusDraft_WhenListingIsAlreadyDraft()
    {
        var listing = CreateListing(ListingStatus.Draft);

        listing.Unpublish();

        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Theory]
    [InlineData(ListingStatus.Archived)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public void Unpublish_ShouldThrow_WhenListingStatusCannotBeUnpublished(ListingStatus status)
    {
        var listing = CreateListing(status);

        var act = () => listing.Unpublish();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_ShouldChangeStatusToArchived_WhenListingIsDraft()
    {
        var listing = CreateListing(ListingStatus.Draft);

        listing.Archive();

        listing.Status.Should().Be(ListingStatus.Archived);
    }

    [Fact]
    public void Archive_ShouldChangeStatusToArchived_WhenListingIsActive()
    {
        var listing = CreateListing(ListingStatus.Active);

        listing.Archive();

        listing.Status.Should().Be(ListingStatus.Archived);
    }

    [Fact]
    public void Archive_ShouldKeepStatusArchived_WhenListingIsAlreadyArchived()
    {
        var listing = CreateListing(ListingStatus.Archived);

        listing.Archive();

        listing.Status.Should().Be(ListingStatus.Archived);
    }

    [Theory]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public void Archive_ShouldThrow_WhenListingStatusCannotBeArchived(ListingStatus status)
    {
        var listing = CreateListing(status);

        var act = () => listing.Archive();

        act.Should().Throw<InvalidOperationException>();
    }

    private static Listing CreateListing(ListingStatus status)
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();

        switch (status)
        {
            case ListingStatus.Draft:
                break;
            case ListingStatus.Active:
                listing = StrongLocationListingTestFixtures
                    .CreateValidActive();
                break;
            case ListingStatus.Archived:
                listing.Archive();
                break;
            case ListingStatus.Reserved:
            case ListingStatus.Sold:
            case ListingStatus.Rented:
                ListingStatusTestMaterializer.MaterializeUnreachableStatus(
                    listing,
                    status);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return listing;
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
