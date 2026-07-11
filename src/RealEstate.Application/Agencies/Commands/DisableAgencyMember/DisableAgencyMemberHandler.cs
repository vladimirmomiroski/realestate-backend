using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.DisableAgencyMember;

public sealed class DisableAgencyMemberHandler
{
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;
    private readonly IAgencyRepository _agencyRepository;

    public DisableAgencyMemberHandler(
        AgencyAdminAccessChecker agencyAdminAccessChecker,
        IAgencyRepository agencyRepository)
    {
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
        _agencyRepository = agencyRepository;
    }

    public async Task<ServiceResult<bool>> HandleAsync(
        Guid agencyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<bool> accessResult =
            await _agencyAdminAccessChecker
                .EnsureCurrentUserIsActiveOwnerAsync<bool>(
                    agencyId,
                    "Only active agency owners can disable agency members.",
                    cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        AgencyMember? member =
            await _agencyRepository.GetMemberByIdForUpdateAsync(
                agencyId,
                memberId,
                cancellationToken);

        if (member is null)
        {
            return ServiceResult<bool>.NotFound(
                "Agency member was not found.");
        }

        if (member.UserId == accessResult.CurrentUserId)
        {
            return ServiceResult<bool>.ValidationError(
                "Agency owners cannot disable themselves.");
        }

        if (member.Status == AgencyMemberStatus.Disabled)
        {
            return ServiceResult<bool>.Success(true);
        }

        member.Disable();

        await _agencyRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }
}
