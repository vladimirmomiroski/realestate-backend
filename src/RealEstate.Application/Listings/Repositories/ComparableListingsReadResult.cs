using RealEstate.Domain.Entities;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Repositories;

public sealed record ComparableListingsReadResult(
    bool SourceFound,
    IReadOnlyList<Listing> Items,
    IReadOnlyList<ListingPublicationReadinessViolation>
        SourceIntegrityViolations);
