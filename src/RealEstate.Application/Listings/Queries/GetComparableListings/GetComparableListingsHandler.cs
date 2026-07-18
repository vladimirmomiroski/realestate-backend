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

    public async Task<ServiceResult<IReadOnlyList<ListingResponse>>>
        HandleAsync(
            GetComparableListingsQuery query,
            CancellationToken cancellationToken)
    {
        query.LanguageCode =
            EffectiveTranslationOrdering
                .NormalizeRequestedLanguageCode(
                    query.LanguageCode);

        string? validationError =
            _validator.Validate(query);

        if (validationError is not null)
        {
            return ServiceResult<IReadOnlyList<ListingResponse>>
                .ValidationError(validationError);
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
            return ServiceResult<IReadOnlyList<ListingResponse>>
                .NotFound("Listing was not found.");
        }

        IReadOnlyList<ListingResponse> responses =
            readResult.Items
                .Select(listing =>
                    listing.ToResponse(query.LanguageCode))
                .ToList();

        return ServiceResult<IReadOnlyList<ListingResponse>>
            .Success(responses);
    }
}
