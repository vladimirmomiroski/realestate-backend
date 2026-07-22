using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Net;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData("title", "TUA")]
    [InlineData("city", "TUB")]
    [InlineData("municipality", "TUC")]
    [InlineData("neighborhood", "TUD")]
    public async Task GetListings_QMatchesLiteralContainsInEffectiveTranslationField(
        string field,
        string currency)
    {
        const string phrase = "Search Needle";

        ListingTranslation translation =
            CreateSearchableTranslation(
                field,
                phrase);

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    1,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                translation);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(phrase)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WhitespaceOnlyQ_DoesNotAddTextPredicate()
    {
        const string currency = "TUE";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    2,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Completely Unrelated Listing"));

        string whitespaceQuery =
            Uri.EscapeDataString("   ");

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={whitespaceQuery}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_Q_IsTrimmedAndCaseInsensitive()
    {
        const string currency = "TUF";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    3,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Prefix MiXeD Search Phrase Suffix"));

        string searchText =
            Uri.EscapeDataString(
                "  mixed search phrase  ");

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={searchText}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_QTreatsEntireValueAsOneLiteralPhrase()
    {
        const string currency = "TUG";
        const string phrase = "Urban Family";

        Guid matchingListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    4,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Modern Urban Family Home"));

        Guid separatedWordsListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    4,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Urban Luxury Family Home"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(phrase)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(matchingListingId);
        ids.Should().NotContain(separatedWordsListingId);
        totalCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Q%", "QWildcard", "TUH")]
    [InlineData("Q_", "QX", "TUI")]
    [InlineData(@"Q\", "QSlash", "TUJ")]
    public async Task GetListings_QWildcardCharactersAreLiteral(
        string searchText,
        string decoyValue,
        string currency)
    {
        Guid exactListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    5,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"Exact {searchText} Search Match"));

        Guid decoyListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    5,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"Decoy {decoyValue} Search Match"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(searchText)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(exactListingId);
        ids.Should().NotContain(decoyListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_QHandlesNullOptionalSearchFields()
    {
        const string currency = "TUK";
        const string phrase = "Nullable Fields Match";

        ListingTranslation translation =
            CreateTranslation(
                "en",
                $"Prefix {phrase} Suffix");

        translation.City.Should().BeNull();
        translation.Municipality.Should().BeNull();
        translation.Neighborhood.Should().BeNull();

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    1,
                    6,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                translation);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(phrase)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    [Theory]
    [InlineData("description", "TUL")]
    [InlineData("addressLine", "TUM")]
    public async Task GetListings_QDoesNotSearchExcludedTranslationField(
        string field,
        string currency)
    {
        const string excludedPhrase =
            "Excluded Search Phrase";

        ListingTranslation translation =
            CreateTranslation(
                "en",
                "Neutral Search Title",
                city: "Neutral Search City",
                municipality: "Neutral Search Municipality",
                neighborhood: "Neutral Search Neighborhood");

        switch (field)
        {
            case "description":
                translation.Description =
                    $"Prefix {excludedPhrase} Suffix";
                break;

            case "addressLine":
                translation.AddressLine =
                    $"Prefix {excludedPhrase} Suffix";
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Unsupported excluded search field.");
        }

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2034,
                1,
                7,
                10,
                0,
                0,
                DateTimeKind.Utc),
            translation);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(excludedPhrase)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetListings_QUsesCaseInsensitiveRequestedLanguageSelection()
    {
        const string currency = "TUN";
        const string phrase = "Repository Language Match";

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    2,
                    1,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "mk",
                    "Македонски наслов",
                    city: "Друг Град"),
                CreateTranslation(
                    "EN",
                    $"Prefix {phrase} Suffix",
                    city: "Selected City"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=%20en%20" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(phrase)}");

        List<JsonElement> items =
            await ReadPagedListingItemsAsync(response);

        items.Should().ContainSingle();

        JsonElement item = items[0];

        item.GetProperty("id").GetGuid()
            .Should().Be(listingId);

        item.GetProperty("languageCode").GetString()
            .Should().Be("EN");

        item.GetProperty("title").GetString()
            .Should().Be($"Prefix {phrase} Suffix");
    }

    [Fact]
    public async Task GetListings_QDoesNotMatchPhraseFromNonEffectiveTranslation()
    {
        const string currency = "TUO";
        const string hiddenPhrase = "Hidden Search Phrase";

        await CreateActiveListingWithTranslationsAsync(
            currency,
            new DateTime(
                2034,
                2,
                2,
                10,
                0,
                0,
                DateTimeKind.Utc),
            CreateTranslation(
                "en",
                "Displayed English Translation",
                city: "Displayed City"),
            CreateTranslation(
                "mk",
                $"Prefix {hiddenPhrase} Suffix",
                city: "Hidden City"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(hiddenPhrase)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetListings_QAndStructuredLocation_MustMatchOneEffectiveTranslationRow()
    {
        const string currency = "TUP";
        const string phrase = "Same Row Search";
        const string city = "Same Row Search City";

        Guid splitListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    2,
                    3,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"Prefix {phrase} Suffix",
                    city: "Wrong City"),
                CreateTranslation(
                    "mk",
                    "Македонски наслов",
                    city: city));

        Guid matchingListingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    2,
                    3,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"True {phrase} Match",
                    city: city),
                CreateTranslation(
                    "mk",
                    "Друг превод",
                    city: "Друг Град"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(phrase)}" +
                $"&city={Uri.EscapeDataString(city)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(matchingListingId);
        ids.Should().NotContain(splitListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_Q_AppliesBeforeCountAndPagination()
    {
        const string currency = "TUQ";
        const string phrase = "Paged Search Match";

        Guid olderMatchingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    2,
                    4,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"Older {phrase}"));

        Guid newerMatchingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    2,
                    4,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"Newer {phrase}"));

        Guid excludedNewestId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    2,
                    4,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    "Newest Nonmatching Listing"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(phrase)}" +
                "&page=2" +
                "&pageSize=1");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(olderMatchingId);
        ids.Should().NotContain(newerMatchingId);
        ids.Should().NotContain(excludedNewestId);
        totalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetListings_Q_NonActiveStatusesDoNotAffectItemsOrCount()
    {
        const string currency = "TUR";
        const string phrase = "Visibility Search Phrase";

        DateTime baseTimestamp =
            new(
                2034,
                2,
                5,
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
                    $"Active {phrase}"));

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
                        $"{status} {phrase}"));

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
                $"&q={Uri.EscapeDataString(phrase)}" +
                "&page=1" +
                "&pageSize=1");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(activeListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_QExcludesListingWithNoTranslations()
    {
        const string currency = "TUS";

        Guid listingId =
            await CreateActiveSearchListingAsync(
                price: 100000m,
                currency,
                createdAtUtc: new DateTime(
                    2034,
                    2,
                    6,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                "&q=Missing");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetListings_WithoutQ_ReturnsListingWithNoTranslationsAndNullTranslatedFields()
    {
        const string currency = "TUT";

        Guid listingId =
            await CreateActiveSearchListingAsync(
                price: 100000m,
                currency,
                createdAtUtc: new DateTime(
                    2034,
                    2,
                    7,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId);

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}");

        List<JsonElement> items =
            await ReadPagedListingItemsAsync(response);

        items.Should().ContainSingle();

        JsonElement item = items[0];

        item.GetProperty("id").GetGuid()
            .Should().Be(listingId);

        item.GetProperty("languageCode").ValueKind
            .Should().Be(JsonValueKind.Null);

        item.GetProperty("title").ValueKind
            .Should().Be(JsonValueKind.Null);

        item.GetProperty("city").ValueKind
            .Should().Be(JsonValueKind.Null);

        item.GetProperty("municipality").ValueKind
            .Should().Be(JsonValueKind.Null);

        item.GetProperty("neighborhood").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetListings_QWithCurrencyAndPriceFilter_ReturnsOnlyFullyMatchingListing()
    {
        const string selectedCurrency = "TUU";
        const string otherCurrency = "TUV";
        const string phrase = "Composed Search Phrase";

        Guid matchingListingId =
            await CreateActiveSearchListingAsync(
                price: 200000m,
                currency: selectedCurrency,
                createdAtUtc: new DateTime(
                    2034,
                    3,
                    1,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            matchingListingId,
            CreateTranslation(
                "en",
                $"Matching {phrase}"));

        Guid excludedByPriceId =
            await CreateActiveSearchListingAsync(
                price: 100000m,
                currency: selectedCurrency,
                createdAtUtc: new DateTime(
                    2034,
                    3,
                    1,
                    11,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            excludedByPriceId,
            CreateTranslation(
                "en",
                $"Cheap {phrase}"));

        Guid excludedBySearchId =
            await CreateActiveSearchListingAsync(
                price: 300000m,
                currency: selectedCurrency,
                createdAtUtc: new DateTime(
                    2034,
                    3,
                    1,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            excludedBySearchId,
            CreateTranslation(
                "en",
                "Expensive Unrelated Listing"));

        Guid excludedByCurrencyId =
            await CreateActiveSearchListingAsync(
                price: 300000m,
                currency: otherCurrency,
                createdAtUtc: new DateTime(
                    2034,
                    3,
                    1,
                    13,
                    0,
                    0,
                    DateTimeKind.Utc));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            excludedByCurrencyId,
            CreateTranslation(
                "en",
                $"Other Currency {phrase}"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={selectedCurrency}" +
                "&minPrice=150000" +
                $"&q={Uri.EscapeDataString(phrase)}");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(matchingListingId);

        ids.Should().NotContain(
            [
                excludedByPriceId,
            excludedBySearchId,
            excludedByCurrencyId
            ]);

        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WhenQHas100Characters_ReturnsMatchingListing()
    {
        const string currency = "TUW";

        string searchText =
            new('x', 100);

        Guid listingId =
            await CreateActiveListingWithTranslationsAsync(
                currency,
                new DateTime(
                    2034,
                    3,
                    2,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc),
                CreateTranslation(
                    "en",
                    $"Prefix{searchText}Suffix"));

        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&q={Uri.EscapeDataString(searchText)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadSearchPageAsync(response);

        ids.Should().Equal(listingId);
        totalCount.Should().Be(1);
    }

    private static ListingTranslation CreateSearchableTranslation(
        string field,
        string phrase)
    {
        ListingTranslation translation =
            CreateTranslation(
                "en",
                "Neutral Search Title",
                city: "Neutral Search City",
                municipality: "Neutral Search Municipality",
                neighborhood: "Neutral Search Neighborhood");

        string searchableValue =
            $"Prefix {phrase} Suffix";

        switch (field)
        {
            case "title":
                translation.Title = searchableValue;
                break;

            case "city":
                translation.City = searchableValue;
                break;

            case "municipality":
                translation.Municipality = searchableValue;
                break;

            case "neighborhood":
                translation.Neighborhood = searchableValue;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Unsupported searchable translation field.");
        }

        return translation;
    }
}
