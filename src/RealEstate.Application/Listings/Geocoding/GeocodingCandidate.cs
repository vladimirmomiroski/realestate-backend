using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Geocoding;

public sealed record GeocodingCandidate(
    string ProviderKey,
    string ResultReference,
    string DisplayLabel,
    decimal Latitude,
    decimal Longitude,
    LocationPrecision Precision);
