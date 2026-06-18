using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Dtos;

public sealed class ListingResponse
{
    public Guid Id { get; set; }

    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public ListingStatus Status { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = default!;

    public decimal AreaSquareMeters { get; set; }

    public decimal PricePerSquareMeter { get; set; }

    public decimal? Rooms { get; set; }

    public decimal? Bathrooms { get; set; }

    public int? YearBuilt { get; set; }

    public int? BalconyCount { get; set; }

    public int? ParkingSpaces { get; set; }

    public bool? HasBasement { get; set; }

    public bool? IsExchangePossible { get; set; }

    public HeatingType HeatingType { get; set; }

    public FurnishingStatus FurnishingStatus { get; set; }

    public PropertyCondition Condition { get; set; }

    public int? YearRenovated { get; set; }

    public Orientation Orientation { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? LanguageCode { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Municipality { get; set; }

    public string? Neighborhood { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public ListingApartmentDetailsResponse? ApartmentDetails { get; set; }

    public ListingHouseDetailsResponse? HouseDetails { get; set; }

    public List<ListingImageResponse> Images { get; set; } =
        new List<ListingImageResponse>();
}