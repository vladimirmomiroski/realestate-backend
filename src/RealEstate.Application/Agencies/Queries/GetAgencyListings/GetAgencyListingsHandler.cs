using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Queries.GetAgencyListings;

public sealed class GetAgencyListingsHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly IListingRepository _listingRepository;
    private readonly GetListingsValidator _getListingsValidator;

    public GetAgencyListingsHandler(
        IAgencyRepository agencyRepository,
        IListingRepository listingRepository,
        GetListingsValidator getListingsValidator)
    {
        _agencyRepository = agencyRepository;
        _listingRepository = listingRepository;
        _getListingsValidator = getListingsValidator;
    }

    public async Task<ServiceResult<PagedResponse<ListingResponse>>> HandleAsync(
        GetAgencyListingsQuery query,
        CancellationToken cancellationToken)
    {
        var listingsQuery = new GetListingsQuery
        {
            AgencyId = query.AgencyId,
            LanguageCode = NormalizeLanguageCode(query.LanguageCode),
            Sort = NormalizeSort(query.Sort),
            Currency = NormalizeCurrency(query.Currency),
            Page = NormalizePage(query.Page),
            PageSize = NormalizePageSize(query.PageSize)
        };

        GetListingsValidator.ValidationFailure? validationFailure =
            _getListingsValidator.ValidateWithKey(listingsQuery);

        if (validationFailure is not null)
        {
            return ServiceResult<PagedResponse<ListingResponse>>
                .ValidationError(
                    validationFailure.Error,
                    validationFailure.Key,
                    ErrorCodes.ValidationFailed);
        }

        if (!ListingSortOptionParser.TryParse(
                listingsQuery.Sort,
                out ListingSortOption sortOption))
        {
            return ServiceResult<PagedResponse<ListingResponse>>
                .ValidationError(
                    GetListingsValidator.InvalidSortError,
                    "sort",
                    ErrorCodes.ValidationFailed);
        }

        listingsQuery.SortOption = sortOption;

        bool agencyExists = await _agencyRepository.ExistsAsync(
            query.AgencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return ServiceResult<PagedResponse<ListingResponse>>.NotFound(
                "Agency was not found.",
                ErrorCodes.ResourceNotFound);
        }

        PagedResult<Listing> listings =
            await _listingRepository.GetFilteredReadOnlyAsync(
                listingsQuery,
                cancellationToken);

        PagedResponse<ListingResponse> response =
            PagedResponse<ListingResponse>.From(
                listings,
                listing =>
                    listing.ToResponse(listingsQuery.LanguageCode));

        return ServiceResult<PagedResponse<ListingResponse>>
            .Success(response);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }

    private static string NormalizeSort(string? sort)
    {
        return sort is null
            ? "newest"
            : sort.Trim();
    }

    private static string? NormalizeCurrency(string? currency)
    {
        return currency is null
            ? null
            : currency.Trim().ToUpperInvariant();
    }

    private static int NormalizePage(int page)
    {
        return page < 1
            ? 1
            : page;
    }

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            return 20;
        }

        return pageSize > 100
            ? 100
            : pageSize;
    }
}
