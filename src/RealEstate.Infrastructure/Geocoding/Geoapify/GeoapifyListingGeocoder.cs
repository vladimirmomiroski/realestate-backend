using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Infrastructure.Geocoding.Geoapify;

public sealed class GeoapifyListingGeocoder : IListingGeocoder
{
    public const string ProviderKey = "geoapify";

    private const string ApprovedDatasourceName = "openstreetmap";
    private const string NorthMacedoniaCountryFilter = "countrycode:mk";
    private const decimal StrongConfidenceThreshold = 0.95m;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _httpClient;
    private readonly GeoapifyOptions _options;

    public GeoapifyListingGeocoder(
        HttpClient httpClient,
        IOptions<GeoapifyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GeocodingSearchResult> SearchAsync(
        GeocodingSearchInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryBuildSearchText(input.Location, out string? searchText) ||
            !TryGetProviderLanguage(
                input.Location.LanguageCode,
                out string? providerLanguage))
        {
            return GeocodingSearchResult.Failure(
                GeocodingSearchOutcome.PermanentFailure);
        }

        string requestUri = BuildForwardSearchUri(
            searchText!,
            providerLanguage!);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        GeocodingSearchOutcome? failure = MapSearchFailure(response.StatusCode);

        if (failure.HasValue)
        {
            return GeocodingSearchResult.Failure(failure.Value);
        }

        GeoapifySearchResponse? payload;

        try
        {
            await using Stream content = await response.Content
                .ReadAsStreamAsync(cancellationToken);

            payload = await JsonSerializer.DeserializeAsync<
                GeoapifySearchResponse>(
                content,
                SerializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return GeocodingSearchResult.Failure(
                GeocodingSearchOutcome.MalformedResponse);
        }

        if (payload?.Results is null)
        {
            return GeocodingSearchResult.Failure(
                GeocodingSearchOutcome.MalformedResponse);
        }

        List<GeocodingCandidate> candidates = [];

        foreach (GeoapifyResult? result in payload.Results.Take(
                     _options.CandidateLimit))
        {
            if (result is null)
            {
                return GeocodingSearchResult.Failure(
                    GeocodingSearchOutcome.MalformedResponse);
            }

            CandidateMappingOutcome mapping = TryMapCandidate(
                result,
                out GeocodingCandidate? candidate);

            if (mapping == CandidateMappingOutcome.MalformedResponse)
            {
                return GeocodingSearchResult.Failure(
                    GeocodingSearchOutcome.MalformedResponse);
            }

            if (mapping == CandidateMappingOutcome.Accepted)
            {
                candidates.Add(candidate!);

                if (candidates.Count == _options.CandidateLimit)
                {
                    break;
                }
            }
        }

        return GeocodingSearchResult.Success(candidates);
    }

    public async Task<GeocodingResolutionResult> ResolveAsync(
        GeocodingReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (!string.Equals(
                reference.ProviderKey,
                ProviderKey,
                StringComparison.Ordinal) ||
            !IsValidReference(
                reference.ResultReference,
                ListingLocationRules.GeocodingResultReferenceMaxLength))
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.PermanentFailure);
        }

        string requestUri = BuildPlaceDetailsUri(
            reference.ResultReference);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        GeocodingResolutionOutcome? failure =
            MapResolutionFailure(response.StatusCode);

        if (failure.HasValue)
        {
            return GeocodingResolutionResult.Failure(failure.Value);
        }

        GeoapifyPlaceDetailsResponse? payload;

        try
        {
            await using Stream content = await response.Content
                .ReadAsStreamAsync(cancellationToken);

            payload = await JsonSerializer.DeserializeAsync<
                GeoapifyPlaceDetailsResponse>(
                content,
                SerializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.MalformedResponse);
        }

        if (payload?.Features is null)
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.MalformedResponse);
        }

        if (payload.Features.Count == 0)
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.NotFound);
        }

        if (payload.Features.Count != 1 ||
            payload.Features[0] is not GeoapifyFeature feature ||
            !string.Equals(
                feature.Properties?.FeatureType,
                "details",
                StringComparison.Ordinal))
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.MalformedResponse);
        }

        GeoapifyResult? result = feature.Properties;

        if (result is null ||
            string.IsNullOrEmpty(result.PlaceId) ||
            !string.Equals(
                result.PlaceId,
                reference.ResultReference,
                StringComparison.Ordinal))
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.Stale);
        }

        if (!IsValidReference(
                result.PlaceId,
                ListingLocationRules.GeocodingResultReferenceMaxLength) ||
            !HasApprovedDatasource(result, allowMissing: true) ||
            !TryGetCoordinates(result, out decimal latitude, out decimal longitude) ||
            !HasWellFormedMappingText(result))
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.MalformedResponse);
        }

        string? displayName = GetOptionalDisplayName(result.Formatted);

        if (result.Formatted is not null && displayName is null &&
            !IsOptionalDisplayValue(result.Formatted))
        {
            return GeocodingResolutionResult.Failure(
                GeocodingResolutionOutcome.MalformedResponse);
        }

        var snapshot = new ResolvedGeocodingSnapshot(
            ProviderKey,
            result.PlaceId,
            latitude,
            longitude,
            MapPrecision(result),
            displayName);

        return GeocodingResolutionResult.Success(snapshot);
    }

    private string BuildForwardSearchUri(
        string searchText,
        string providerLanguage)
    {
        return "v1/geocode/search" +
               $"?text={Escape(searchText)}" +
               $"&lang={Escape(providerLanguage)}" +
               $"&limit={_options.CandidateLimit}" +
               "&format=json" +
               $"&filter={Escape(NorthMacedoniaCountryFilter)}" +
               $"&apiKey={Escape(_options.ApiKey)}";
    }

    private string BuildPlaceDetailsUri(string resultReference)
    {
        return "v2/place-details" +
               $"?id={Escape(resultReference)}" +
               $"&apiKey={Escape(_options.ApiKey)}";
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static bool TryBuildSearchText(
        CanonicalListingLocationTranslation location,
        out string? searchText)
    {
        string?[] components =
        [
            location.AddressLine,
            location.Neighborhood,
            location.City,
            location.Municipality
        ];

        if (components.Any(value =>
                value is not null && !GeoapifyText.IsWellFormed(value)))
        {
            searchText = null;
            return false;
        }

        searchText = string.Join(
            ", ",
            components.Where(value => value is not null));

        return searchText.Length > 0;
    }

    private static bool TryGetProviderLanguage(
        string languageCode,
        out string? providerLanguage)
    {
        if (!ListingTranslationRules.IsCanonicalLanguageCode(languageCode))
        {
            providerLanguage = null;
            return false;
        }

        int separatorIndex = languageCode.IndexOf('-', StringComparison.Ordinal);
        providerLanguage = separatorIndex < 0
            ? languageCode
            : languageCode[..separatorIndex];

        return providerLanguage.Length == 2;
    }

    private static CandidateMappingOutcome TryMapCandidate(
        GeoapifyResult result,
        out GeocodingCandidate? candidate)
    {
        candidate = null;

        if (!HasApprovedDatasource(result, allowMissing: false) ||
            !TryGetCoordinates(result, out decimal latitude, out decimal longitude) ||
            !HasWellFormedMappingText(result))
        {
            return CandidateMappingOutcome.MalformedResponse;
        }

        if (string.IsNullOrWhiteSpace(result.PlaceId) ||
            string.IsNullOrWhiteSpace(result.Formatted))
        {
            return CandidateMappingOutcome.MalformedResponse;
        }

        if (!IsValidReference(
                result.PlaceId,
                ListingLocationRules.GeocodingResultReferenceMaxLength) ||
            !IsRequiredDisplay(
                result.Formatted,
                ListingLocationRules.GeocodedDisplayNameMaxLength))
        {
            return CandidateMappingOutcome.Rejected;
        }

        candidate = new GeocodingCandidate(
            ProviderKey,
            result.PlaceId!,
            result.Formatted!,
            latitude,
            longitude,
            MapPrecision(result));

        return CandidateMappingOutcome.Accepted;
    }

    private static bool HasApprovedDatasource(
        GeoapifyResult result,
        bool allowMissing)
    {
        if (result.Datasource is null)
        {
            return allowMissing;
        }

        return string.Equals(
            result.Datasource.SourceName,
            ApprovedDatasourceName,
            StringComparison.Ordinal);
    }

    private static bool TryGetCoordinates(
        GeoapifyResult result,
        out decimal latitude,
        out decimal longitude)
    {
        latitude = default;
        longitude = default;

        if (!result.Latitude.HasValue ||
            !result.Longitude.HasValue ||
            result.Latitude.Value < ListingLocationRules.MinimumLatitude ||
            result.Latitude.Value > ListingLocationRules.MaximumLatitude ||
            result.Longitude.Value < ListingLocationRules.MinimumLongitude ||
            result.Longitude.Value > ListingLocationRules.MaximumLongitude)
        {
            return false;
        }

        latitude = result.Latitude.Value;
        longitude = result.Longitude.Value;
        return true;
    }

    private static bool HasWellFormedMappingText(GeoapifyResult result)
    {
        string?[] values =
        [
            result.PlaceId,
            result.Formatted,
            result.ResultType,
            result.Street,
            result.HouseNumber,
            result.Suburb,
            result.City,
            result.Rank?.MatchType,
            result.Datasource?.SourceName
        ];

        return values.All(value =>
            value is null || GeoapifyText.IsWellFormed(value));
    }

    private static string? GetOptionalDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !GeoapifyText.IsWellFormed(value) ||
            !GeoapifyText.IsWithinScalarLimit(
                value,
                ListingLocationRules.GeocodedDisplayNameMaxLength))
        {
            return null;
        }

        return value;
    }

    private static bool IsOptionalDisplayValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               (GeoapifyText.IsWellFormed(value) &&
                !GeoapifyText.IsWithinScalarLimit(
                    value,
                    ListingLocationRules.GeocodedDisplayNameMaxLength));
    }

    private static bool IsValidReference(
        string? value,
        int maximumScalars)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               string.Equals(
                   value,
                   ListingTranslationRules.TrimBoundaryWhitespace(value),
                   StringComparison.Ordinal) &&
               GeoapifyText.IsWellFormed(value) &&
               GeoapifyText.IsWithinScalarLimit(value, maximumScalars);
    }

    private static bool IsRequiredDisplay(
        string value,
        int maximumScalars)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               GeoapifyText.IsWellFormed(value) &&
               GeoapifyText.IsWithinScalarLimit(value, maximumScalars);
    }

    private static LocationPrecision MapPrecision(GeoapifyResult result)
    {
        GeoapifyRank? rank = result.Rank;

        if (string.Equals(result.ResultType, "building", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(result.Street) &&
            !string.IsNullOrWhiteSpace(result.HouseNumber) &&
            string.Equals(rank?.MatchType, "full_match", StringComparison.Ordinal) &&
            IsStrong(rank?.Confidence) &&
            IsStrong(rank?.ConfidenceBuildingLevel))
        {
            return LocationPrecision.ExactAddress;
        }

        if ((string.Equals(result.ResultType, "street", StringComparison.Ordinal) ||
             string.Equals(result.ResultType, "building", StringComparison.Ordinal)) &&
            !string.IsNullOrWhiteSpace(result.Street) &&
            IsStrong(rank?.ConfidenceStreetLevel) &&
            (string.Equals(rank?.MatchType, "full_match", StringComparison.Ordinal) ||
             string.Equals(rank?.MatchType, "match_by_street", StringComparison.Ordinal)))
        {
            return LocationPrecision.Street;
        }

        if (string.Equals(result.ResultType, "suburb", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(result.Suburb) &&
            IsStrong(rank?.Confidence))
        {
            return LocationPrecision.Neighborhood;
        }

        if (string.Equals(result.ResultType, "city", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(result.City) &&
            IsStrong(rank?.ConfidenceCityLevel) &&
            (string.Equals(rank?.MatchType, "full_match", StringComparison.Ordinal) ||
             string.Equals(
                 rank?.MatchType,
                 "match_by_city_or_disrict",
                 StringComparison.Ordinal)))
        {
            return LocationPrecision.City;
        }

        return LocationPrecision.Approximate;
    }

    private static bool IsStrong(decimal? confidence)
    {
        return confidence.HasValue &&
               confidence.Value >= StrongConfidenceThreshold &&
               confidence.Value <= 1m;
    }

    private static GeocodingSearchOutcome? MapSearchFailure(
        HttpStatusCode statusCode)
    {
        int status = (int)statusCode;

        if (status is >= 200 and <= 299)
        {
            return null;
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return GeocodingSearchOutcome.RateLimited;
        }

        return status >= 500
            ? GeocodingSearchOutcome.Unavailable
            : GeocodingSearchOutcome.PermanentFailure;
    }

    private static GeocodingResolutionOutcome? MapResolutionFailure(
        HttpStatusCode statusCode)
    {
        int status = (int)statusCode;

        if (status is >= 200 and <= 299)
        {
            return null;
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return GeocodingResolutionOutcome.NotFound;
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return GeocodingResolutionOutcome.RateLimited;
        }

        return status >= 500
            ? GeocodingResolutionOutcome.Unavailable
            : GeocodingResolutionOutcome.PermanentFailure;
    }

    private enum CandidateMappingOutcome
    {
        Accepted,
        Rejected,
        MalformedResponse
    }

    private sealed class GeoapifySearchResponse
    {
        [JsonPropertyName("results")]
        public List<GeoapifyResult?>? Results { get; init; }
    }

    private sealed class GeoapifyPlaceDetailsResponse
    {
        [JsonPropertyName("features")]
        public List<GeoapifyFeature?>? Features { get; init; }
    }

    private sealed class GeoapifyFeature
    {
        [JsonPropertyName("properties")]
        public GeoapifyResult? Properties { get; init; }
    }

    private sealed class GeoapifyResult
    {
        [JsonPropertyName("feature_type")]
        public string? FeatureType { get; init; }

        [JsonPropertyName("place_id")]
        public string? PlaceId { get; init; }

        [JsonPropertyName("formatted")]
        public string? Formatted { get; init; }

        [JsonPropertyName("lat")]
        public decimal? Latitude { get; init; }

        [JsonPropertyName("lon")]
        public decimal? Longitude { get; init; }

        [JsonPropertyName("result_type")]
        public string? ResultType { get; init; }

        [JsonPropertyName("street")]
        public string? Street { get; init; }

        [JsonPropertyName("housenumber")]
        public string? HouseNumber { get; init; }

        [JsonPropertyName("suburb")]
        public string? Suburb { get; init; }

        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("rank")]
        public GeoapifyRank? Rank { get; init; }

        [JsonPropertyName("datasource")]
        public GeoapifyDatasource? Datasource { get; init; }
    }

    private sealed class GeoapifyRank
    {
        [JsonPropertyName("confidence")]
        public decimal? Confidence { get; init; }

        [JsonPropertyName("confidence_city_level")]
        public decimal? ConfidenceCityLevel { get; init; }

        [JsonPropertyName("confidence_street_level")]
        public decimal? ConfidenceStreetLevel { get; init; }

        [JsonPropertyName("confidence_building_level")]
        public decimal? ConfidenceBuildingLevel { get; init; }

        [JsonPropertyName("match_type")]
        public string? MatchType { get; init; }
    }

    private sealed class GeoapifyDatasource
    {
        [JsonPropertyName("sourcename")]
        public string? SourceName { get; init; }
    }
}
