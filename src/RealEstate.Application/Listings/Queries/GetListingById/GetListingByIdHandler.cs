using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Queries.GetListingById;

public sealed class GetListingByIdHandler
{
    private readonly IListingRepository _listingRepository;

    public GetListingByIdHandler(IListingRepository listingRepository)
    {
        _listingRepository = listingRepository;
    }

    public async Task<ServiceResult<ListingResponse>> HandleAsync(
        Guid id,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdReadOnlyAsync(id, cancellationToken);

        if (listing is null)
        {
            return ServiceResult<ListingResponse>.NotFound("Listing was not found.");
        }

        var response = listing.ToResponse(languageCode);

        return ServiceResult<ListingResponse>.Success(response);
    }
}
