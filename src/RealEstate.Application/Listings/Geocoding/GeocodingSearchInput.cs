namespace RealEstate.Application.Listings.Geocoding;

public sealed class GeocodingSearchInput
{
    public GeocodingSearchInput(
        CanonicalListingLocationTranslation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        Location = location;
    }

    public CanonicalListingLocationTranslation Location { get; }
}
