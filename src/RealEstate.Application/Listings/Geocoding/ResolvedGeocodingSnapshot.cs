using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Geocoding;

public sealed record ResolvedGeocodingSnapshot(
    string ProviderKey,
    string ResultReference,
    decimal Latitude,
    decimal Longitude,
    LocationPrecision Precision,
    string? DisplayName);
