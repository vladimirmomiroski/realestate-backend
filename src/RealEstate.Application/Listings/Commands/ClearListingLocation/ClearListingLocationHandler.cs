using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.ClearListingLocation;

public sealed class ClearListingLocationHandler
{
    private readonly IListingAuthoringRepository _listingAuthoringRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;

    public ClearListingLocationHandler(
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

    public async Task<ServiceResult<ListingLocationStateResponse>> HandleAsync(
        ClearListingLocationCommand command,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = _currentUserService.UserId;

        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        Guid userId = currentUserId.Value;

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            userId,
            cancellationToken);

        ServiceResult<ListingLocationStateResponse>? userFailure =
            ValidateCurrentUser(currentUser);

        if (userFailure is not null)
        {
            return userFailure;
        }

        IListingAuthoringWriteScope? writeScope =
            await _listingAuthoringRepository.BeginWriteAsync(
                command.ListingId,
                cancellationToken);

        if (writeScope is null)
        {
            return ListingNotFound();
        }

        await using (writeScope)
        {
            User? lockedCurrentUser =
                await _userRepository.GetByIdReadOnlyAsync(
                    userId,
                    cancellationToken);

            userFailure = ValidateCurrentUser(lockedCurrentUser);

            if (userFailure is not null)
            {
                return userFailure;
            }

            Listing lockedListing = writeScope.Listing;

            ServiceResult<ListingLocationStateResponse>? accessFailure =
                await EnsureCanManageListingAsync(
                    lockedListing,
                    userId,
                    cancellationToken);

            if (accessFailure is not null)
            {
                return accessFailure;
            }

            if (lockedListing.Status != ListingStatus.Draft)
            {
                return DraftRequired();
            }

            lockedListing.ClearLocation();

            await writeScope.SaveChangesAsync(cancellationToken);
            await writeScope.CommitAsync(cancellationToken);

            return ServiceResult<ListingLocationStateResponse>.Success(
                ToLocationState(lockedListing));
        }
    }

    private async Task<ServiceResult<ListingLocationStateResponse>?>
        EnsureCanManageListingAsync(
            Listing listing,
            Guid userId,
            CancellationToken cancellationToken)
    {
        if (listing.AgencyId.HasValue)
        {
            return await _agencyListingAccessChecker
                .EnsureCanManageAgencyListingsAsync<
                    ListingLocationStateResponse>(
                    listing.AgencyId.Value,
                    userId,
                    "User is not allowed to manage listing locations for this agency.",
                    cancellationToken);
        }

        return listing.CreatedByUserId == userId
            ? null
            : ServiceResult<ListingLocationStateResponse>.Forbidden(
                "User is not allowed to manage this listing.",
                ErrorCodes.AuthorizationForbidden);
    }

    private static ServiceResult<ListingLocationStateResponse>?
        ValidateCurrentUser(User? user)
    {
        if (user is null)
        {
            return Unauthorized();
        }

        return user.Status == UserStatus.Disabled
            ? ServiceResult<ListingLocationStateResponse>.Forbidden(
                "User is not allowed to manage listing locations.",
                ErrorCodes.AuthorizationAccountDisabled)
            : null;
    }

    private static ListingLocationStateResponse ToLocationState(
        Listing listing)
    {
        return new ListingLocationStateResponse(
            listing.Latitude,
            listing.Longitude,
            listing.LocationPrecision,
            listing.GeocodedDisplayName,
            listing.LocationConfirmedAtUtc);
    }

    private static ServiceResult<ListingLocationStateResponse> Unauthorized()
    {
        return ServiceResult<ListingLocationStateResponse>.Unauthorized(
            "Current user could not be resolved.",
            ErrorCodes.AuthenticationInvalidPrincipal);
    }

    private static ServiceResult<ListingLocationStateResponse> ListingNotFound()
    {
        return ServiceResult<ListingLocationStateResponse>.NotFound(
            "Listing was not found.",
            ErrorCodes.ResourceNotFound);
    }

    private static ServiceResult<ListingLocationStateResponse> DraftRequired()
    {
        return ServiceResult<ListingLocationStateResponse>.Conflict(
            "Location can only be cleared for draft listings.",
            ErrorCodes.ConflictResourceState);
    }
}
