using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;

public sealed class GetAgencyDashboardListingsHandler
{
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetAgencyDashboardListingsHandler(
        AgencyListingAccessChecker agencyListingAccessChecker,
        IListingRepository listingRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<PagedResult<ListingResponse>>> HandleAsync(
        GetAgencyDashboardListingsQuery query,
        CancellationToken cancellationToken)
    {
        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        var user = await _userRepository.GetByIdReadOnlyAsync(userId, cancellationToken);

        if (user is null || user.Status == UserStatus.Disabled)
        {
            return ServiceResult<PagedResult<ListingResponse>>.Forbidden(
                "User is not allowed to view agency listings.");
        }

        var agencyAccessResult =
            await _agencyListingAccessChecker.EnsureCanManageAgencyListingsAsync<PagedResult<ListingResponse>>(
                query.AgencyId,
                userId,
                "User is not allowed to view agency listings.",
                cancellationToken);

        if (agencyAccessResult is not null)
        {
            return agencyAccessResult;
        }

        string languageCode = NormalizeLanguageCode(query.LanguageCode);

        PagedResult<Listing> listings =
            await _listingRepository.GetByAgencyIdForDashboardReadOnlyAsync(
                query.AgencyId,
                query.Status,
                query.Page,
                query.PageSize,
                cancellationToken);

        var responseItems = listings.Items
            .Select(listing => listing.ToResponse(languageCode))
            .ToList();

        var response = new PagedResult<ListingResponse>(
            responseItems,
            listings.Page,
            listings.PageSize,
            listings.TotalCount);

        return ServiceResult<PagedResult<ListingResponse>>.Success(response);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }
}