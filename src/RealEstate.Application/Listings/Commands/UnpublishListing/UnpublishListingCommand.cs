namespace RealEstate.Application.Listings.Commands.UnpublishListing;

public sealed record UnpublishListingCommand(
    Guid ListingId,
    string? LanguageCode);
