using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.ConfirmListingLocation;

public sealed class ConfirmListingLocationHandler
{
    private const string ConfirmationTokenValidationKey =
        "confirmationToken";

    private readonly IListingAuthoringRepository _listingAuthoringRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingGeocoder _listingGeocoder;
    private readonly ILocationConfirmationTokenProtector _tokenProtector;
    private readonly TimeProvider _timeProvider;

    public ConfirmListingLocationHandler(
        IListingAuthoringRepository listingAuthoringRepository,
        IUserRepository userRepository,
        AgencyListingAccessChecker agencyListingAccessChecker,
        ICurrentUserService currentUserService,
        IListingGeocoder listingGeocoder,
        ILocationConfirmationTokenProtector tokenProtector,
        TimeProvider timeProvider)
    {
        _listingAuthoringRepository = listingAuthoringRepository;
        _userRepository = userRepository;
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _currentUserService = currentUserService;
        _listingGeocoder = listingGeocoder;
        _tokenProtector = tokenProtector;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<ListingLocationStateResponse>> HandleAsync(
        ConfirmListingLocationCommand command,
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

        Listing? currentListing = await _listingAuthoringRepository
            .GetByIdReadOnlyAsync(command.ListingId, cancellationToken);

        if (currentListing is null)
        {
            return ListingNotFound();
        }

        ServiceResult<ListingLocationStateResponse>? accessFailure =
            await EnsureCanManageListingAsync(
                currentListing,
                userId,
                cancellationToken);

        if (accessFailure is not null)
        {
            return accessFailure;
        }

        if (currentListing.Status != ListingStatus.Draft)
        {
            return DraftRequired();
        }

        LocationConfirmationTokenUnprotectResult tokenResult =
            _tokenProtector.Unprotect(command.ConfirmationToken);

        if (!tokenResult.Succeeded || tokenResult.Payload is null)
        {
            return InvalidToken();
        }

        LocationConfirmationTokenPayload token = tokenResult.Payload;

        if (token.ListingId != command.ListingId)
        {
            return InvalidToken();
        }

        if (token.ActorUserId != userId)
        {
            return ServiceResult<ListingLocationStateResponse>.Forbidden(
                "The location confirmation token is not valid for the current user.",
                ErrorCodes.AuthorizationForbidden);
        }

        if (!TryComputeCurrentFingerprint(
                currentListing,
                token.LanguageCode,
                out string? currentFingerprint) ||
            !string.Equals(
                currentFingerprint,
                token.LocationFingerprint,
                StringComparison.Ordinal))
        {
            return StaleSelection();
        }

        GeocodingResolutionResult resolution =
            await _listingGeocoder.ResolveAsync(
                new GeocodingReference(
                    token.ProviderKey,
                    token.ProviderResultReference),
                cancellationToken);

        if (!resolution.Succeeded || resolution.Snapshot is null)
        {
            return MapProviderFailure(resolution.Outcome);
        }

        ResolvedGeocodingSnapshot snapshot = resolution.Snapshot;

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

            accessFailure = await EnsureCanManageListingAsync(
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

            if (!TryComputeCurrentFingerprint(
                    lockedListing,
                    token.LanguageCode,
                    out string? lockedFingerprint) ||
                !string.Equals(
                    lockedFingerprint,
                    token.LocationFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.ProviderKey,
                    token.ProviderKey,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.ResultReference,
                    token.ProviderResultReference,
                    StringComparison.Ordinal))
            {
                return StaleSelection();
            }

            lockedListing.ConfirmLocation(
                snapshot.Latitude,
                snapshot.Longitude,
                snapshot.Precision,
                snapshot.ProviderKey,
                snapshot.ResultReference,
                snapshot.DisplayName,
                _timeProvider.GetUtcNow().UtcDateTime);

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

    private static bool TryComputeCurrentFingerprint(
        Listing listing,
        string selectedLanguageCode,
        out string? fingerprint)
    {
        CanonicalListingLocation canonicalLocation =
            CanonicalListingLocation.From(listing.Translations.Select(
                translation => new CanonicalListingLocationInput(
                    translation.LanguageCode,
                    translation.City,
                    translation.Municipality,
                    translation.AddressLine,
                    translation.Neighborhood)));

        CanonicalListingLocationTranslation? selectedLocation =
            canonicalLocation.Translations.SingleOrDefault(translation =>
                string.Equals(
                    translation.LanguageCode,
                    selectedLanguageCode,
                    StringComparison.Ordinal));

        if (selectedLocation?.City is null ||
            selectedLocation.Municipality is null ||
            selectedLocation.AddressLine is null)
        {
            fingerprint = null;
            return false;
        }

        fingerprint = ListingLocationFingerprint.Compute(canonicalLocation);
        return true;
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

    private static ServiceResult<ListingLocationStateResponse> MapProviderFailure(
        GeocodingResolutionOutcome outcome)
    {
        return outcome switch
        {
            GeocodingResolutionOutcome.RateLimited =>
                ServiceResult<ListingLocationStateResponse>.RateLimited(
                    "Too many location requests were made. Try again later.",
                    ErrorCodes.RateLimitGeocodingExceeded),
            GeocodingResolutionOutcome.NotFound or
                GeocodingResolutionOutcome.Stale => StaleSelection(),
            _ => ServiceResult<ListingLocationStateResponse>
                .DependencyUnavailable(
                    "Location search and confirmation are temporarily unavailable.",
                    ErrorCodes.DependencyGeocodingUnavailable)
        };
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
            "Location can only be confirmed for draft listings.",
            ErrorCodes.ConflictResourceState);
    }

    private static ServiceResult<ListingLocationStateResponse> InvalidToken()
    {
        return ServiceResult<ListingLocationStateResponse>.ValidationError(
            "The location confirmation token is invalid or expired.",
            ConfirmationTokenValidationKey,
            ErrorCodes.ValidationFailed);
    }

    private static ServiceResult<ListingLocationStateResponse> StaleSelection()
    {
        return ServiceResult<ListingLocationStateResponse>.Conflict(
            "The location selection no longer matches the current listing location.",
            ErrorCodes.ConflictResourceSetChanged);
    }
}
