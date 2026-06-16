namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public sealed record SetPrimaryListingImageCommand(
    Guid ListingId,
    Guid ImageId);