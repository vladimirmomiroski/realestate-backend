namespace RealEstate.Application.Listings.Queries.SearchLocationCandidates;

public sealed record SearchLocationCandidatesQuery(
    Guid ListingId,
    string? LanguageCode);
