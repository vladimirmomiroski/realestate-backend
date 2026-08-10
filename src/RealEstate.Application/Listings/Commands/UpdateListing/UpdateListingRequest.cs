using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingRequest
{
    public required ListingType ListingType { get; set; }

    public required PropertyType PropertyType { get; set; }

    public required decimal Price { get; set; }

    public required string Currency { get; set; }

    public required decimal AreaSquareMeters { get; set; }

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

    public UpdateListingApartmentDetailsRequest? ApartmentDetails { get; set; }

    public UpdateListingHouseDetailsRequest? HouseDetails { get; set; }

    public required List<UpdateListingTranslationRequest> Translations { get; set; }
}
