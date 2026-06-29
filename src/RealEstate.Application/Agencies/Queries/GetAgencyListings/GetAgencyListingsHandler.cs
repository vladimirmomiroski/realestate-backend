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

    public GetAgencyListingsHandler(
        IAgencyRepository agencyRepository,
        IListingRepository listingRepository)
    {
        _agencyRepository = agencyRepository;
        _listingRepository = listingRepository;
    }

    public async Task<ServiceResult<PagedResult<ListingResponse>>> HandleAsync(
        GetAgencyListingsQuery query,
        CancellationToken cancellationToken)
    {
        bool agencyExists = await _agencyRepository.ExistsAsync(
            query.AgencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return ServiceResult<PagedResult<ListingResponse>>.NotFound(
                "Agency was not found.");
        }

        string languageCode = NormalizeLanguageCode(query.LanguageCode);

        var listingsQuery = new GetListingsQuery
        {
            AgencyId = query.AgencyId,
            LanguageCode = languageCode,
            Page = query.Page,
            PageSize = query.PageSize
        };

        PagedResult<Listing> listings =
            await _listingRepository.GetFilteredReadOnlyAsync(
                listingsQuery,
                cancellationToken);

        var responseItems = listings.Items
            .Select(listing => listing.ToResponse(languageCode))
            .ToList();

        var response = new PagedResult<ListingResponse>(
            responseItems,
            listings.Page,
            listings.PageSize,
            listings.TotalCount);

        return ServiceResult<PagedResult<ListingResponse>>.Success(response);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }
}
