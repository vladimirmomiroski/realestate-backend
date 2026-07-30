using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Queries.GetAgencyBySlug;

public sealed class GetAgencyBySlugHandler
{
    private readonly IAgencyRepository _agencyRepository;

    public GetAgencyBySlugHandler(IAgencyRepository agencyRepository)
    {
        _agencyRepository = agencyRepository;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        string normalizedSlug = NormalizeSlug(slug);

        Agency? agency = await _agencyRepository.GetBySlugReadOnlyAsync(
            normalizedSlug,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<AgencyResponse>.NotFound(
                "Agency was not found.",
                ErrorCodes.ResourceNotFound);
        }

        return ServiceResult<AgencyResponse>.Success(agency.ToResponse());
    }

    private static string NormalizeSlug(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }
}
