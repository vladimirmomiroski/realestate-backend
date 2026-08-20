using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Queries.SearchLocationCandidates;

public sealed record ListingLocationCandidateResponse(
    string Label,
    decimal PreviewLatitude,
    decimal PreviewLongitude,
    LocationPrecision Precision,
    string ConfirmationToken);
