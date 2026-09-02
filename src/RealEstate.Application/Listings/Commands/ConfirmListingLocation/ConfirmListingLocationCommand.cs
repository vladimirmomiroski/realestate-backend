namespace RealEstate.Application.Listings.Commands.ConfirmListingLocation;

public sealed record ConfirmListingLocationCommand(
    Guid ListingId,
    string ConfirmationToken);
