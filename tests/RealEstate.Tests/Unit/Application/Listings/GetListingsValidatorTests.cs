using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetListings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class GetListingsValidatorTests
{
    private readonly GetListingsValidator _validator = new();

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
}
