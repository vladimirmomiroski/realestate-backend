using FluentAssertions;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class ListingMappingExtensionsTests
{
    [Fact]
    public void ToResponse_ShouldReturnRequestedTranslation_WhenLanguageExists()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("mk", "МК Наслов"),
            CreateTranslation("en", "EN Title")
        ];

        // Act
        var response = listing.ToResponse("en");

        // Assert
        response.LanguageCode.Should().Be("en");
        response.Title.Should().Be("EN Title");
    }

    [Fact]
    public void ToResponse_ShouldDefaultToMacedonian_WhenLanguageCodeIsEmpty()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("mk", "МК Наслов"),
            CreateTranslation("en", "EN Title")
        ];

        // Act
        var response = listing.ToResponse(" ");

        // Assert
        response.LanguageCode.Should().Be("mk");
        response.Title.Should().Be("МК Наслов");
    }

    [Fact]
    public void ToResponse_ShouldFallbackToMacedonian_WhenRequestedLanguageDoesNotExist()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("en", "EN Title"),
        CreateTranslation("mk", "МК Наслов")
        ];

        // Act
        var response = listing.ToResponse("de");

        // Assert
        response.LanguageCode.Should().Be("mk");
        response.Title.Should().Be("МК Наслов");
    }

    [Fact]
    public void ToResponse_ShouldSelectRequestedTranslationCaseInsensitively()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("mk", "МК Наслов"),
        CreateTranslation("EN", "Stored English Title")
        ];

        // Act
        var response = listing.ToResponse("  en  ");

        // Assert
        response.LanguageCode.Should().Be("EN");
        response.Title.Should().Be("Stored English Title");
    }

    [Fact]
    public void ToResponse_ShouldUsePostgreSqlCOrdering_WhenRequestedAndMacedonianAreMissing()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("\U00010000", "Supplementary Title"),
        CreateTranslation("\uE000", "Private Use Title")
        ];

        // Act
        var response = listing.ToResponse("de");

        // Assert
        response.LanguageCode.Should().Be("\uE000");
        response.Title.Should().Be("Private Use Title");
    }

    [Fact]
    public void ToResponse_ShouldNotDependOnTranslationInsertionOrder()
    {
        // Arrange
        ListingTranslation privateUseTranslation =
            CreateTranslation("\uE000", "Private Use Title");

        ListingTranslation supplementaryTranslation =
            CreateTranslation("\U00010000", "Supplementary Title");

        var firstListing = CreateBaseListing();
        firstListing.Translations =
        [
            supplementaryTranslation,
        privateUseTranslation
        ];

        var secondListing = CreateBaseListing();
        secondListing.Translations =
        [
            CreateTranslation("\uE000", "Private Use Title"),
        CreateTranslation("\U00010000", "Supplementary Title")
        ];

        // Act
        var firstResponse = firstListing.ToResponse("de");
        var secondResponse = secondListing.ToResponse("de");

        // Assert
        firstResponse.LanguageCode.Should().Be("\uE000");
        firstResponse.Title.Should().Be("Private Use Title");

        secondResponse.LanguageCode.Should().Be("\uE000");
        secondResponse.Title.Should().Be("Private Use Title");
    }

    [Fact]
    public void ToPublicResponse_WithPublishableListing_MapsStrictSelectedTranslation()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("mk", "Македонски наслов"),
            new ListingTranslation
            {
                Id = Guid.NewGuid(),
                LanguageCode = "en",
                Title = "Exact public title",
                City = "Exact public city",
                Description = "Exact public description",
                AddressLine = "Exact public address",
                Municipality = "Exact public municipality",
                Neighborhood = "Exact public neighborhood"
            }
        ];

        // Act
        var response = listing.ToPublicResponse("en");

        // Assert
        response.LanguageCode.Should().Be("en");
        response.Title.Should().Be("Exact public title");
        response.City.Should().Be("Exact public city");
        response.Description.Should().Be("Exact public description");
    }

    [Fact]
    public void ToPublicResponse_WithMissingMaterializedTranslation_ThrowsIntegrityFailure()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations = [];

        // Act
        Action act = () => listing.ToPublicResponse("en");

        // Assert
        PublicListingIntegrityException exception = act.Should()
            .Throw<PublicListingIntegrityException>()
            .Which;
        exception.ListingId.Should().Be(listing.Id);
        exception.Violations.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(PublicIdentityCorruption.NullLanguageCode)]
    [InlineData(PublicIdentityCorruption.BlankLanguageCode)]
    [InlineData(PublicIdentityCorruption.NullTitle)]
    [InlineData(PublicIdentityCorruption.BlankTitle)]
    [InlineData(PublicIdentityCorruption.NullCity)]
    [InlineData(PublicIdentityCorruption.BlankCity)]
    [InlineData(PublicIdentityCorruption.NullDescription)]
    [InlineData(PublicIdentityCorruption.BlankDescription)]
    public void ToPublicResponse_WithCorruptActivePublicIdentity_ThrowsIntegrityFailure(
        PublicIdentityCorruption corruption)
    {
        // Arrange
        Listing listing = CreateBaseListing();
        ListingTranslation translation = listing.Translations.Single();
        CorruptPublicIdentity(translation, corruption);

        // Act
        Action act = () => listing.ToPublicResponse("mk");

        // Assert
        PublicListingIntegrityException exception = act.Should()
            .Throw<PublicListingIntegrityException>()
            .Which;
        exception.ListingId.Should().Be(listing.Id);
        exception.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public void ToResponse_ShouldLeaveTranslatedFieldsNull_WhenListingHasNoTranslations()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations = [];

        // Act
        var response = listing.ToResponse("en");

        // Assert
        response.LanguageCode.Should().BeNull();
        response.Title.Should().BeNull();
        response.Description.Should().BeNull();
        response.AddressLine.Should().BeNull();
        response.City.Should().BeNull();
        response.Municipality.Should().BeNull();
        response.Neighborhood.Should().BeNull();
    }

    [Fact]
    public void ToResponse_ShouldMapAgencyId()
    {
        // Arrange
        var listing = CreateBaseListing();
        var agencyId = Guid.NewGuid();

        listing.AssignAgency(agencyId);

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.AgencyId.Should().Be(agencyId);
    }

    [Fact]
    public void ToResponse_ShouldRoundPricePerSquareMeterToTwoDecimals()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Price = 125_000m;
        listing.AreaSquareMeters = 58m;

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.PricePerSquareMeter.Should().Be(2155.17m);
    }

    [Fact]
    public void ToResponse_ShouldMapPrimaryImageUrlFromPrimaryImage()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Images =
        [
            CreateImage(url: "/uploads/second.jpg", sortOrder: 1, isPrimary: false),
            CreateImage(url: "/uploads/primary.jpg", sortOrder: 2, isPrimary: true),
            CreateImage(url: "/uploads/first.jpg", sortOrder: 0, isPrimary: false)
        ];

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.PrimaryImageUrl.Should().Be("/uploads/primary.jpg");
    }

    [Fact]
    public void ToResponse_ShouldUseFirstOrderedImageAsPrimaryImageUrl_WhenNoImageIsPrimary()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Images =
        [
            CreateImage(url: "/uploads/second.jpg", sortOrder: 1, isPrimary: false),
            CreateImage(url: "/uploads/first.jpg", sortOrder: 0, isPrimary: false)
        ];

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.PrimaryImageUrl.Should().Be("/uploads/first.jpg");
    }

    [Fact]
    public void ToResponse_ShouldReturnImagesOrderedBySortOrder()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Images =
        [
            CreateImage(url: "/uploads/third.jpg", sortOrder: 2, isPrimary: false),
            CreateImage(url: "/uploads/first.jpg", sortOrder: 0, isPrimary: true),
            CreateImage(url: "/uploads/second.jpg", sortOrder: 1, isPrimary: false)
        ];

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.Images.Select(image => image.Url).Should().ContainInOrder(
            "/uploads/first.jpg",
            "/uploads/second.jpg",
            "/uploads/third.jpg");
    }

    [Fact]
    public void ToResponse_ShouldMapApartmentDetails_WhenListingHasApartmentDetails()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.PropertyType = PropertyType.Apartment;
        listing.ApartmentDetails = new ListingApartmentDetails
        {
            ApartmentType = ApartmentType.Standard,
            Floor = 4,
            TotalFloors = 8,
            HasElevator = true
        };

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.ApartmentDetails.Should().NotBeNull();
        response.ApartmentDetails!.ApartmentType.Should().Be(ApartmentType.Standard);
        response.ApartmentDetails.Floor.Should().Be(4);
        response.ApartmentDetails.TotalFloors.Should().Be(8);
        response.ApartmentDetails.HasElevator.Should().BeTrue();
        response.HouseDetails.Should().BeNull();
    }

    [Fact]
    public void ToResponse_ShouldMapHouseDetails_WhenListingHasHouseDetails()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.PropertyType = PropertyType.House;
        listing.HouseDetails = new ListingHouseDetails
        {
            HouseType = HouseType.Detached,
            NumberOfFloors = 2,
            YardAreaSquareMeters = 350m
        };

        // Act
        var response = listing.ToResponse("mk");

        // Assert
        response.HouseDetails.Should().NotBeNull();
        response.HouseDetails!.HouseType.Should().Be(HouseType.Detached);
        response.HouseDetails.NumberOfFloors.Should().Be(2);
        response.HouseDetails.YardAreaSquareMeters.Should().Be(350m);
        response.ApartmentDetails.Should().BeNull();
    }

    [Fact]
    public void ToResponse_ShouldMapConfirmedLocationSnapshot()
    {
        Listing listing = CreateBaseListing();

        ListingResponse response = listing.ToResponse("mk");

        response.Latitude.Should().Be(41.9981m);
        response.Longitude.Should().Be(21.4254m);
        response.LocationPrecision.Should().Be(LocationPrecision.ExactAddress);
        response.GeocodedDisplayName.Should().BeNull();
        response.LocationConfirmedAtUtc.Should().Be(
            new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToResponse_ShouldMapUnresolvedLocationSnapshot()
    {
        Listing listing = CreateBaseListing();
        listing.ClearLocation();

        ListingResponse response = listing.ToResponse("mk");

        response.Latitude.Should().BeNull();
        response.Longitude.Should().BeNull();
        response.LocationPrecision.Should().BeNull();
        response.GeocodedDisplayName.Should().BeNull();
        response.LocationConfirmedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ToAuthoringResponse_ShouldMapConfirmedLocationSnapshot()
    {
        Listing listing = CreateBaseListing();

        ListingAuthoringResponse response = listing.ToAuthoringResponse();

        response.Latitude.Should().Be(41.9981m);
        response.Longitude.Should().Be(21.4254m);
        response.LocationPrecision.Should().Be(LocationPrecision.ExactAddress);
        response.GeocodedDisplayName.Should().BeNull();
        response.LocationConfirmedAtUtc.Should().Be(
            new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToAuthoringResponse_ShouldMapUnresolvedLocationSnapshot()
    {
        Listing listing = CreateBaseListing();
        listing.ClearLocation();

        ListingAuthoringResponse response = listing.ToAuthoringResponse();

        response.Latitude.Should().BeNull();
        response.Longitude.Should().BeNull();
        response.LocationPrecision.Should().BeNull();
        response.GeocodedDisplayName.Should().BeNull();
        response.LocationConfirmedAtUtc.Should().BeNull();
    }

    private static Listing CreateBaseListing()
    {
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 120_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            Rooms = 3,
            Bathrooms = 1,
            YearBuilt = 2015,
            BalconyCount = 1,
            ParkingSpaces = 1,
            HasBasement = true,
            IsExchangePossible = false,
            HeatingType = HeatingType.Central,
            FurnishingStatus = FurnishingStatus.Furnished,
            Condition = PropertyCondition.Good,
            YearRenovated = 2020,
            Orientation = Orientation.SouthEast,
            Translations =
            [
                CreateTranslation("mk", "МК Наслов")
            ],
            Images = []
        };

        listing.ConfirmLocation(
            41.9981m,
            21.4254m,
            LocationPrecision.ExactAddress,
            "mapping-test",
            "Opaque:Result/Reference",
            null,
            new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));

        listing.Publish();

        return listing;
    }

    private static ListingTranslation CreateTranslation(string languageCode, string title)
    {
        return new ListingTranslation
        {
            Id = Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = title,
            Description = "Description",
            AddressLine = "Address 1",
            City = "Skopje",
            Municipality = "Centar",
            Neighborhood = "Center"
        };
    }

    private static ListingImage CreateImage(
        string url,
        int sortOrder,
        bool isPrimary)
    {
        return new ListingImage
        {
            Id = Guid.NewGuid(),
            Url = url,
            ContentType = "image/jpeg",
            SizeBytes = 1000,
            SortOrder = sortOrder,
            IsPrimary = isPrimary
        };
    }

    private static void CorruptPublicIdentity(
        ListingTranslation translation,
        PublicIdentityCorruption corruption)
    {
        switch (corruption)
        {
            case PublicIdentityCorruption.NullLanguageCode:
                SetNullForCorruptFixture(
                    translation,
                    nameof(ListingTranslation.LanguageCode));
                break;
            case PublicIdentityCorruption.BlankLanguageCode:
                translation.LanguageCode = " ";
                break;
            case PublicIdentityCorruption.NullTitle:
                SetNullForCorruptFixture(
                    translation,
                    nameof(ListingTranslation.Title));
                break;
            case PublicIdentityCorruption.BlankTitle:
                translation.Title = " ";
                break;
            case PublicIdentityCorruption.NullCity:
                translation.City = null;
                break;
            case PublicIdentityCorruption.BlankCity:
                translation.City = " ";
                break;
            case PublicIdentityCorruption.NullDescription:
                translation.Description = null;
                break;
            case PublicIdentityCorruption.BlankDescription:
                translation.Description = " ";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(corruption),
                    corruption,
                    null);
        }
    }

    private static void SetNullForCorruptFixture(
        ListingTranslation translation,
        string propertyName)
    {
        System.Reflection.PropertyInfo property =
            typeof(ListingTranslation).GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"ListingTranslation.{propertyName} was not found.");

        property.SetValue(translation, null);
    }

    public enum PublicIdentityCorruption
    {
        NullLanguageCode,
        BlankLanguageCode,
        NullTitle,
        BlankTitle,
        NullCity,
        BlankCity,
        NullDescription,
        BlankDescription
    }
}
