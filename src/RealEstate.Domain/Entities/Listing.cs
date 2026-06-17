using RealEstate.Domain.Enums;
using RealEstate.Domain.Common;

namespace RealEstate.Domain.Entities;

public class Listing : IAuditableEntity
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

    public int? BalconyCount { get; set; }

    public int? ParkingSpaces { get; set; }

    public bool? HasBasement { get; set; }

    public bool? IsExchangePossible { get; set; }

    public HeatingType HeatingType { get; set; } = HeatingType.Unknown;

    public FurnishingStatus FurnishingStatus { get; set; } = FurnishingStatus.Unknown;

    public PropertyCondition Condition { get; set; } = PropertyCondition.Unknown;

    public int? YearRenovated { get; set; }

    public Orientation Orientation { get; set; } = Orientation.Unknown;

    public int? YearBuilt { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }

    public ICollection<ListingTranslation> Translations { get; set; } =
        new List<ListingTranslation>();

    public ICollection<ListingImage> Images { get; set; } =
    new List<ListingImage>();

    public decimal CalculatePricePerSquareMeter()
    {
        if (AreaSquareMeters <= 0)
        {
            return 0;
        }

        return Price / AreaSquareMeters;
    }
}