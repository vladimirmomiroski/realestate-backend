namespace RealEstate.Api.RateLimiting;

internal sealed class GeocodingRateLimitOptions
{
    public const string SectionName = "GeocodingRateLimit";

    public const int DefaultPermitLimit = 10;
    public const int DefaultWindowSeconds = 60;

    public int PermitLimit { get; set; } = DefaultPermitLimit;

    public int WindowSeconds { get; set; } = DefaultWindowSeconds;
}
