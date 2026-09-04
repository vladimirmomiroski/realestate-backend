using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class ListingLandDetails
{
    public Guid ListingId { get; set; }

    public LandType LandType { get; set; } = LandType.Unknown;

    public Listing Listing { get; set; } = null!;
}
