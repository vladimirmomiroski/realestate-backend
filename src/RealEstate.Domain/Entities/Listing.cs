using RealEstate.Domain.Enums;
using RealEstate.Domain.Common;

namespace RealEstate.Domain.Entities;

public class Listing : IAuditableEntity
{
    public Guid Id { get; set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? AgencyId { get; private set; }

    public Agency? Agency { get; private set; }

    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "EUR";

    public decimal AreaSquareMeters { get; set; }

    public decimal? Rooms { get; set; }

    public decimal? Bathrooms { get; set; }

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

    public ListingApartmentDetails? ApartmentDetails { get; set; }

    public ListingHouseDetails? HouseDetails { get; set; }

    public ICollection<ListingTranslation> Translations { get; set; } =
        new List<ListingTranslation>();

    public ICollection<ListingImage> Images { get; set; } =
    new List<ListingImage>();

    public void AssignAgency(Guid agencyId)
    {
        if (agencyId == Guid.Empty)
        {
            throw new ArgumentException("Agency id cannot be empty.", nameof(agencyId));
        }

        AgencyId = agencyId;
    }

    public void ClearAgency()
    {
        AgencyId = null;
    }

    public void AssignCreator(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        CreatedByUserId = userId;
    }

    public decimal CalculatePricePerSquareMeter()
    {
        if (AreaSquareMeters <= 0)
        {
            return 0;
        }

        return Price / AreaSquareMeters;
    }
}