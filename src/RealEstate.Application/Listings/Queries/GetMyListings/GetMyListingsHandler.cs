using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Queries.GetMyListings;

public sealed class GetMyListingsHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyListingsHandler(
        IListingRepository listingRepository,
        ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ListingResponse>> HandleAsync(
        GetMyListingsQuery query,
        CancellationToken cancellationToken)
    {
        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        string languageCode = string.IsNullOrWhiteSpace(query.Lang)
            ? "mk"
            : query.Lang;

        PagedResult<Listing> listings =
            await _listingRepository.GetByCreatedByUserIdAsync(
                userId,
                query.Page,
                query.PageSize,
                cancellationToken);

        List<ListingResponse> responses = listings.Items
            .Select(listing => listing.ToResponse(languageCode))
            .ToList();

        return new PagedResult<ListingResponse>(
            responses,
            listings.Page,
            listings.PageSize,
            listings.TotalCount);
    }
}