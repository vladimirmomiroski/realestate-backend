namespace RealEstate.Application.Listings.Queries.GetMyListings;

public sealed record GetMyListingsQuery(
    string? Lang,
    int Page = 1,
    int PageSize = 20);
