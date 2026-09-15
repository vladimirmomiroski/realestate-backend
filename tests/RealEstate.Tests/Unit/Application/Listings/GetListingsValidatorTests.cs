using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class GetListingsValidatorTests
{
    private readonly GetListingsValidator _validator = new();

    public static TheoryData<string, int> DefinedEnumFilterCases => new()
    {
        { "listingType", (int)ListingType.Sale },
        { "propertyType", (int)PropertyType.Apartment },
        { "heatingType", (int)HeatingType.Central },
        { "furnishingStatus", (int)FurnishingStatus.Furnished },
        { "condition", (int)PropertyCondition.Good },
        { "apartmentType", (int)ApartmentType.Standard },
        { "houseType", (int)HouseType.Detached }
    };

    public static TheoryData<string> UnknownEnumFilterCases => new()
    {
        "heatingType",
        "furnishingStatus",
        "condition",
        "apartmentType",
        "houseType"
    };

    [Theory]
    [MemberData(nameof(DefinedEnumFilterCases))]
    public void ValidateWithKey_ShouldAcceptDefinedEnumFilter(
        string field,
        int value)
    {
        GetListingsQuery query = CreateQueryWithEnumFilter(field, value);

        GetListingsValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(query);

        failure.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(UnknownEnumFilterCases))]
    public void ValidateWithKey_ShouldAcceptDefinedUnknownEnumFilter(
        string field)
    {
        GetListingsQuery query = CreateQueryWithEnumFilter(field, 0);

        GetListingsValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(query);

        failure.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(DefinedEnumFilterCases))]
    public void ValidateWithKey_ShouldRejectUndefinedEnumFilter(
        string field,
        int _)
    {
        GetListingsQuery query = CreateQueryWithEnumFilter(field, 999);

        GetListingsValidator.ValidationFailure? failure =
            _validator.ValidateWithKey(query);

        failure.Should().NotBeNull();
        failure!.Key.Should().Be(field);
    }

    [Theory]
    [InlineData("city")]
    [InlineData("municipality")]
    [InlineData("neighborhood")]
    public void Validate_ShouldAcceptStructuredLocationWith100Characters(
        string field)
    {
        // Arrange
        GetListingsQuery query =
            CreateQueryWithLocation(
                field,
                new string('a', 100));

        // Act
        string? error = _validator.Validate(query);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(
        "city",
        GetListingsValidator.CityTooLongError)]
    [InlineData(
        "municipality",
        GetListingsValidator.MunicipalityTooLongError)]
    [InlineData(
        "neighborhood",
        GetListingsValidator.NeighborhoodTooLongError)]
    public void Validate_ShouldRejectStructuredLocationOver100Characters(
        string field,
        string expectedError)
    {
        // Arrange
        GetListingsQuery query =
            CreateQueryWithLocation(
                field,
                new string('a', 101));

        // Act
        string? error = _validator.Validate(query);

        // Assert
        error.Should().Be(expectedError);
    }

    [Fact]
    public void Validate_WhenSearchTextHasOneCharacter_ReturnsTooShortError()
    {
        // Arrange
        var query = new GetListingsQuery
        {
            SearchText = "a"
        };

        // Act
        string? error = _validator.Validate(query);

        // Assert
        error.Should().Be(
            GetListingsValidator.SearchTextTooShortError);
    }

    [Fact]
    public void Validate_WhenSearchTextHasTwoCharacters_ReturnsNoError()
    {
        // Arrange
        var query = new GetListingsQuery
        {
            SearchText = new string('a', 2)
        };

        // Act
        string? error = _validator.Validate(query);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_WhenSearchTextHas100Characters_ReturnsNoError()
    {
        // Arrange
        var query = new GetListingsQuery
        {
            SearchText = new string('a', 100)
        };

        // Act
        string? error = _validator.Validate(query);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_WhenSearchTextHas101Characters_ReturnsTooLongError()
    {
        // Arrange
        var query = new GetListingsQuery
        {
            SearchText = new string('a', 101)
        };

        // Act
        string? error = _validator.Validate(query);

        // Assert
        error.Should().Be(
            GetListingsValidator.SearchTextTooLongError);
    }

    private static GetListingsQuery CreateQueryWithLocation(
        string field,
        string value)
    {
        var query = new GetListingsQuery();

        switch (field)
        {
            case "city":
                query.City = value;
                break;

            case "municipality":
                query.Municipality = value;
                break;

            case "neighborhood":
                query.Neighborhood = value;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Unsupported structured-location field.");
        }

        return query;
    }

    private static GetListingsQuery CreateQueryWithEnumFilter(
        string field,
        int value)
    {
        var query = new GetListingsQuery();

        switch (field)
        {
            case "listingType":
                query.ListingType = (ListingType)value;
                break;
            case "propertyType":
                query.PropertyType = (PropertyType)value;
                break;
            case "heatingType":
                query.HeatingType = (HeatingType)value;
                break;
            case "furnishingStatus":
                query.FurnishingStatus = (FurnishingStatus)value;
                break;
            case "condition":
                query.Condition = (PropertyCondition)value;
                break;
            case "apartmentType":
                query.ApartmentType = (ApartmentType)value;
                break;
            case "houseType":
                query.HouseType = (HouseType)value;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Unsupported enum-filter field.");
        }

        return query;
    }
}
