using FluentAssertions;
using System.Net;
using RealEstate.Application.Listings.Queries.GetListings;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData("oldest")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("NewestDescending")]
    public async Task GetListings_WithUnsupportedSort_ReturnsBadRequest(
        string sort)
    {
        string encodedSort = Uri.EscapeDataString(sort);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?sort={encodedSort}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("newest")]
    [InlineData("NEWEST")]
    [InlineData("priceAsc")]
    [InlineData("PRICEASC")]
    [InlineData("priceDesc")]
    [InlineData("PRICEDESC")]
    public async Task GetListings_WithSupportedSort_ReturnsOk(
        string sort)
    {
        string encodedSort = Uri.EscapeDataString(sort);

        string currencyQuery =
            sort.Equals(
                "newest",
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : "&currency=EUR";

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?sort={encodedSort}{currencyQuery}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetListings_WithTrimmedCaseInsensitiveInputs_ReturnsOk()
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings?sort=%20PrIcEaSc%20&currency=%20eur%20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("E_R")]
    [InlineData("EÜR")]
    public async Task GetListings_WithInvalidCurrency_ReturnsBadRequest(
        string currency)
    {
        string encodedCurrency = Uri.EscapeDataString(currency);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?currency={encodedCurrency}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("minPrice=90000")]
    [InlineData("maxPrice=100000")]
    [InlineData("sort=priceAsc")]
    [InlineData("sort=priceDesc")]
    public async Task GetListings_WithPriceSemanticsWithoutCurrency_ReturnsBadRequest(
        string queryString)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("minPrice=0&currency=EUR")]
    [InlineData("minPrice=-1&currency=EUR")]
    [InlineData("maxPrice=0&currency=EUR")]
    [InlineData("maxPrice=-1&currency=EUR")]
    [InlineData("minPrice=100001&maxPrice=100000&currency=EUR")]
    public async Task GetListings_WithInvalidPriceRange_ReturnsBadRequest(
        string queryString)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("minAreaSquareMeters=0")]
    [InlineData("minAreaSquareMeters=-1")]
    [InlineData("maxAreaSquareMeters=0")]
    [InlineData("maxAreaSquareMeters=-1")]
    [InlineData("minAreaSquareMeters=101&maxAreaSquareMeters=100")]
    public async Task GetListings_WithInvalidAreaRange_ReturnsBadRequest(
        string queryString)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("minRooms=-1")]
    [InlineData("maxRooms=-1")]
    [InlineData("minRooms=3&maxRooms=2")]
    public async Task GetListings_WithInvalidRoomRange_ReturnsBadRequest(
        string queryString)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("minYardAreaSquareMeters=-1")]
    [InlineData("maxYardAreaSquareMeters=-1")]
    [InlineData(
        "minYardAreaSquareMeters=401&maxYardAreaSquareMeters=400")]
    public async Task GetListings_WithInvalidYardAreaRange_ReturnsBadRequest(
        string queryString)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetListings_WithOneCharacterSearchText_ReturnsBadRequest()
    {
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings?q=a");

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            GetListingsValidator.SearchTextTooShortError);
    }

    [Fact]
    public async Task GetListings_WithSearchTextOver100Characters_ReturnsBadRequest()
    {
        string searchText =
            new('a', 101);

        string encodedSearchText =
            Uri.EscapeDataString(searchText);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings?q={encodedSearchText}");

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            GetListingsValidator.SearchTextTooLongError);
    }
}
