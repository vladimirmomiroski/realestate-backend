using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class ListingHouseDetails
{
    public Guid ListingId { get; set; }

    public HouseType HouseType { get; set; } = HouseType.Unknown;

    public int? NumberOfFloors { get; set; }

    public decimal? YardAreaSquareMeters { get; set; }

    public Listing Listing { get; set; } = null!;
}
