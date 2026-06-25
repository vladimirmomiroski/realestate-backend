using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingRequest
{
    public ListingType ListingType { get; set; }

    public PropertyType PropertyType { get; set; }

    public ListingStatus Status { get; set; }

    public Guid? AgencyId { get; set; }

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

    public CreateListingApartmentDetailsRequest? ApartmentDetails { get; set; }

    public CreateListingHouseDetailsRequest? HouseDetails { get; set; }

    public List<CreateListingTranslationRequest> Translations { get; set; } =
        new List<CreateListingTranslationRequest>();
}