using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UpdateListing;

public sealed class UpdateListingHandler
{
    private readonly IListingAuthoringRepository _listingAuthoringRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;
    private readonly UpdateListingValidator _validator;
    private readonly ListingDraftReplacementEngine _replacementEngine;

    public UpdateListingHandler(
        IListingAuthoringRepository listingAuthoringRepository,
        IUserRepository userRepository,
        AgencyListingAccessChecker agencyListingAccessChecker,
        ICurrentUserService currentUserService,
        UpdateListingValidator validator,
        ListingDraftReplacementEngine replacementEngine)
    {
        _listingAuthoringRepository = listingAuthoringRepository;
        _userRepository = userRepository;
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _currentUserService = currentUserService;
        _validator = validator;
        _replacementEngine = replacementEngine;
    }

    public async Task<ServiceResult<ListingAuthoringResponse>> HandleAsync(
        Guid listingId,
        UpdateListingRequest request,
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

        User? user = await _userRepository.GetByIdReadOnlyAsync(
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
                "User is not allowed to update listings.",
                ErrorCodes.AuthorizationAccountDisabled);
        }

        IListingAuthoringWriteScope? writeScope =
            await _listingAuthoringRepository.BeginWriteAsync(
                listingId,
                cancellationToken);

        if (writeScope is null)
        {
            return ServiceResult<ListingAuthoringResponse>.NotFound(
                "Listing was not found.",
                ErrorCodes.ResourceNotFound);
        }

        await using (writeScope)
        {
            Listing listing = writeScope.Listing;

            if (listing.AgencyId.HasValue)
            {
                ServiceResult<ListingAuthoringResponse>? agencyAccessResult =
                    await _agencyListingAccessChecker
                        .EnsureCanManageAgencyListingsAsync<ListingAuthoringResponse>(
                            listing.AgencyId.Value,
                            userId,
                            "User is not allowed to update listings for this agency.",
                            cancellationToken);

                if (agencyAccessResult is not null)
                {
                    return agencyAccessResult;
                }
            }
            else if (listing.CreatedByUserId != userId)
            {
                return ServiceResult<ListingAuthoringResponse>.Forbidden(
                    "User is not allowed to update this listing.",
                    ErrorCodes.AuthorizationForbidden);
            }

            if (listing.Status != ListingStatus.Draft)
            {
                return ServiceResult<ListingAuthoringResponse>.Conflict(
                    "Only draft listings can be updated.",
                    ErrorCodes.ConflictResourceState);
            }

            UpdateListingValidator.ValidationFailure? validationFailure =
                _validator.ValidateWithKey(request);

            if (validationFailure is not null)
            {
                return ServiceResult<ListingAuthoringResponse>.ValidationError(
                    validationFailure.Error,
                    validationFailure.Key,
                    ErrorCodes.ValidationFailed);
            }

            _validator.Normalize(request);
            _replacementEngine.Apply(writeScope, request);

            await writeScope.SaveChangesAsync(cancellationToken);
            await writeScope.CommitAsync(cancellationToken);

            return ServiceResult<ListingAuthoringResponse>.Success(
                listing.ToAuthoringResponse());
        }
    }
}
