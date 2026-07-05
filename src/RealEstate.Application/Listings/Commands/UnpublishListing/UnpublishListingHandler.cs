using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UnpublishListing;

public sealed class UnpublishListingHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;

    public UnpublishListingHandler(
        IListingRepository listingRepository,
        IUserRepository userRepository,
        AgencyListingAccessChecker agencyListingAccessChecker,
        ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _userRepository = userRepository;
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<ListingResponse>> HandleAsync(
        UnpublishListingCommand command,
        CancellationToken cancellationToken)
    {
        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        var user = await _userRepository.GetByIdReadOnlyAsync(userId, cancellationToken);

        if (user is null || user.Status == UserStatus.Disabled)
        {
            return ServiceResult<ListingResponse>.Forbidden(
                "User is not allowed to unpublish listings.");
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
            var agencyAccessResult =
                await _agencyListingAccessChecker.EnsureCanManageAgencyListingsAsync<ListingResponse>(
                    listing.AgencyId.Value,
                    userId,
                    "User is not allowed to unpublish listings for this agency.",
                    cancellationToken);

            if (agencyAccessResult is not null)
            {
                return agencyAccessResult;
            }
        }
        else if (listing.CreatedByUserId != userId)
        {
            return ServiceResult<ListingResponse>.Forbidden(
                "User is not allowed to unpublish this listing.");
        }

        try
        {
            listing.Unpublish();
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

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }
}
