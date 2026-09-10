using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingCommercialDetailsRequest
{
    public CommercialType CommercialType { get; set; } = CommercialType.Unknown;
}
