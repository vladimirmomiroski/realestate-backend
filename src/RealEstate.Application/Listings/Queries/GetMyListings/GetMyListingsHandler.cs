using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Queries.GetMyListings;

public sealed class GetMyListingsHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyListingsHandler(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<PagedResult<ListingResponse>>> HandleAsync(
        GetMyListingsQuery query,
        CancellationToken cancellationToken)
    {
        Guid? userId = _currentUserService.UserId;

        if (!userId.HasValue)
        {
            return ServiceResult<PagedResult<ListingResponse>>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (await _userRepository.GetByIdReadOnlyAsync(
                userId.Value,
                cancellationToken) is null)
        {
            return ServiceResult<PagedResult<ListingResponse>>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        string languageCode = string.IsNullOrWhiteSpace(query.Lang)
            ? "mk"
            : query.Lang;

        PagedResult<Listing> listings =
            await _listingRepository.GetByCreatedByUserIdAsync(
                userId.Value,
                query.Page,
                query.PageSize,
                cancellationToken);

        List<ListingResponse> responses = listings.Items
            .Select(listing => listing.ToResponse(languageCode))
            .ToList();

        return ServiceResult<PagedResult<ListingResponse>>.Success(
            new PagedResult<ListingResponse>(
                responses,
                listings.Page,
                listings.PageSize,
                listings.TotalCount));
    }
}
