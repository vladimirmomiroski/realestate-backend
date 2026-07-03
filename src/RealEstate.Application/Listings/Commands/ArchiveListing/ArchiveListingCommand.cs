namespace RealEstate.Application.Listings.Commands.ArchiveListing;

public sealed record ArchiveListingCommand(
    Guid ListingId,
    string? LanguageCode);
