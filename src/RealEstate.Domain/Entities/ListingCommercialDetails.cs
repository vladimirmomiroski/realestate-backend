using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class ListingCommercialDetails
{
    public Guid ListingId { get; set; }

    public CommercialType CommercialType { get; set; } = CommercialType.Unknown;

    public Listing Listing { get; set; } = null!;
}
