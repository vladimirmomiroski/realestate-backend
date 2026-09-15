using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.CreateListing;

public sealed class CreateListingLandDetailsRequest
{
    public LandType LandType { get; set; } = LandType.Unknown;
}
