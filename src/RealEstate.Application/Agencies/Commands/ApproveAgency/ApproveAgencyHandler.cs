using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Permissions;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Commands.ApproveAgency;

public sealed class ApproveAgencyHandler
{
    private readonly PlatformAdminAccessChecker _platformAdminAccessChecker;
    private readonly IAgencyRepository _agencyRepository;

    public ApproveAgencyHandler(
        PlatformAdminAccessChecker platformAdminAccessChecker,
        IAgencyRepository agencyRepository)
    {
        _platformAdminAccessChecker = platformAdminAccessChecker;
        _agencyRepository = agencyRepository;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse>? accessFailure =
            await _platformAdminAccessChecker
                .EnsureCurrentUserIsActiveAdminAsync<AgencyResponse>(
                    "Only active platform administrators can approve agencies.",
                    cancellationToken);

        if (accessFailure is not null)
        {
            return accessFailure;
        }

        Agency? agency =
            await _agencyRepository.GetByIdForUpdateAsync(
                agencyId,
                cancellationToken);

        if (agency is null)
        {
            return ServiceResult<AgencyResponse>.NotFound(
                "Agency was not found.");
        }

        try
        {
            agency.Approve();
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<AgencyResponse>.ValidationError(
                exception.Message);
        }

        await _agencyRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<AgencyResponse>.Success(
            agency.ToResponse());
    }
}
