using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public class Listing
{
    public Guid Id { get; set; }

    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Draft;

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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

     public ICollection<ListingTranslation> Translations { get; set; } =
        new List<ListingTranslation>();

    public decimal CalculatePricePerSquareMeter()
    {
        if (AreaSquareMeters <= 0)
        {
            return 0;
        }

        return Price / AreaSquareMeters;
    }
}