using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class ListingApartmentDetails
{
    public Guid ListingId { get; set; }

    public ApartmentType ApartmentType { get; set; } = ApartmentType.Unknown;

    public int? Floor { get; set; }

    public int? TotalFloors { get; set; }

    public bool? HasElevator { get; set; }

    public Listing Listing { get; set; } = null!;
}
