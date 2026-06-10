using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Dtos;

public sealed class CreateListingRequest
{
    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "EUR";

    public decimal AreaSquareMeters { get; set; }

    public decimal? Rooms { get; set; }

    public decimal? Bathrooms { get; set; }

    public int? Floor { get; set; }

    public int? TotalFloors { get; set; }

    public int? YearBuilt { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public List<CreateListingTranslationRequest> Translations { get; set; } = [];
}