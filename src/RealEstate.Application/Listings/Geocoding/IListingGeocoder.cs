namespace RealEstate.Application.Listings.Geocoding;

public interface IListingGeocoder
{
    Task<GeocodingSearchResult> SearchAsync(
        GeocodingSearchInput input,
        CancellationToken cancellationToken);

    Task<GeocodingResolutionResult> ResolveAsync(
        GeocodingReference reference,
        CancellationToken cancellationToken);
}
