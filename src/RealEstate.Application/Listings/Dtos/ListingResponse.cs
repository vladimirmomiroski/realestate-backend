using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Dtos;

public sealed class ListingResponse
{
    public Guid Id { get; set; }

    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = default!;

    public decimal AreaSquareMeters { get; set; }

    public decimal PricePerSquareMeter { get; set; }

    public decimal? Rooms { get; set; }

    public decimal? Bathrooms { get; set; }

    public int? Floor { get; set; }

    public int? TotalFloors { get; set; }

    public int? YearBuilt { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? LanguageCode { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Neighborhood { get; set; }
}