using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Queries.GetAgencyById;

public sealed class GetAgencyByIdHandler
{
    private readonly IAgencyRepository _agencyRepository;

    public GetAgencyByIdHandler(IAgencyRepository agencyRepository)
    {
        _agencyRepository = agencyRepository;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        Agency? agency = await _agencyRepository.GetByIdReadOnlyAsync(
            agencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<AgencyResponse>.NotFound("Agency was not found.");
        }

        return ServiceResult<AgencyResponse>.Success(agency.ToResponse());
    }
}
