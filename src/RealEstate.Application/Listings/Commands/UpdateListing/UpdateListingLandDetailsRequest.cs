using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingLandDetailsRequest
{
    public LandType LandType { get; set; } = LandType.Unknown;
}
