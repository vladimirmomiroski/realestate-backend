using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Commands.PublishListing;

public sealed class PublishListingHandler
{
    private readonly IListingAuthoringRepository _listingAuthoringRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;

    public PublishListingHandler(
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

    public async Task<ServiceResult<PublicListingResponse>> HandleAsync(
        PublishListingCommand command,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = _currentUserService.UserId;

        if (!currentUserId.HasValue)
        {
            return ServiceResult<PublicListingResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        Guid userId = currentUserId.Value;

        var user = await _userRepository.GetByIdReadOnlyAsync(userId, cancellationToken);

        if (user is null)
        {
            return ServiceResult<PublicListingResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (user.Status == UserStatus.Disabled)
        {
            return ServiceResult<PublicListingResponse>.Forbidden(
                "User is not allowed to publish listings.",
                ErrorCodes.AuthorizationAccountDisabled);
        }

        if (user.Status != UserStatus.Active)
        {
            return ServiceResult<PublicListingResponse>.Forbidden(
                "User is not allowed to publish listings.",
                ErrorCodes.AuthorizationForbidden);
        }

        IListingAuthoringWriteScope? writeScope =
            await _listingAuthoringRepository.BeginWriteAsync(
            command.ListingId,
            cancellationToken);

        if (writeScope is null)
        {
            return ServiceResult<PublicListingResponse>.NotFound(
                "Listing was not found.",
                ErrorCodes.ResourceNotFound);
        }

        await using (writeScope)
        {
            var listing = writeScope.Listing;

            if (listing.AgencyId.HasValue)
            {
                var agencyAccessResult =
                    await _agencyListingAccessChecker.EnsureCanPublishAgencyListingsAsync<PublicListingResponse>(
                        listing.AgencyId.Value,
                        userId,
                        cancellationToken);

                if (agencyAccessResult is not null)
                {
                    return agencyAccessResult;
                }
            }
            else if (listing.CreatedByUserId != userId)
            {
                return ServiceResult<PublicListingResponse>.Forbidden(
                    "User is not allowed to publish this listing.",
                    ErrorCodes.AuthorizationForbidden);
            }

            ListingPublicationReadinessResult readiness;

            try
            {
                readiness = listing.Publish();
            }
            catch (InvalidOperationException exception)
            {
                return ServiceResult<PublicListingResponse>.Conflict(
                    exception.Message,
                    ErrorCodes.ConflictResourceState);
            }

            if (!readiness.IsReady)
            {
                return ServiceResult<PublicListingResponse>.Conflict(
                    "The listing is not ready for publication.",
                    ErrorCodes.ConflictListingNotReady);
            }

            await writeScope.SaveChangesAsync(cancellationToken);

            await writeScope.CommitAsync(cancellationToken);

            var languageCode = NormalizeLanguageCode(command.LanguageCode);

            return ServiceResult<PublicListingResponse>.Success(
                listing.ToPublicResponse(languageCode));
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }
}
