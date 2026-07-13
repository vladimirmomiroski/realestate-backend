namespace RealEstate.Application.Listings.Commands.PublishListing;

public sealed record PublishListingCommand(
    Guid ListingId,
    string? LanguageCode);
