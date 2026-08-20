using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Dtos;

public sealed record ListingLocationStateResponse(
    decimal? Latitude,
    decimal? Longitude,
    LocationPrecision? LocationPrecision,
    string? GeocodedDisplayName,
    DateTime? LocationConfirmedAtUtc);
