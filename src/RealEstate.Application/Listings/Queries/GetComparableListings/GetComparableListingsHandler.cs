using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Queries.GetComparableListings;

public sealed class GetComparableListingsHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly GetComparableListingsValidator _validator;

    public GetComparableListingsHandler(
        IListingRepository listingRepository,
        GetComparableListingsValidator validator)
    {
        _listingRepository = listingRepository;
        _validator = validator;
    }

    public async Task<ServiceResult<IReadOnlyList<PublicListingResponse>>>
        HandleAsync(
            GetComparableListingsQuery query,
            CancellationToken cancellationToken)
    {
        query.LanguageCode =
            EffectiveTranslationOrdering
                .NormalizeRequestedLanguageCode(
                    query.LanguageCode);

        GetComparableListingsValidator.ValidationFailure? validationError =
            _validator.ValidateWithKey(query);

        if (validationError is not null)
        {
            return ServiceResult<IReadOnlyList<PublicListingResponse>>
                .ValidationError(
                    validationError.Error,
                    validationError.Key,
                    ErrorCodes.ValidationFailed);
        }

        ComparableListingsReadResult readResult =
            await _listingRepository
                .GetComparableListingsReadOnlyAsync(
                    query.ListingId,
                    query.LanguageCode,
                    query.Limit,
                    cancellationToken);

        if (!readResult.SourceFound)
        {
            return ServiceResult<IReadOnlyList<PublicListingResponse>>
                .NotFound(
                    "Listing was not found.",
                    ErrorCodes.ResourceNotFound);
        }

        if (readResult.SourceIntegrityViolations.Count > 0)
        {
            throw new PublicListingIntegrityException(
                query.ListingId,
                readResult.SourceIntegrityViolations);
        }

        IReadOnlyList<PublicListingResponse> responses =
            readResult.Items
                .Select(listing =>
                    listing.ToPublicResponse(query.LanguageCode))
                .ToList();

        return ServiceResult<IReadOnlyList<PublicListingResponse>>
            .Success(responses);
    }
}
