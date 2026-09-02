using FluentAssertions;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class CreateListingTranslationValidationTests
{
    private readonly CreateListingValidator _validator = new();

    [Theory]
    [InlineData(ListingType.Sale)]
    [InlineData(ListingType.Rent)]
    public void ValidateWithKey_AcceptsCurrentListingTypes(
        ListingType listingType)
    {
        CreateListingRequest request = CreateValidRequest();
        request.ListingType = listingType;

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void ValidateWithKey_RejectsDefaultOrUndefinedListingType(
        int listingType)
    {
        CreateListingRequest request = CreateValidRequest();
        request.ListingType = (ListingType)listingType;

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("listingType");
        failure.Error.Should().Be(
            CreateListingValidator.InvalidListingTypeError);
    }

    [Theory]
    [InlineData(PropertyType.Apartment)]
    [InlineData(PropertyType.House)]
    public void ValidateWithKey_AcceptsCurrentPropertyTypes(
        PropertyType propertyType)
    {
        CreateListingRequest request = CreateValidRequest(propertyType);

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void ValidateWithKey_RejectsDefaultOrUndefinedPropertyType(
        int propertyType)
    {
        CreateListingRequest request = CreateValidRequest();
        request.PropertyType = (PropertyType)propertyType;

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("propertyType");
        failure.Error.Should().Be(
            CreateListingValidator.InvalidPropertyTypeError);
    }

    [Fact]
    public void ValidateWithKey_AcceptsCanonicalizableLanguageCode()
    {
        CreateListingRequest request = CreateValidRequest();
        request.Translations[0].LanguageCode = "\u00A0EN-US\u3000";

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Fact]
    public void ValidateWithKey_AcceptsTranslationFieldsAtNormalizedMaximums()
    {
        CreateListingRequest request = CreateValidRequest();
        CreateListingTranslationRequest translation = request.Translations[0];
        translation.LanguageCode = "abc-de2345";
        translation.Title = new string(
            'a',
            ListingTranslationRules.TitleMaxLength);
        translation.Description = new string(
            'a',
            ListingTranslationRules.DescriptionMaxLength);
        translation.AddressLine = new string(
            'a',
            ListingTranslationRules.AddressLineMaxLength);
        translation.City = new string(
            'a',
            ListingTranslationRules.LocationMaxLength);
        translation.Municipality = new string(
            'a',
            ListingTranslationRules.LocationMaxLength);
        translation.Neighborhood = new string(
            'a',
            ListingTranslationRules.LocationMaxLength);

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Theory]
    [InlineData("e")]
    [InlineData("engl")]
    [InlineData("en_")]
    [InlineData("en-")]
    [InlineData("en-u")]
    [InlineData("ен")]
    public void ValidateWithKey_RejectsInvalidLanguageGrammar(
        string languageCode)
    {
        CreateListingRequest request = CreateValidRequest();
        request.Translations[0].LanguageCode = languageCode;

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("translations[0].languageCode");
        failure.Error.Should().Be(
            CreateListingValidator.InvalidLanguageCodeError);
    }

    [Fact]
    public void ValidateWithKey_RejectsLanguageOverNormalizedMaximum()
    {
        CreateListingRequest request = CreateValidRequest();
        request.Translations[0].LanguageCode = "abc-de23456";

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("translations[0].languageCode");
        failure.Error.Should().Contain(
            ListingTranslationRules.LanguageCodeMaxLength.ToString());
    }

    [Fact]
    public void ValidateWithKey_RejectsBlankTitleAfterBoundaryTrim()
    {
        CreateListingRequest request = CreateValidRequest();
        request.Translations[0].Title = "\t\u00A0\u2003\r\n";

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("translations[0].title");
        failure.Error.Should().Be("Translation title is required.");
    }

    [Fact]
    public void ValidateWithKey_RejectsTitleOverNormalizedMaximum()
    {
        CreateListingRequest request = CreateValidRequest();
        request.Translations[0].Title =
            new string('a', ListingTranslationRules.TitleMaxLength + 1);

        AssertTranslationFailure(request, "translations[0].title");
    }

    [Theory]
    [InlineData("description", ListingTranslationRules.DescriptionMaxLength)]
    [InlineData("addressLine", ListingTranslationRules.AddressLineMaxLength)]
    [InlineData("city", ListingTranslationRules.LocationMaxLength)]
    [InlineData("municipality", ListingTranslationRules.LocationMaxLength)]
    [InlineData("neighborhood", ListingTranslationRules.LocationMaxLength)]
    public void ValidateWithKey_RejectsOptionalTranslationFieldOverNormalizedMaximum(
        string field,
        int maximumLength)
    {
        CreateListingRequest request = CreateValidRequest();
        CreateListingTranslationRequest translation = request.Translations[0];
        string value = new('a', maximumLength + 1);

        switch (field)
        {
            case "description":
                translation.Description = value;
                break;
            case "addressLine":
                translation.AddressLine = value;
                break;
            case "city":
                translation.City = value;
                break;
            case "municipality":
                translation.Municipality = value;
                break;
            case "neighborhood":
                translation.Neighborhood = value;
                break;
        }

        AssertTranslationFailure(request, $"translations[0].{field}");
    }

    [Fact]
    public void ValidateWithKey_AcceptsOptionalWhitespaceOnlyTranslationFields()
    {
        CreateListingRequest request = CreateValidRequest();
        CreateListingTranslationRequest translation = request.Translations[0];
        const string whitespace = "\t\r\n\u00A0\u2003\u3000";
        translation.Description = whitespace;
        translation.AddressLine = whitespace;
        translation.City = whitespace;
        translation.Municipality = whitespace;
        translation.Neighborhood = whitespace;

        _validator.ValidateWithKey(request).Should().BeNull();
    }

    [Fact]
    public void ValidateWithKey_RejectsDuplicateLanguageAfterCanonicalNormalization()
    {
        CreateListingRequest request = CreateValidRequest();
        request.Translations =
        [
            CreateTranslation("en", "First"),
            CreateTranslation("\u00A0EN\u3000", "Second")
        ];

        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be("translations");
        failure.Error.Should().Be(
            "Duplicate translation languages are not allowed.");
    }

    private void AssertTranslationFailure(
        CreateListingRequest request,
        string expectedKey)
    {
        CreateListingValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(request);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(expectedKey);
    }

    private static CreateListingRequest CreateValidRequest(
        PropertyType propertyType = PropertyType.Apartment)
    {
        return new CreateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = propertyType,
            Price = 120_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            ApartmentDetails = propertyType == PropertyType.Apartment
                ? new CreateListingApartmentDetailsRequest()
                : null,
            HouseDetails = propertyType == PropertyType.House
                ? new CreateListingHouseDetailsRequest()
                : null,
            Translations =
            [
                CreateTranslation("en", "Valid title")
            ]
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
            AddressLine = "Address",
            City = "Skopje",
            Municipality = "Centar",
            Neighborhood = "Center"
        };
    }
}
