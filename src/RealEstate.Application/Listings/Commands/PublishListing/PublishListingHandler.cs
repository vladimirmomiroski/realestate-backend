using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.PublishListing;

public sealed class PublishListingHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAgencyRepository _agencyRepository;
    private readonly ICurrentUserService _currentUserService;

    public PublishListingHandler(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _agencyRepository = agencyRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<ListingResponse>> HandleAsync(
        PublishListingCommand command,
        CancellationToken cancellationToken)
    {
        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        var user = await _userRepository.GetByIdReadOnlyAsync(userId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return ServiceResult<ListingResponse>.Forbidden(
                "User is not allowed to publish listings.");
        }

        var listing = await _listingRepository.GetByIdForUpdateAsync(
            command.ListingId,
            cancellationToken);

        if (listing is null)
        {
            return ServiceResult<ListingResponse>.NotFound("Listing was not found.");
        }

        if (listing.AgencyId.HasValue)
        {
            var agencyAccessResult = await EnsureAgencyPublishingAccessAsync(
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
            return ServiceResult<ListingResponse>.Forbidden(
                "User is not allowed to publish this listing.");
        }

        try
        {
            listing.Publish();
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<ListingResponse>.ValidationError(exception.Message);
        }

        await _listingRepository.SaveChangesAsync(cancellationToken);

        var languageCode = NormalizeLanguageCode(command.LanguageCode);

        return ServiceResult<ListingResponse>.Success(
            listing.ToResponse(languageCode));
    }

    private async Task<ServiceResult<ListingResponse>?> EnsureAgencyPublishingAccessAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var agency = await _agencyRepository.GetByIdReadOnlyAsync(
            agencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<ListingResponse>.NotFound("Agency was not found.");
        }

        if (agency.Status != AgencyStatus.Active)
        {
            return ServiceResult<ListingResponse>.Forbidden(
                "Agency is not allowed to publish listings.");
        }

        var memberAccess = await _agencyRepository.GetMemberAccessReadOnlyAsync(
            agencyId,
            userId,
            cancellationToken);

        if (memberAccess is null ||
            memberAccess.Status != AgencyMemberStatus.Active)
        {
            return ServiceResult<ListingResponse>.Forbidden(
                "User is not an active member of this agency.");
        }

        if (memberAccess.Role != AgencyMemberRole.Owner &&
            memberAccess.Role != AgencyMemberRole.Agent)
        {
            return ServiceResult<ListingResponse>.Forbidden(
                "User is not allowed to publish listings for this agency.");
        }

        return null;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }
}
