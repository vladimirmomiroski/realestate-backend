using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_WhenRequestedLanguageExists_DisplaysRequestedTranslation()
    {
        const string currency = "TLA";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    1,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "mk",
                    "Македонски наслов",
                    city: "Скопје"),
                CreateTranslation(
                    "en",
                    "Requested English Title",
                    city: "Skopje"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=%20EN%20" +
                $"&currency={currency}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<JsonElement> items =
            await ReadPagedListingItemsAsync(response);

        items.Should().ContainSingle();

        JsonElement item = items[0];

        item.GetProperty("id").GetGuid().Should().Be(listingId);
        item.GetProperty("languageCode").GetString().Should().Be("en");
        item.GetProperty("title").GetString()
            .Should().Be("Requested English Title");
        item.GetProperty("city").GetString().Should().Be("Skopje");
    }

    [Fact]
    public async Task GetListings_WhenRequestedLanguageIsMissing_UsesMacedonianForLocationAndResponse()
    {
        const string currency = "TLB";
        const string city = "Македонски Град";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    2,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "English Title",
                    city: "English City"),
                CreateTranslation(
                    "mk",
                    "Македонски наслов",
                    city: city));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=de" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(city)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement item =
            json.GetProperty("items")[0];

        item.GetProperty("id").GetGuid().Should().Be(listingId);
        item.GetProperty("languageCode").GetString().Should().Be("mk");
        item.GetProperty("title").GetString()
            .Should().Be("Македонски наслов");
        item.GetProperty("city").GetString().Should().Be(city);
    }

    [Fact]
    public async Task GetListings_WhenRequestedAndMacedonianAreMissing_UsesPostgreSqlCOrderedFallback()
    {
        const string currency = "TLC";
        const string selectedCity = "Selected C Order City";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    3,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "\U00010000",
                    "Supplementary Title",
                    city: "Excluded City"),
                CreateTranslation(
                    "\uE000",
                    "Private Use Title",
                    city: selectedCity));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=de" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(selectedCity)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement item =
            json.GetProperty("items")[0];

        item.GetProperty("id").GetGuid().Should().Be(listingId);
        item.GetProperty("languageCode").GetString()
            .Should().Be("\uE000");
        item.GetProperty("title").GetString()
            .Should().Be("Private Use Title");
        item.GetProperty("city").GetString()
            .Should().Be(selectedCity);
    }

    [Fact]
    public async Task GetListingById_WhenRequestedAndMacedonianAreMissing_UsesDeterministicFallback()
    {
        const string currency = "TLD";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    4,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "\U00010000",
                    "Supplementary Title",
                    city: "Supplementary City"),
                CreateTranslation(
                    "\uE000",
                    "Private Use Title",
                    city: "Private Use City"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{listingId}?lang=de");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("languageCode").GetString()
            .Should().Be("\uE000");
        json.GetProperty("title").GetString()
            .Should().Be("Private Use Title");
        json.GetProperty("city").GetString()
            .Should().Be("Private Use City");
    }

    [Fact]
    public async Task GetListingById_WhenListingHasNoTranslations_ReturnsNullTranslatedFields()
    {
        Guid listingId =
            await CreateActiveSearchListingAsync(
                price: 100000m,
                currency: "TLE",
                createdAtUtc: new DateTime(
                    2033,
                    1,
                    5,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{listingId}?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("languageCode").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("title").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("description").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("addressLine").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("city").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("municipality").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("neighborhood").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetListings_StructuredLocation_DoesNotMatchPartialValue()
    {
        const string currency = "TLF";

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                6,
                10,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Exact City Test",
                city: "Skopje"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                "&city=Skop");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetListings_StructuredLocation_IsTrimmedAndCaseInsensitive()
    {
        const string currency = "TLG";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    7,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Mixed Case Location",
                    city: "MiXeD City"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString("  mixed city  ")}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Percent%City", "PercentWildcardCity", "TLH")]
    [InlineData("Under_City", "UnderXCity", "TLI")]
    [InlineData(@"Slash\City", "SlashCity", "TLJ")]
    public async Task GetListings_StructuredLocation_WildcardCharactersAreLiteral(
        string exactCity,
        string decoyCity,
        string currency)
    {
        Guid exactListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    8,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Exact Literal Listing",
                    city: exactCity));

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                8,
                11,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Wildcard Decoy Listing",
                city: decoyCity));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(exactCity)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(exactListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_CityAndMunicipality_MustMatchOneEffectiveTranslationRow()
    {
        const string currency = "TLK";
        const string city = "Same Row City";
        const string municipality = "Same Row Municipality";

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                9,
                10,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Split English",
                city: city,
                municipality: "Wrong Municipality"),
            CreateTranslation(
                "mk",
                "Split Macedonian",
                city: "Wrong City",
                municipality: municipality));

        Guid matchingListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    9,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "True Same Row Match",
                    city: city,
                    municipality: municipality),
                CreateTranslation(
                    "mk",
                    "Македонски",
                    city: "Друг Град",
                    municipality: "Друга Општина"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(city)}" +
                $"&municipality={Uri.EscapeDataString(municipality)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(matchingListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_DoesNotMatchLocationFromNonEffectiveTranslation()
    {
        const string currency = "TLL";
        const string hiddenCity = "Hidden Translation City";

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                10,
                10,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Displayed English",
                city: "Displayed City"),
            CreateTranslation(
                "mk",
                "Hidden Macedonian",
                city: hiddenCity));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(hiddenCity)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetListings_WithLocationFilter_AppliesBeforeCountAndPagination()
    {
        const string currency = "TLM";
        const string matchingCity = "Paged Match City";

        Guid olderMatchingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    11,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Older Match",
                    city: matchingCity));

        Guid newerMatchingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    11,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Newer Match",
                    city: matchingCity));

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                11,
                12,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Excluded Newest Listing",
                city: "Different City"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(matchingCity)}" +
                "&page=2" +
                "&pageSize=1");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(olderMatchingId);
        ids.Should().NotContain(newerMatchingId);
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetListings_WhenStructuredLocationExceeds100Characters_ReturnsBadRequest()
    {
        string city = new('a', 101);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&city={city}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string error =
            await response.Content.ReadAsStringAsync();

        error.Should().Contain(
            "City cannot exceed 100 characters.");
    }

    [Fact]
    public async Task GetListings_WithExactMunicipality_ReturnsOnlyExactMatch()
    {
        const string currency = "TLN";
        const string municipality = "Exact Municipality";

        Guid exactListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    12,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Exact Municipality Listing",
                    municipality: municipality));

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                12,
                11,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Municipality Decoy",
                municipality: $"{municipality} Extension"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&municipality={Uri.EscapeDataString(municipality)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(exactListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WithExactNeighborhood_ReturnsOnlyExactMatch()
    {
        const string currency = "TLO";
        const string neighborhood = "Exact Neighborhood";

        Guid exactListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    13,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Exact Neighborhood Listing",
                    neighborhood: neighborhood));

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2033,
                1,
                13,
                11,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Neighborhood Decoy",
                neighborhood: $"{neighborhood} Extension"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&neighborhood={Uri.EscapeDataString(neighborhood)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(exactListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_CityMunicipalityAndNeighborhood_MustMatchOneEffectiveTranslationRow()
    {
        const string currency = "TLP";
        const string city = "Three Level City";
        const string municipality = "Three Level Municipality";
        const string neighborhood = "Three Level Neighborhood";

        Guid splitListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    14,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Split English Translation",
                    city: city,
                    municipality: municipality,
                    neighborhood: "Wrong Neighborhood"),
                CreateTranslation(
                    "mk",
                    "Поделен македонски превод",
                    city: "Wrong City",
                    municipality: municipality,
                    neighborhood: neighborhood));

        Guid matchingListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    14,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "True Three Level Match",
                    city: city,
                    municipality: municipality,
                    neighborhood: neighborhood),
                CreateTranslation(
                    "mk",
                    "Македонски превод",
                    city: "Друг Град",
                    municipality: "Друга Општина",
                    neighborhood: "Друга Населба"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(city)}" +
                $"&municipality={Uri.EscapeDataString(municipality)}" +
                $"&neighborhood={Uri.EscapeDataString(neighborhood)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(matchingListingId);
        ids.Should().NotContain(splitListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WhitespaceOnlyStructuredLocation_IsIgnored()
    {
        const string currency = "TLQ";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    15,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Whitespace Location Listing",
                    city: "Stored City"));

        string whitespaceCity =
            Uri.EscapeDataString("   ");

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={whitespaceCity}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WithLocationFilter_NonActiveStatusesDoNotAffectItemsOrCount()
    {
        const string currency = "TLR";
        const string city = "Visibility Location";

        DateTime baseTimestamp =
            new(
                2033,
                1,
                16,
                10,
                0,
                0,
                DateTimeKind.Utc);

        Guid activeListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                baseTimestamp,
                CreateTranslation(
                    "en",
                    "Active Location Listing",
                    city: city));

        ListingStatus[] nonActiveStatuses =
        [
            ListingStatus.Draft,
        ListingStatus.Archived,
        ListingStatus.Reserved,
        ListingStatus.Sold,
        ListingStatus.Rented
        ];

        for (int index = 0; index < nonActiveStatuses.Length; index++)
        {
            ListingStatus status =
                nonActiveStatuses[index];

            Guid listingId =
                await CreateActiveListingWithTranslationsAsync(
                    currency,
                    baseTimestamp.AddMinutes(index + 1),
                    CreateTranslation(
                        "en",
                        $"{status} Location Listing",
                        city: city));

            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                status);
        }

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(city)}" +
                "&page=1" +
                "&pageSize=1");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(activeListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WhenStructuredLocationHas100Characters_ReturnsMatchingListing()
    {
        const string currency = "TLS";

        string city =
            new('a', 100);

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    17,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Maximum Length City Listing",
                    city: city));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(city)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    private async Task<Guid> CreateActiveListingWithTranslationsAsync(
        string currency,
        DateTime createdAtUtc,
        params ListingTranslation[] translations)
    {
        Guid listingId =
            await CreateActiveSearchListingAsync(
                price: 100000m,
                currency,
                createdAtUtc);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            translations);

        return listingId;
    }

    [Fact]
    public async Task GetListings_DeterministicFallback_DoesNotDependOnTranslationInsertionOrder()
    {
        const string currency = "TLT";
        const string selectedCity = "Insertion Independent City";

        Guid firstListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    18,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "\U00010000",
                    "First Supplementary Title",
                    city: "First Excluded City"),
                CreateTranslation(
                    "\uE000",
                    "First Selected Title",
                    city: selectedCity));

        Guid secondListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2033,
                    1,
                    18,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "\uE000",
                    "Second Selected Title",
                    city: selectedCity),
                CreateTranslation(
                    "\U00010000",
                    "Second Supplementary Title",
                    city: "Second Excluded City"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=de" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(selectedCity)}" +
                "&page=1" +
                "&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(2);

        List<JsonElement> items = json
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        items.Select(item =>
                item.GetProperty("id").GetGuid())
            .Should()
            .BeEquivalentTo(
                [firstListingId, secondListingId]);

        items.Should().OnlyContain(item =>
            item.GetProperty("languageCode").GetString() == "\uE000");

        items.Select(item =>
                item.GetProperty("city").GetString())
            .Should()
            .OnlyContain(value =>
                value == selectedCity);

        items.Select(item =>
                item.GetProperty("title").GetString())
            .Should()
            .BeEquivalentTo(
                [
                    "First Selected Title",
                "Second Selected Title"
                ]);
    }

    private static ListingTranslation CreateTranslation(
        string languageCode,
        string title,
        string? city = null,
        string? municipality = null,
        string? neighborhood = null,
        Guid? id = null)
    {
        return new ListingTranslation
        {
            Id = id ?? Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = title,
            Description = $"{title} description",
            AddressLine = $"{title} address",
            City = city,
            Municipality = municipality,
            Neighborhood = neighborhood
        };
    }
}