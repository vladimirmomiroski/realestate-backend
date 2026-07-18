using FluentAssertions;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateListing_WithOnlyOneCoordinate_ReturnsBadRequest(
        bool provideLatitude)
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                latitude: provideLatitude
                    ? 41.9981m
                    : null,
                longitude: provideLatitude
                    ? null
                    : 21.4254m);

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            CreateListingValidator.CoordinatePairError);
    }

    [Theory]
    [InlineData(-91, 21)]
    [InlineData(91, 21)]
    public async Task CreateListing_WithOutOfRangeLatitude_ReturnsBadRequest(
        int latitude,
        int longitude)
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                latitude: latitude,
                longitude: longitude);

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            CreateListingValidator.LatitudeOutOfRangeError);
    }

    [Theory]
    [InlineData(41, -181)]
    [InlineData(41, 181)]
    public async Task CreateListing_WithOutOfRangeLongitude_ReturnsBadRequest(
        int latitude,
        int longitude)
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                latitude: latitude,
                longitude: longitude);

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            CreateListingValidator.LongitudeOutOfRangeError);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public async Task CreateListing_WithBoundaryCoordinates_PersistsAndReturnsThem(
        int latitude,
        int longitude)
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                latitude: latitude,
                longitude: longitude);

        HttpResponseMessage createResponse =
            await PostListingAsNewUserAsync(request);

        createResponse.StatusCode.Should().Be(
            HttpStatusCode.Created);

        JsonElement created =
            await createResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        Guid listingId =
            created.GetProperty("id").GetGuid();

        created.GetProperty("latitude").GetDecimal()
            .Should().Be(latitude);

        created.GetProperty("longitude").GetDecimal()
            .Should().Be(longitude);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        HttpResponseMessage getResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{listingId}?lang=en");

        getResponse.StatusCode.Should().Be(
            HttpStatusCode.OK);

        JsonElement persisted =
            await getResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        persisted.GetProperty("latitude").GetDecimal()
            .Should().Be(latitude);

        persisted.GetProperty("longitude").GetDecimal()
            .Should().Be(longitude);
    }

    [Fact]
    public async Task CreateListing_WithSixDecimalCoordinates_PreservesPrecision()
    {
        const decimal latitude = 41.998123m;
        const decimal longitude = 21.425456m;

        object request =
            ListingTestHelpers.CreateValidListingRequest(
                latitude: latitude,
                longitude: longitude);

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.Created);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("latitude").GetDecimal()
            .Should().Be(latitude);

        json.GetProperty("longitude").GetDecimal()
            .Should().Be(longitude);
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("E_R")]
    [InlineData("EÜR")]
    public async Task CreateListing_WithInvalidCurrency_ReturnsBadRequest(
        string currency)
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                currency: currency);

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            CreateListingValidator.InvalidCurrencyError);
    }

    [Fact]
    public async Task CreateListing_WithTrimmedMixedCaseCurrency_ReturnsNormalizedCurrency()
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                currency: " eUr ");

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.Created);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("currency").GetString()
            .Should().Be("EUR");
    }

    private async Task<HttpResponseMessage> PostListingAsNewUserAsync(
        object request)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            return await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
