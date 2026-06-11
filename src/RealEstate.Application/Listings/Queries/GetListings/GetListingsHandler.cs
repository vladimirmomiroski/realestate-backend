using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Queries.GetListings;

public sealed class GetListingsHandler
{
    private readonly IListingRepository _listingRepository;

    public GetListingsHandler(IListingRepository listingRepository)
    {
        _listingRepository = listingRepository;
    }

    public async Task<IReadOnlyList<ListingResponse>> HandleAsync(
        string languageCode,
        CancellationToken cancellationToken)
    {
        var listings = await _listingRepository.GetAllReadOnlyAsync(cancellationToken);

        return listings
            .Select(listing => listing.ToResponse(languageCode))
            .ToList();
    }
}
