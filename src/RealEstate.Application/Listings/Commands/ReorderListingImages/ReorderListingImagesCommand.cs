namespace RealEstate.Application.Listings.Commands.ReorderListingImages;

public sealed record ReorderListingImagesCommand(
    Guid ListingId,
    IReadOnlyCollection<Guid> ImageIds);