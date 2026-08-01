using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Queries.GetListings;

public sealed class GetListingsHandler
{
    private readonly IListingRepository _listingRepository;
    private readonly GetListingsValidator _validator;

    public GetListingsHandler(
        IListingRepository listingRepository,
        GetListingsValidator validator)
    {
        _listingRepository = listingRepository;
        _validator = validator;
    }

    public async Task<ServiceResult<PagedResponse<ListingResponse>>> HandleAsync(
        GetListingsQuery query,
        CancellationToken cancellationToken)
    {
        query.LanguageCode =
            EffectiveTranslationOrdering.NormalizeRequestedLanguageCode(
                query.LanguageCode);

        query.Page = query.Page < 1
            ? 1
            : query.Page;

        query.PageSize = query.PageSize < 1
            ? 20
            : query.PageSize;

        query.PageSize = query.PageSize > 100
            ? 100
            : query.PageSize;

        query.Sort = query.Sort is null
            ? "newest"
            : query.Sort.Trim();

        query.Currency = query.Currency is null
            ? null
            : query.Currency.Trim().ToUpperInvariant();

        query.SearchText =
            NormalizeOptionalSearchText(query.SearchText);

        query.City = NormalizeOptionalLocation(query.City);
        query.Municipality = NormalizeOptionalLocation(query.Municipality);
        query.Neighborhood = NormalizeOptionalLocation(query.Neighborhood);

        GetListingsValidator.ValidationFailure? validationError =
            _validator.ValidateWithKey(query);

        if (validationError is not null)
        {
            return ServiceResult<PagedResponse<ListingResponse>>
                .ValidationError(
                    validationError.Error,
                    validationError.Key,
                    ErrorCodes.ValidationFailed);
        }

        if (!ListingSortOptionParser.TryParse(
                query.Sort,
                out ListingSortOption sortOption))
        {
            return ServiceResult<PagedResponse<ListingResponse>>
                .ValidationError(
                    GetListingsValidator.InvalidSortError,
                    "sort",
                    ErrorCodes.ValidationFailed);
        }

        query.SortOption = sortOption;

        PagedResult<Listing> pagedListings =
            await _listingRepository.GetFilteredReadOnlyAsync(
                query,
                cancellationToken);

        PagedResponse<ListingResponse> response =
            PagedResponse<ListingResponse>.From(
                pagedListings,
                listing => listing.ToResponse(query.LanguageCode));

        return ServiceResult<PagedResponse<ListingResponse>>
            .Success(response);
    }

    private static string? NormalizeOptionalLocation(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeOptionalSearchText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
