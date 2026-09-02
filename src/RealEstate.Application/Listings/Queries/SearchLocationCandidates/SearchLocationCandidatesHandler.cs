using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Queries.SearchLocationCandidates;

public sealed class SearchLocationCandidatesHandler
{
    private const string LocationValidationKey = "location";

    private readonly IListingAuthoringRepository _listingAuthoringRepository;
    private readonly IUserRepository _userRepository;
    private readonly AgencyListingAccessChecker _agencyListingAccessChecker;
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingGeocoder _listingGeocoder;
    private readonly ILocationConfirmationTokenProtector _tokenProtector;

    public SearchLocationCandidatesHandler(
        IListingAuthoringRepository listingAuthoringRepository,
        IUserRepository userRepository,
        AgencyListingAccessChecker agencyListingAccessChecker,
        ICurrentUserService currentUserService,
        IListingGeocoder listingGeocoder,
        ILocationConfirmationTokenProtector tokenProtector)
    {
        _listingAuthoringRepository = listingAuthoringRepository;
        _userRepository = userRepository;
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _currentUserService = currentUserService;
        _listingGeocoder = listingGeocoder;
        _tokenProtector = tokenProtector;
    }

    public async Task<ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>>
        HandleAsync(
            SearchLocationCandidatesQuery query,
            CancellationToken cancellationToken)
    {
        Guid? currentUserId = _currentUserService.UserId;

        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        Guid userId = currentUserId.Value;

        User? user = await _userRepository.GetByIdReadOnlyAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        if (user.Status == UserStatus.Disabled)
        {
            return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
                .Forbidden(
                    "User is not allowed to manage listing locations.",
                    ErrorCodes.AuthorizationAccountDisabled);
        }

        Listing? listing = await _listingAuthoringRepository
            .GetByIdReadOnlyAsync(query.ListingId, cancellationToken);

        if (listing is null)
        {
            return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
                .NotFound(
                    "Listing was not found.",
                    ErrorCodes.ResourceNotFound);
        }

        if (listing.AgencyId.HasValue)
        {
            ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>?
                agencyAccessResult = await _agencyListingAccessChecker
                    .EnsureCanManageAgencyListingsAsync<
                        IReadOnlyList<ListingLocationCandidateResponse>>(
                        listing.AgencyId.Value,
                        userId,
                        "User is not allowed to manage listing locations for this agency.",
                        cancellationToken);

            if (agencyAccessResult is not null)
            {
                return agencyAccessResult;
            }
        }
        else if (listing.CreatedByUserId != userId)
        {
            return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
                .Forbidden(
                    "User is not allowed to manage this listing.",
                    ErrorCodes.AuthorizationForbidden);
        }

        if (listing.Status != ListingStatus.Draft)
        {
            return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
                .Conflict(
                    "Location candidates can only be searched for draft listings.",
                    ErrorCodes.ConflictResourceState);
        }

        ListingTranslation? selectedTranslation =
            EffectiveTranslationOrdering.SelectEffectiveTranslation(
                listing.Translations,
                query.LanguageCode);

        if (selectedTranslation is null)
        {
            return IncompleteLocation();
        }

        CanonicalListingLocation canonicalLocation =
            CreateCanonicalLocation(listing.Translations);

        string selectedLanguageCode =
            ListingTranslationRules.NormalizeLanguageCode(
                selectedTranslation.LanguageCode);

        CanonicalListingLocationTranslation selectedLocation =
            canonicalLocation.Translations.Single(translation =>
                string.Equals(
                    translation.LanguageCode,
                    selectedLanguageCode,
                    StringComparison.Ordinal));

        if (selectedLocation.City is null ||
            selectedLocation.Municipality is null ||
            selectedLocation.AddressLine is null)
        {
            return IncompleteLocation();
        }

        string locationFingerprint =
            ListingLocationFingerprint.Compute(canonicalLocation);

        GeocodingSearchResult geocodingResult =
            await _listingGeocoder.SearchAsync(
                new GeocodingSearchInput(selectedLocation),
                cancellationToken);

        if (!geocodingResult.Succeeded)
        {
            return MapProviderFailure(geocodingResult.Outcome);
        }

        var responses = new List<ListingLocationCandidateResponse>(
            geocodingResult.Candidates.Count);

        foreach (GeocodingCandidate candidate in geocodingResult.Candidates)
        {
            LocationConfirmationTokenClaims claims =
                LocationConfirmationTokenClaims.Create(
                    listing.Id,
                    userId,
                    candidate.ProviderKey,
                    candidate.ResultReference,
                    selectedLocation.LanguageCode,
                    locationFingerprint);

            string confirmationToken = _tokenProtector.Protect(claims);

            responses.Add(new ListingLocationCandidateResponse(
                candidate.DisplayLabel,
                candidate.Latitude,
                candidate.Longitude,
                candidate.Precision,
                confirmationToken));
        }

        return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
            .Success(responses.AsReadOnly());
    }

    private static CanonicalListingLocation CreateCanonicalLocation(
        IEnumerable<ListingTranslation> translations)
    {
        return CanonicalListingLocation.From(translations.Select(translation =>
            new CanonicalListingLocationInput(
                translation.LanguageCode,
                translation.City,
                translation.Municipality,
                translation.AddressLine,
                translation.Neighborhood)));
    }

    private static ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
        Unauthorized()
    {
        return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
            .Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
    }

    private static ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
        IncompleteLocation()
    {
        return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
            .ValidationError(
                "The selected translation requires City, Municipality, and AddressLine before location search.",
                LocationValidationKey,
                ErrorCodes.ValidationFailed);
    }

    private static ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
        MapProviderFailure(GeocodingSearchOutcome outcome)
    {
        if (outcome == GeocodingSearchOutcome.RateLimited)
        {
            return ServiceResult<
                    IReadOnlyList<ListingLocationCandidateResponse>>
                .RateLimited(
                    "Too many location requests were made. Try again later.",
                    ErrorCodes.RateLimitGeocodingExceeded);
        }

        return ServiceResult<IReadOnlyList<ListingLocationCandidateResponse>>
            .DependencyUnavailable(
                "Location search and confirmation are temporarily unavailable.",
                ErrorCodes.DependencyGeocodingUnavailable);
    }
}
