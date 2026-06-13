using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Queries.GetListings;

public sealed class GetListingsQuery
{
    public string LanguageCode { get; set; } = "mk";

    public ListingType? ListingType { get; set; }

    public PropertyType? PropertyType { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? City { get; set; }

    public string? Neighborhood { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}