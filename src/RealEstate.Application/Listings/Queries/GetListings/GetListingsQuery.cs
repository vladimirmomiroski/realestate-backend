using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Queries.GetListings;

public sealed class GetListingsQuery
{
    public string LanguageCode { get; set; } = "mk";

    public Guid? AgencyId { get; set; }

    public ListingType? ListingType { get; set; }

    public PropertyType? PropertyType { get; set; }

    public HeatingType? HeatingType { get; set; }

    public FurnishingStatus? FurnishingStatus { get; set; }

    public PropertyCondition? Condition { get; set; }

    public bool? HasBasement { get; set; }

    public bool? HasElevator { get; set; }

    public ApartmentType? ApartmentType { get; set; }

    public HouseType? HouseType { get; set; }

    public decimal? MinYardAreaSquareMeters { get; set; }

    public decimal? MaxYardAreaSquareMeters { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? Currency { get; set; }

    public decimal? MinAreaSquareMeters { get; set; }

    public decimal? MaxAreaSquareMeters { get; set; }

    public decimal? MinRooms { get; set; }

    public decimal? MaxRooms { get; set; }

    public string Sort { get; set; } = "newest";

    public ListingSortOption SortOption { get; set; } =
        ListingSortOption.Newest;

    public string? City { get; set; }

    public string? Municipality { get; set; }

    public string? Neighborhood { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}