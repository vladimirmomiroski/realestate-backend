namespace RealEstate.Application.Listings.Repositories;

public sealed record ListingImageUploadProbeReadModel(
    Guid ListingId,
    Guid? CreatedByUserId,
    int ImageCount);
