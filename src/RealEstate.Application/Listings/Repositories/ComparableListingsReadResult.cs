using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Repositories;

public sealed record ComparableListingsReadResult(
    bool SourceFound,
    IReadOnlyList<Listing> Items);
