using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.ChangeAgencyMemberRole;

public sealed class ChangeAgencyMemberRoleHandler
{
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;
    private readonly ChangeAgencyMemberRoleValidator _validator;
    private readonly IAgencyRepository _agencyRepository;

    public ChangeAgencyMemberRoleHandler(
        AgencyAdminAccessChecker agencyAdminAccessChecker,
        ChangeAgencyMemberRoleValidator validator,
        IAgencyRepository agencyRepository)
    {
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
        _validator = validator;
        _agencyRepository = agencyRepository;
    }

    public async Task<ServiceResult<bool>> HandleAsync(
    Guid agencyId,
    Guid memberId,
    ChangeAgencyMemberRoleRequest? request,
    CancellationToken cancellationToken)
    {
        const string forbiddenMessage =
            "Only active agency owners can change member roles.";

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

        string? validationError =
            _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<bool>.ValidationError(
                validationError);
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

            if (member.Status !=
                AgencyMemberStatus.Active)
            {
                return ServiceResult<bool>.ValidationError(
                    "Only active agency members can have their role changed.");
            }

            AgencyMemberRole requestedRole =
                request!.Role;

            if (member.Role == requestedRole)
            {
                await ownerMutationScope.CommitAsync(
                    cancellationToken);

                return ServiceResult<bool>.Success(true);
            }

            if (member.Role ==
                    AgencyMemberRole.Owner &&
                requestedRole ==
                    AgencyMemberRole.Agent)
            {
                int activeOwnerCount =
                    await _agencyRepository
                        .CountActiveOwnersAsync(
                            agencyId,
                            cancellationToken);

                if (activeOwnerCount <= 1)
                {
                    return ServiceResult<bool>.ValidationError(
                        "Cannot demote the last active agency owner.");
                }
            }

            member.ChangeRole(requestedRole);

            await _agencyRepository.SaveChangesAsync(
                cancellationToken);

            await ownerMutationScope.CommitAsync(
                cancellationToken);

            return ServiceResult<bool>.Success(true);
        }
    }
}
