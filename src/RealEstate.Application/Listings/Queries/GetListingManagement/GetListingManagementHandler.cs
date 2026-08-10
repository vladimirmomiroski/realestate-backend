using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Queries.GetListingManagement;

public sealed class GetListingManagementHandler
{
    private readonly IListingAuthoringRepository _listingAuthoringRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;

    public GetListingManagementHandler(
        IListingAuthoringRepository listingAuthoringRepository,
        IUserRepository userRepository,
        AgencyListingAccessChecker agencyListingAccessChecker,
        ICurrentUserService currentUserService)
    {
        _listingAuthoringRepository = listingAuthoringRepository;
        _userRepository = userRepository;
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<ListingAuthoringResponse>> HandleAsync(
        GetListingManagementQuery query,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = _currentUserService.UserId;

        if (!currentUserId.HasValue)
        {
            return ServiceResult<ListingAuthoringResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        Guid userId = currentUserId.Value;

        var user = await _userRepository.GetByIdReadOnlyAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            return ServiceResult<ListingAuthoringResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (user.Status == UserStatus.Disabled)
        {
            return ServiceResult<ListingAuthoringResponse>.Forbidden(
                "User is not allowed to manage listings.",
                ErrorCodes.AuthorizationAccountDisabled);
        }

        var listing = await _listingAuthoringRepository.GetByIdReadOnlyAsync(
            query.ListingId,
            cancellationToken);

        if (listing is null)
        {
            return ServiceResult<ListingAuthoringResponse>.NotFound(
                "Listing was not found.",
                ErrorCodes.ResourceNotFound);
        }

        if (listing.AgencyId.HasValue)
        {
            ServiceResult<ListingAuthoringResponse>? agencyAccessResult =
                await _agencyListingAccessChecker
                    .EnsureCanManageAgencyListingsAsync<ListingAuthoringResponse>(
                        listing.AgencyId.Value,
                        userId,
                        "User is not allowed to manage listings for this agency.",
                        cancellationToken);

            if (agencyAccessResult is not null)
            {
                return agencyAccessResult;
            }
        }
        else if (listing.CreatedByUserId != userId)
        {
            return ServiceResult<ListingAuthoringResponse>.Forbidden(
                "User is not allowed to manage this listing.",
                ErrorCodes.AuthorizationForbidden);
        }

        return ServiceResult<ListingAuthoringResponse>.Success(
            listing.ToAuthoringResponse());
    }
}
