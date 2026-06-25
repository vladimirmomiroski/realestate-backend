using FluentAssertions;
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
    public void ToResponse_ShouldFallbackToFirstTranslation_WhenRequestedLanguageDoesNotExist()
    {
        // Arrange
        var listing = CreateBaseListing();
        listing.Translations =
        [
            CreateTranslation("mk", "МК Наслов"),
            CreateTranslation("en", "EN Title")
        ];

        // Act
        var response = listing.ToResponse("de");

        // Assert
        response.LanguageCode.Should().Be("mk");
        response.Title.Should().Be("МК Наслов");
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

    private static Listing CreateBaseListing()
    {
        return new Listing
        {
            Id = Guid.NewGuid(),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
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
            Latitude = 41.9981m,
            Longitude = 21.4254m,
            Translations =
            [
                CreateTranslation("mk", "МК Наслов")
            ],
            Images = []
        };
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
}
