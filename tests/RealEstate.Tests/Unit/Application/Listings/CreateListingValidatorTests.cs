using FluentAssertions;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class CreateListingValidatorTests
{
    private readonly CreateListingValidator _validator = new();

    public static TheoryData<string, int> DefinedCategoricalCases => new()
    {
        { "heatingType", (int)HeatingType.Central },
        { "furnishingStatus", (int)FurnishingStatus.Furnished },
        { "condition", (int)PropertyCondition.Good },
        { "orientation", (int)Orientation.SouthEast },
        { "apartmentDetails.apartmentType", (int)ApartmentType.Standard },
        { "houseDetails.houseType", (int)HouseType.Detached }
    };

    public static TheoryData<string> UnknownCategoricalCases => new()
    {
        "heatingType",
        "furnishingStatus",
        "condition",
        "orientation",
        "apartmentDetails.apartmentType",
        "houseDetails.houseType"
    };

    public static TheoryData<string, string> UndefinedCategoricalCases => new()
    {
        { "heatingType", "Heating type must be a defined value." },
        { "furnishingStatus", "Furnishing status must be a defined value." },
        { "condition", "Property condition must be a defined value." },
        { "orientation", "Orientation must be a defined value." },
        {
            "apartmentDetails.apartmentType",
            "Apartment type must be a defined value."
        },
        { "houseDetails.houseType", "House type must be a defined value." }
    };

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

    [Theory]
    [MemberData(nameof(DefinedCategoricalCases))]
    public void ValidateWithKey_ShouldAcceptDefinedCategoricalValue(
        string field,
        int value)
    {
        CreateListingRequest request = CreateRequestForCategoricalField(field);
        SetCategoricalValue(request, field, value);

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(UnknownCategoricalCases))]
    public void ValidateWithKey_ShouldAcceptDefinedUnknownCategoricalValue(
        string field)
    {
        CreateListingRequest request = CreateRequestForCategoricalField(field);
        SetCategoricalValue(request, field, 0);

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(UndefinedCategoricalCases))]
    public void ValidateWithKey_ShouldRejectUndefinedCategoricalValue(
        string field,
        string expectedError)
    {
        CreateListingRequest request = CreateRequestForCategoricalField(field);
        SetCategoricalValue(request, field, 999);

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(field);
        failure.Error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void ValidateWithKey_ShouldRejectUnsupportedPropertyTypeFirst(int value)
    {
        CreateListingRequest request = CreateValidApartmentRequest();
        request.PropertyType = (PropertyType)value;
        request.Price = 0;
        request.HouseDetails = new CreateListingHouseDetailsRequest
        {
            HouseType = HouseType.Detached
        };

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("propertyType");
        failure.Error.Should().Be(
            CreateListingValidator.InvalidPropertyTypeError);
    }

    [Fact]
    public void CreateContract_DoesNotExposeCoordinateAuthoring()
    {
        typeof(CreateListingRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(
            [
                "Latitude",
                "Longitude",
                "LocationPrecision",
                "GeocodingProviderKey",
                "GeocodingResultReference",
                "GeocodedDisplayName",
                "LocationConfirmedAtUtc"
            ]);

        typeof(CreateListingValidator).GetFields()
            .Select(field => field.Name)
            .Should().NotContain([
                "CoordinatePairError",
                "LatitudeOutOfRangeError",
                "LongitudeOutOfRangeError"
            ]);
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

    private static CreateListingRequest CreateRequestForCategoricalField(
        string field)
    {
        return field == "houseDetails.houseType"
            ? CreateValidHouseRequest()
            : CreateValidApartmentRequest();
    }

    private static void SetCategoricalValue(
        CreateListingRequest request,
        string field,
        int value)
    {
        switch (field)
        {
            case "heatingType":
                request.HeatingType = (HeatingType)value;
                break;
            case "furnishingStatus":
                request.FurnishingStatus = (FurnishingStatus)value;
                break;
            case "condition":
                request.Condition = (PropertyCondition)value;
                break;
            case "orientation":
                request.Orientation = (Orientation)value;
                break;
            case "apartmentDetails.apartmentType":
                request.ApartmentDetails!.ApartmentType =
                    (ApartmentType)value;
                break;
            case "houseDetails.houseType":
                request.HouseDetails!.HouseType = (HouseType)value;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Unsupported categorical field.");
        }
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
