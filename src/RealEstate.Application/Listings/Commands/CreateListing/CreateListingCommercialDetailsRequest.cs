using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingCommercialDetailsRequest
{
    public CommercialType CommercialType { get; set; } = CommercialType.Unknown;
}
