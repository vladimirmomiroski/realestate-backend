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
        query.LanguageCode = string.IsNullOrWhiteSpace(query.LanguageCode)
            ? "mk"
            : query.LanguageCode.Trim().ToLower();

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

        string? validationError = _validator.Validate(query);

        if (validationError is not null)
        {
            return ServiceResult<PagedResponse<ListingResponse>>
                .ValidationError(validationError);
        }

        if (!ListingSortOptionParser.TryParse(
                query.Sort,
                out ListingSortOption sortOption))
        {
            return ServiceResult<PagedResponse<ListingResponse>>
                .ValidationError(GetListingsValidator.InvalidSortError);
        }

        query.SortOption = sortOption;

        PagedResult<Listing> pagedListings =
            await _listingRepository.GetFilteredReadOnlyAsync(
                query,
                cancellationToken);

        IReadOnlyList<ListingResponse> listingResponses =
            pagedListings.Items
                .Select(listing =>
                    listing.ToResponse(query.LanguageCode))
                .ToList();

        var response = new PagedResponse<ListingResponse>(
            listingResponses,
            query.Page,
            query.PageSize,
            pagedListings.TotalCount);

        return ServiceResult<PagedResponse<ListingResponse>>
            .Success(response);
    }
}