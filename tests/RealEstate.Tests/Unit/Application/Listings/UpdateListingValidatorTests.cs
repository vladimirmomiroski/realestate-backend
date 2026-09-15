using FluentAssertions;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class UpdateListingValidatorTests
{
    private readonly UpdateListingValidator _validator = new();

    public static TheoryData<string> BoundaryWhitespaceCases => new()
    {
        " ",
        "\t",
        "\r\n",
        "\u00A0",
        "\u2003",
        "\u3000"
    };

    public static TheoryData<PropertyType> SupportedPropertyTypes => new()
    {
        PropertyType.Apartment,
        PropertyType.House,
        PropertyType.Commercial,
        PropertyType.Land
    };

    public static TheoryData<PropertyType, PropertyType>
        NonMatchingDetailCases => new()
        {
            { PropertyType.Apartment, PropertyType.House },
            { PropertyType.Apartment, PropertyType.Commercial },
            { PropertyType.Apartment, PropertyType.Land },
            { PropertyType.House, PropertyType.Apartment },
            { PropertyType.House, PropertyType.Commercial },
            { PropertyType.House, PropertyType.Land },
            { PropertyType.Commercial, PropertyType.Apartment },
            { PropertyType.Commercial, PropertyType.House },
            { PropertyType.Commercial, PropertyType.Land },
            { PropertyType.Land, PropertyType.Apartment },
            { PropertyType.Land, PropertyType.House },
            { PropertyType.Land, PropertyType.Commercial }
        };

    [Fact]
    public void ValidateWithKey_AcceptsCompleteApartmentReplacement()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Fact]
    public void ValidateWithKey_AcceptsCompleteHouseReplacement()
    {
        UpdateListingRequest request = CreateValidHouseRequest();

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(SupportedPropertyTypes))]
    public void ValidateWithKey_AcceptsExactFourTypeReplacement(
        PropertyType propertyType)
    {
        UpdateListingRequest request = CreateValidRequest(propertyType);

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Fact]
    public void UpdateContract_DoesNotExposeCoordinateAuthoring()
    {
        typeof(UpdateListingRequest).GetProperties()
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

        typeof(UpdateListingValidator).GetFields()
            .Select(field => field.Name)
            .Should().NotContain([
                "CoordinatePairError",
                "LatitudeOutOfRangeError",
                "LongitudeOutOfRangeError"
            ]);
    }

    [Fact]
    public void ValidateWithKey_AcceptsCurrentListingTypesAndDefinedOptionalEnums()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.ListingType = ListingType.Rent;
        request.HeatingType = HeatingType.Gas;
        request.FurnishingStatus = FurnishingStatus.Furnished;
        request.Condition = PropertyCondition.Renovated;
        request.Orientation = Orientation.SouthEast;
        request.ApartmentDetails!.ApartmentType = ApartmentType.Penthouse;

        _validator.ValidateWithKey(request).Should().BeNull();
        request.HeatingType.Should().Be(HeatingType.Gas);
        request.FurnishingStatus.Should().Be(FurnishingStatus.Furnished);
        request.Condition.Should().Be(PropertyCondition.Renovated);
        request.Orientation.Should().Be(Orientation.SouthEast);
        request.ApartmentDetails.ApartmentType.Should().Be(ApartmentType.Penthouse);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void ValidateWithKey_RejectsDefaultOrUndefinedListingType(int value)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.ListingType = (ListingType)value;

        AssertFailure(request, "listingType");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void ValidateWithKey_RejectsUnsupportedPropertyTypeFirst(int value)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.PropertyType = (PropertyType)value;
        request.Price = 0;
        request.HouseDetails = new UpdateListingHouseDetailsRequest
        {
            HouseType = HouseType.Detached
        };

        UpdateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("propertyType");
        failure.Error.Should().Be(
            UpdateListingValidator.InvalidPropertyTypeError);
    }

    [Fact]
    public void ValidateWithKey_RejectsEveryUndefinedOptionalEnum()
    {
        (string Key, Action<UpdateListingRequest> MakeInvalid)[] cases =
        [
            ("heatingType", request => request.HeatingType = (HeatingType)999),
            ("furnishingStatus", request =>
                request.FurnishingStatus = (FurnishingStatus)999),
            ("condition", request => request.Condition = (PropertyCondition)999),
            ("orientation", request => request.Orientation = (Orientation)999),
            ("apartmentDetails.apartmentType", request =>
                request.ApartmentDetails!.ApartmentType = (ApartmentType)999)
        ];

        foreach ((string key, Action<UpdateListingRequest> makeInvalid) in cases)
        {
            UpdateListingRequest request = CreateValidApartmentRequest();
            makeInvalid(request);

            AssertFailure(request, key);
        }

        UpdateListingRequest houseRequest = CreateValidHouseRequest();
        houseRequest.HouseDetails!.HouseType = (HouseType)999;
        AssertFailure(houseRequest, "houseDetails.houseType");

        UpdateListingRequest commercialRequest =
            CreateValidRequest(PropertyType.Commercial);
        commercialRequest.CommercialDetails!.CommercialType =
            (CommercialType)999;
        AssertFailure(
            commercialRequest,
            "commercialDetails.commercialType");

        UpdateListingRequest landRequest = CreateValidRequest(PropertyType.Land);
        landRequest.LandDetails!.LandType = (LandType)999;
        AssertFailure(landRequest, "landDetails.landType");
    }

    [Fact]
    public void ValidateWithKey_AcceptsUnknownOptionalEnumDefaults()
    {
        UpdateListingRequest apartment = CreateValidApartmentRequest();
        apartment.HeatingType = HeatingType.Unknown;
        apartment.FurnishingStatus = FurnishingStatus.Unknown;
        apartment.Condition = PropertyCondition.Unknown;
        apartment.Orientation = Orientation.Unknown;
        apartment.ApartmentDetails!.ApartmentType = ApartmentType.Unknown;

        UpdateListingRequest house = CreateValidHouseRequest();
        house.HouseDetails!.HouseType = HouseType.Unknown;

        UpdateListingRequest commercial =
            CreateValidRequest(PropertyType.Commercial);
        commercial.CommercialDetails!.CommercialType = CommercialType.Unknown;

        UpdateListingRequest land = CreateValidRequest(PropertyType.Land);
        land.LandDetails!.LandType = LandType.Unknown;

        _validator.ValidateWithKey(apartment).Should().BeNull();
        _validator.ValidateWithKey(house).Should().BeNull();
        _validator.ValidateWithKey(commercial).Should().BeNull();
        _validator.ValidateWithKey(land).Should().BeNull();
    }

    [Theory]
    [InlineData("price")]
    [InlineData("areaSquareMeters")]
    [InlineData("balconyCount")]
    [InlineData("parkingSpaces")]
    [InlineData("yearRenovated")]
    public void ValidateWithKey_RejectsExistingInvalidRootScalarBoundaries(
        string field)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();

        switch (field)
        {
            case "price":
                request.Price = 0;
                break;
            case "areaSquareMeters":
                request.AreaSquareMeters = 0;
                break;
            case "balconyCount":
                request.BalconyCount = -1;
                break;
            case "parkingSpaces":
                request.ParkingSpaces = -1;
                break;
            case "yearRenovated":
                request.YearRenovated = 1799;
                break;
        }

        AssertFailure(request, field);
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("EÜR")]
    public void ValidateWithKey_RejectsInvalidCurrency(string currency)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Currency = currency;

        AssertFailure(request, "currency");
    }

    [Fact]
    public void ValidateWithKey_RejectsRenovationOrdering()
    {
        UpdateListingRequest renovationRequest = CreateValidApartmentRequest();
        renovationRequest.YearBuilt = 2020;
        renovationRequest.YearRenovated = 2019;
        AssertFailure(renovationRequest, "request");
    }

    [Fact]
    public void ValidateWithKey_RejectsEmptyTranslations()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations.Clear();

        AssertFailure(request, "translations");
    }

    [Fact]
    public void ValidateWithKey_RejectsNullTranslations()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations = null!;

        AssertFailure(request, "translations");
    }

    [Fact]
    public void ValidateWithKey_RejectsDuplicateLanguagesAfterNormalization()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations =
        [
            CreateTranslation("EN", "First"),
            CreateTranslation(" en ", "Second")
        ];

        AssertFailure(request, "translations");
    }

    [Theory]
    [InlineData("e")]
    [InlineData("engl")]
    [InlineData("en_")]
    [InlineData("en-")]
    [InlineData("en-u")]
    [InlineData("ÐµÐ½")]
    public void ValidateWithKey_RejectsMalformedLanguageCode(string languageCode)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations[0].LanguageCode = languageCode;

        AssertFailure(request, "translations[0].languageCode");
    }

    [Fact]
    public void ValidateWithKey_RejectsLanguageOverMaximum()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations[0].LanguageCode = "abc-de23456";

        AssertFailure(request, "translations[0].languageCode");
    }

    [Fact]
    public void ValidateWithKey_RejectsBlankLanguageAfterBoundaryTrim()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations[0].LanguageCode = "\t\u00A0\u2003\r\n";

        AssertFailure(request, "translations[0].languageCode");
    }

    [Theory]
    [MemberData(nameof(BoundaryWhitespaceCases))]
    public void Normalize_UsesApprovedBoundaryWhitespaceAndCanonicalLanguage(
        string whitespace)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        UpdateListingTranslationRequest translation = request.Translations[0];
        translation.LanguageCode = $"{whitespace}EN-US{whitespace}";
        translation.Title = $"{whitespace}Title{whitespace}";
        translation.Description = $"{whitespace}Description{whitespace}";
        translation.AddressLine = $"{whitespace}Address{whitespace}";
        translation.City = $"{whitespace}City{whitespace}";
        translation.Municipality = $"{whitespace}Municipality{whitespace}";
        translation.Neighborhood = $"{whitespace}Neighborhood{whitespace}";
        request.Currency = " eur ";

        _validator.ValidateWithKey(request).Should().BeNull();

        UpdateListingRequest normalized = _validator.Normalize(request);

        normalized.Should().BeSameAs(request);
        request.Currency.Should().Be("EUR");
        translation.LanguageCode.Should().Be("en-us");
        translation.Title.Should().Be("Title");
        translation.Description.Should().Be("Description");
        translation.AddressLine.Should().Be("Address");
        translation.City.Should().Be("City");
        translation.Municipality.Should().Be("Municipality");
        translation.Neighborhood.Should().Be("Neighborhood");
    }

    [Fact]
    public void Normalize_ConvertsOptionalWhitespaceOnlyTranslationFieldsToNull()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        UpdateListingTranslationRequest translation = request.Translations[0];
        const string whitespace = "\t\r\n\u00A0\u2003\u3000";
        translation.Description = whitespace;
        translation.AddressLine = whitespace;
        translation.City = whitespace;
        translation.Municipality = whitespace;
        translation.Neighborhood = whitespace;

        _validator.ValidateWithKey(request).Should().BeNull();
        _validator.Normalize(request);

        translation.Description.Should().BeNull();
        translation.AddressLine.Should().BeNull();
        translation.City.Should().BeNull();
        translation.Municipality.Should().BeNull();
        translation.Neighborhood.Should().BeNull();
    }

    [Fact]
    public void ValidateWithKey_AcceptsEveryTranslationFieldAtNormalizedMaximum()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        UpdateListingTranslationRequest translation = request.Translations[0];
        translation.LanguageCode = "abc-de2345";
        translation.Title = new string('a', ListingTranslationRules.TitleMaxLength);
        translation.Description = new string(
            'a',
            ListingTranslationRules.DescriptionMaxLength);
        translation.AddressLine = new string(
            'a',
            ListingTranslationRules.AddressLineMaxLength);
        translation.City = new string('a', ListingTranslationRules.LocationMaxLength);
        translation.Municipality = new string(
            'a',
            ListingTranslationRules.LocationMaxLength);
        translation.Neighborhood = new string(
            'a',
            ListingTranslationRules.LocationMaxLength);

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Theory]
    [InlineData("title", ListingTranslationRules.TitleMaxLength)]
    [InlineData("description", ListingTranslationRules.DescriptionMaxLength)]
    [InlineData("addressLine", ListingTranslationRules.AddressLineMaxLength)]
    [InlineData("city", ListingTranslationRules.LocationMaxLength)]
    [InlineData("municipality", ListingTranslationRules.LocationMaxLength)]
    [InlineData("neighborhood", ListingTranslationRules.LocationMaxLength)]
    public void ValidateWithKey_RejectsTranslationFieldOverMaximum(
        string field,
        int maximumLength)
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        UpdateListingTranslationRequest translation = request.Translations[0];
        string value = new('a', maximumLength + 1);

        switch (field)
        {
            case "title": translation.Title = value; break;
            case "description": translation.Description = value; break;
            case "addressLine": translation.AddressLine = value; break;
            case "city": translation.City = value; break;
            case "municipality": translation.Municipality = value; break;
            case "neighborhood": translation.Neighborhood = value; break;
        }

        AssertFailure(request, $"translations[0].{field}");
    }

    [Fact]
    public void ValidateWithKey_RejectsBlankTitleAfterBoundaryTrim()
    {
        UpdateListingRequest request = CreateValidApartmentRequest();
        request.Translations[0].Title = "\t\u00A0\u2003\r\n";

        AssertFailure(request, "translations[0].title");
    }

    [Fact]
    public void ValidateWithKey_RequiresExactlyMatchingApartmentDetails()
    {
        UpdateListingRequest missing = CreateValidApartmentRequest();
        missing.ApartmentDetails = null;
        AssertFailure(missing, "apartmentDetails");

        UpdateListingRequest conflicting = CreateValidApartmentRequest();
        conflicting.HouseDetails = new UpdateListingHouseDetailsRequest();
        AssertFailure(conflicting, "request");
    }

    [Fact]
    public void ValidateWithKey_RequiresExactlyMatchingHouseDetails()
    {
        UpdateListingRequest missing = CreateValidHouseRequest();
        missing.HouseDetails = null;
        AssertFailure(missing, "houseDetails");

        UpdateListingRequest conflicting = CreateValidHouseRequest();
        conflicting.ApartmentDetails = new UpdateListingApartmentDetailsRequest();
        AssertFailure(conflicting, "request");
    }

    [Theory]
    [MemberData(nameof(SupportedPropertyTypes))]
    public void ValidateWithKey_MissingMatchingDetailsUsesTypeSpecificKeyFirst(
        PropertyType propertyType)
    {
        UpdateListingRequest request = CreateValidRequest(propertyType);
        ClearDetails(request, propertyType);
        SetDetails(
            request,
            propertyType == PropertyType.Apartment
                ? PropertyType.House
                : PropertyType.Apartment);

        UpdateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(propertyType switch
        {
            PropertyType.Apartment => "apartmentDetails",
            PropertyType.House => "houseDetails",
            PropertyType.Commercial => "commercialDetails",
            PropertyType.Land => "landDetails",
            _ => throw new ArgumentOutOfRangeException(nameof(propertyType))
        });
    }

    [Theory]
    [MemberData(nameof(NonMatchingDetailCases))]
    public void ValidateWithKey_RejectsEveryNonMatchingDetailCombination(
        PropertyType propertyType,
        PropertyType contradictoryDetailType)
    {
        UpdateListingRequest request = CreateValidRequest(propertyType);
        SetDetails(request, contradictoryDetailType);

        AssertFailure(request, "request");
    }

    [Fact]
    public void ValidateWithKey_RejectsExistingInvalidSubtypeScalarBoundaries()
    {
        UpdateListingRequest floor = CreateValidApartmentRequest();
        floor.ApartmentDetails!.Floor = -1;
        AssertFailure(floor, "apartmentDetails.floor");

        UpdateListingRequest floorOrder = CreateValidApartmentRequest();
        floorOrder.ApartmentDetails!.Floor = 9;
        floorOrder.ApartmentDetails.TotalFloors = 8;
        AssertFailure(floorOrder, "request");

        UpdateListingRequest floors = CreateValidHouseRequest();
        floors.HouseDetails!.NumberOfFloors = -1;
        AssertFailure(floors, "houseDetails.numberOfFloors");

        UpdateListingRequest yard = CreateValidHouseRequest();
        yard.HouseDetails!.YardAreaSquareMeters = -1;
        AssertFailure(yard, "houseDetails.yardAreaSquareMeters");
    }

    private void AssertFailure(UpdateListingRequest request, string expectedKey)
    {
        UpdateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(expectedKey);
    }

    private static UpdateListingRequest CreateValidApartmentRequest()
    {
        return new UpdateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 120_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            Rooms = 3,
            Bathrooms = 1,
            BalconyCount = 1,
            ParkingSpaces = 1,
            YearBuilt = 2015,
            YearRenovated = 2020,
            ApartmentDetails = new UpdateListingApartmentDetailsRequest
            {
                ApartmentType = ApartmentType.Standard,
                Floor = 3,
                TotalFloors = 8,
                HasElevator = true
            },
            Translations =
            [
                CreateTranslation("en", "Valid title")
            ]
        };
    }

    private static UpdateListingRequest CreateValidHouseRequest()
    {
        return new UpdateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.House,
            Price = 180_000m,
            Currency = "EUR",
            AreaSquareMeters = 120m,
            Rooms = 4,
            Bathrooms = 2,
            BalconyCount = 1,
            ParkingSpaces = 2,
            YearBuilt = 2010,
            YearRenovated = 2020,
            HouseDetails = new UpdateListingHouseDetailsRequest
            {
                HouseType = HouseType.Detached,
                NumberOfFloors = 2,
                YardAreaSquareMeters = 350m
            },
            Translations =
            [
                CreateTranslation("en", "Valid house")
            ]
        };
    }

    private static UpdateListingRequest CreateValidRequest(
        PropertyType propertyType)
    {
        return propertyType switch
        {
            PropertyType.Apartment => CreateValidApartmentRequest(),
            PropertyType.House => CreateValidHouseRequest(),
            PropertyType.Commercial => new UpdateListingRequest
            {
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Commercial,
                Price = 140_000m,
                Currency = "EUR",
                AreaSquareMeters = 80m,
                CommercialDetails = new UpdateListingCommercialDetailsRequest
                {
                    CommercialType = CommercialType.Office
                },
                Translations = [CreateTranslation("en", "Valid commercial")]
            },
            PropertyType.Land => new UpdateListingRequest
            {
                ListingType = ListingType.Sale,
                PropertyType = PropertyType.Land,
                Price = 90_000m,
                Currency = "EUR",
                AreaSquareMeters = 600m,
                LandDetails = new UpdateListingLandDetailsRequest
                {
                    LandType = LandType.BuildingPlot
                },
                Translations = [CreateTranslation("en", "Valid land")]
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(propertyType),
                propertyType,
                "Unsupported test property type.")
        };
    }

    private static void ClearDetails(
        UpdateListingRequest request,
        PropertyType propertyType)
    {
        switch (propertyType)
        {
            case PropertyType.Apartment:
                request.ApartmentDetails = null;
                break;
            case PropertyType.House:
                request.HouseDetails = null;
                break;
            case PropertyType.Commercial:
                request.CommercialDetails = null;
                break;
            case PropertyType.Land:
                request.LandDetails = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyType));
        }
    }

    private static void SetDetails(
        UpdateListingRequest request,
        PropertyType propertyType)
    {
        switch (propertyType)
        {
            case PropertyType.Apartment:
                request.ApartmentDetails =
                    new UpdateListingApartmentDetailsRequest();
                break;
            case PropertyType.House:
                request.HouseDetails = new UpdateListingHouseDetailsRequest();
                break;
            case PropertyType.Commercial:
                request.CommercialDetails =
                    new UpdateListingCommercialDetailsRequest();
                break;
            case PropertyType.Land:
                request.LandDetails = new UpdateListingLandDetailsRequest();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyType));
        }
    }

    private static UpdateListingTranslationRequest CreateTranslation(
        string languageCode,
        string title)
    {
        return new UpdateListingTranslationRequest
        {
            LanguageCode = languageCode,
            Title = title,
            Description = "Description",
            AddressLine = "Address",
            City = "Skopje",
            Municipality = "Centar",
            Neighborhood = "Center"
        };
    }
}
