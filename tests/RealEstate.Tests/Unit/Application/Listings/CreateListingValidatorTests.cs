using FluentAssertions;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class CreateListingValidatorTests
{
    private readonly CreateListingValidator _validator = new();

    [Fact]
    public void Validate_ShouldReturnNull_WhenApartmentRequestIsValid()
    {
        // Arrange
        var request = CreateValidApartmentRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldReturnNull_WhenHouseRequestIsValid()
    {
        // Arrange
        var request = CreateValidHouseRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenPriceIsZero()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Price = 0;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Price must be greater than zero.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenAreaIsZero()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.AreaSquareMeters = 0;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Area must be greater than zero.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenCurrencyIsMissing()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Currency = " ";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Currency is required.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenTranslationsAreEmpty()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Translations.Clear();

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("At least one translation is required.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenTranslationsAreNull()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Translations = null!;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("At least one translation is required.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenTranslationLanguageCodeIsMissing()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Translations[0].LanguageCode = " ";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Translation language code is required.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenTranslationTitleIsMissing()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Translations[0].Title = " ";

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Translation title is required.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenTranslationLanguagesAreDuplicated()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.Translations = new List<CreateListingTranslationRequest>
        {
            CreateTranslation("mk", "Стан во Центар"),
            CreateTranslation(" MK ", "Друг стан")
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Duplicate translation languages are not allowed.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenApartmentDetailsAreMissingForApartment()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.ApartmentDetails = null;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Apartment details are required for apartment listings.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenHouseDetailsAreProvidedForApartment()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.HouseDetails = new CreateListingHouseDetailsRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("House details are not allowed for apartment listings.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenApartmentFloorIsGreaterThanTotalFloors()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.ApartmentDetails!.Floor = 9;
        request.ApartmentDetails.TotalFloors = 8;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Floor cannot be greater than total floors.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenHouseDetailsAreMissingForHouse()
    {
        // Arrange
        var request = CreateValidHouseRequest();
        request.HouseDetails = null;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("House details are required for house listings.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenApartmentDetailsAreProvidedForHouse()
    {
        // Arrange
        var request = CreateValidHouseRequest();
        request.ApartmentDetails = new CreateListingApartmentDetailsRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Apartment details are not allowed for house listings.");
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenYearRenovatedIsEarlierThanYearBuilt()
    {
        // Arrange
        var request = CreateValidApartmentRequest();
        request.YearBuilt = 2020;
        request.YearRenovated = 2019;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Should().Be("Year renovated cannot be earlier than year built.");
    }

    [Fact]
    public void Validate_ShouldReturnNull_WhenBothCoordinatesAreNull()
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Latitude = null;
        request.Longitude = null;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Validate_ShouldReturnNull_WhenCoordinatesAreWithinRange(
    int latitude,
    int longitude)
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Latitude = latitude;
        request.Longitude = longitude;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenOnlyLatitudeIsProvided()
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Latitude = 41.9981m;
        request.Longitude = null;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().Be(
            CreateListingValidator.CoordinatePairError);
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenOnlyLongitudeIsProvided()
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Latitude = null;
        request.Longitude = 21.4254m;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().Be(
            CreateListingValidator.CoordinatePairError);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Validate_ShouldReturnError_WhenLatitudeIsOutsideRange(
    int latitude)
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Latitude = latitude;
        request.Longitude = 21.4254m;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().Be(
            CreateListingValidator.LatitudeOutOfRangeError);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Validate_ShouldReturnError_WhenLongitudeIsOutsideRange(
    int longitude)
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Latitude = 41.9981m;
        request.Longitude = longitude;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().Be(
            CreateListingValidator.LongitudeOutOfRangeError);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    [InlineData(" EuR ")]
    public void Validate_ShouldReturnNull_WhenCurrencyHasThreeAsciiLetters(
    string currency)
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Currency = currency;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("E_R")]
    [InlineData("EÜR")]
    public void Validate_ShouldReturnError_WhenCurrencyIsNotThreeAsciiLetters(
        string currency)
    {
        // Arrange
        CreateListingRequest request =
            CreateValidApartmentRequest();

        request.Currency = currency;

        // Act
        string? result =
            _validator.Validate(request);

        // Assert
        result.Should().Be(
            CreateListingValidator.InvalidCurrencyError);
    }

    private static CreateListingRequest CreateValidApartmentRequest()
    {
        return new CreateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 120_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            Rooms = 3,
            Bathrooms = 1,
            YearBuilt = 2015,
            YearRenovated = 2020,
            BalconyCount = 1,
            ParkingSpaces = 1,
            ApartmentDetails = new CreateListingApartmentDetailsRequest
            {
                ApartmentType = ApartmentType.Standard,
                Floor = 3,
                TotalFloors = 8,
                HasElevator = true
            },
            HouseDetails = null,
            Translations = new List<CreateListingTranslationRequest>
            {
                CreateTranslation("mk", "Стан во Центар")
            }
        };
    }

    private static CreateListingRequest CreateValidHouseRequest()
    {
        return new CreateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.House,
            Price = 180_000m,
            Currency = "EUR",
            AreaSquareMeters = 120m,
            Rooms = 4,
            Bathrooms = 2,
            YearBuilt = 2010,
            YearRenovated = 2020,
            BalconyCount = 1,
            ParkingSpaces = 2,
            ApartmentDetails = null,
            HouseDetails = new CreateListingHouseDetailsRequest
            {
                HouseType = HouseType.Detached,
                NumberOfFloors = 2,
                YardAreaSquareMeters = 350m
            },
            Translations = new List<CreateListingTranslationRequest>
            {
                CreateTranslation("mk", "Куќа во Скопје")
            }
        };
    }

    private static CreateListingTranslationRequest CreateTranslation(
        string languageCode,
        string title)
    {
        return new CreateListingTranslationRequest
        {
            LanguageCode = languageCode,
            Title = title,
            Description = "Description",
            AddressLine = "Address 1",
            City = "Skopje",
            Municipality = "Centar",
            Neighborhood = "Center"
        };
    }
}
