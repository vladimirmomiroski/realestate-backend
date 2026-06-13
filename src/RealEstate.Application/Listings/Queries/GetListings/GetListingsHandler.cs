using RealEstate.Application.Common;
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

    public async Task<PagedResponse<ListingResponse>> HandleAsync(
        GetListingsQuery query,
        CancellationToken cancellationToken)
    {
        query.LanguageCode = string.IsNullOrWhiteSpace(query.LanguageCode)
            ? "mk"
            : query.LanguageCode.Trim().ToLower();

        query.Page = query.Page < 1 ? 1 : query.Page;
        query.PageSize = query.PageSize < 1 ? 20 : query.PageSize;
        query.PageSize = query.PageSize > 100 ? 100 : query.PageSize;

        var pagedListings = await _listingRepository.GetFilteredReadOnlyAsync(
            query,
            cancellationToken);

        var listingResponses = pagedListings.Items
            .Select(listing => listing.ToResponse(query.LanguageCode))
            .ToList();

        return new PagedResponse<ListingResponse>(
            listingResponses,
            query.Page,
            query.PageSize,
            pagedListings.TotalCount);
    }
}