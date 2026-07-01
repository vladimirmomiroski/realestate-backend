using RealEstate.Application.Common.Files;

namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public sealed record UploadListingImageCommand(
    Guid ListingId,
    UploadedFile? File);