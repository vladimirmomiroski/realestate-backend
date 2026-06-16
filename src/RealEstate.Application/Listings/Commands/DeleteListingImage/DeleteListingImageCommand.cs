namespace RealEstate.Application.Listings.Commands.DeleteListingImage;

public sealed record DeleteListingImageCommand(
    Guid ListingId,
    Guid ImageId);
