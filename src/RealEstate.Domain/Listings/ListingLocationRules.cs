namespace RealEstate.Domain.Listings;

public static class ListingLocationRules
{
    public const int LocationPrecisionMaxLength = 32;
    public const int GeocodingProviderKeyMaxLength = 64;
    public const int GeocodingResultReferenceMaxLength = 512;
    public const int GeocodedDisplayNameMaxLength = 500;

    public const decimal MinimumLatitude = -90m;
    public const decimal MaximumLatitude = 90m;
    public const decimal MinimumLongitude = -180m;
    public const decimal MaximumLongitude = 180m;
}
