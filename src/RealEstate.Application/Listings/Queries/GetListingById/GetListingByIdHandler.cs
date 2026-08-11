using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Queries.GetListingById;

public sealed class GetListingByIdHandler
{
    private readonly IListingRepository _listingRepository;

    public GetListingByIdHandler(IListingRepository listingRepository)
    {
        _listingRepository = listingRepository;
    }

    public async Task<ServiceResult<PublicListingResponse>> HandleAsync(
        Guid id,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdReadOnlyAsync(id, cancellationToken);

        if (listing is null || listing.Status != ListingStatus.Active)
        {
            return ServiceResult<PublicListingResponse>.NotFound(
                "Listing was not found.",
                ErrorCodes.ResourceNotFound);
        }

        PublicListingResponse response = listing.ToPublicResponse(languageCode);

        return ServiceResult<PublicListingResponse>.Success(response);
    }
}
