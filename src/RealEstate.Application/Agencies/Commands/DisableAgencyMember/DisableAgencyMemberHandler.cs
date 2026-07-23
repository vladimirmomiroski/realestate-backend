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
        const string forbiddenMessage =
            "Only active agency owners can disable agency members.";

        AgencyAdminAccessResult<bool> accessResult =
            await _agencyAdminAccessChecker
                .EnsureCurrentUserIsActiveOwnerAsync<bool>(
                    agencyId,
                    forbiddenMessage,
                    cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        IAgencyOwnerMutationScope? ownerMutationScope =
            await _agencyRepository
                .BeginLastActiveOwnerMutationAsync(
                    agencyId,
                    cancellationToken);

        if (ownerMutationScope is null)
        {
            return ServiceResult<bool>.NotFound(
                "Agency was not found.");
        }

        await using (ownerMutationScope)
        {
            var protectedActorAccess =
                await _agencyRepository
                    .GetMemberAccessReadOnlyAsync(
                        agencyId,
                        accessResult.CurrentUserId,
                        cancellationToken);

            if (protectedActorAccess is null ||
                protectedActorAccess.Status !=
                    AgencyMemberStatus.Active ||
                protectedActorAccess.Role !=
                    AgencyMemberRole.Owner)
            {
                return ServiceResult<bool>.Forbidden(
                    forbiddenMessage);
            }

            AgencyMember? member =
                await _agencyRepository
                    .GetMemberByIdForUpdateAsync(
                        agencyId,
                        memberId,
                        cancellationToken);

            if (member is null)
            {
                return ServiceResult<bool>.NotFound(
                    "Agency member was not found.");
            }

            if (member.UserId ==
                accessResult.CurrentUserId)
            {
                return ServiceResult<bool>.ValidationError(
                    "Agency owners cannot disable themselves.");
            }

            if (member.Status ==
                AgencyMemberStatus.Disabled)
            {
                await ownerMutationScope.CommitAsync(
                    cancellationToken);

                return ServiceResult<bool>.Success(true);
            }

            if (member.Status ==
                    AgencyMemberStatus.Active &&
                member.Role ==
                    AgencyMemberRole.Owner)
            {
                int activeOwnerCount =
                    await _agencyRepository
                        .CountActiveOwnersAsync(
                            agencyId,
                            cancellationToken);

                if (activeOwnerCount <= 1)
                {
                    return ServiceResult<bool>.ValidationError(
                        "Cannot disable the last active agency owner.");
                }
            }

            member.Disable();

            await _agencyRepository.SaveChangesAsync(
                cancellationToken);

            await ownerMutationScope.CommitAsync(
                cancellationToken);

            return ServiceResult<bool>.Success(true);
        }
    }
}
