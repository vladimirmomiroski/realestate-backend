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
        AgencyAdminAccessResult<bool> accessResult =
            await _agencyAdminAccessChecker
                .EnsureCurrentUserIsActiveOwnerAsync<bool>(
                    agencyId,
                    "Only active agency owners can change member roles.",
                    cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        string? validationError = _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<bool>.ValidationError(validationError);
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

        if (member.Status != AgencyMemberStatus.Active)
        {
            return ServiceResult<bool>.ValidationError(
                "Only active agency members can have their role changed.");
        }

        AgencyMemberRole requestedRole = request!.Role;

        if (member.Role == requestedRole)
        {
            return ServiceResult<bool>.Success(true);
        }

        if (member.Role == AgencyMemberRole.Owner &&
            requestedRole == AgencyMemberRole.Agent)
        {
            int activeOwnerCount =
                await _agencyRepository.CountActiveOwnersAsync(
                    agencyId,
                    cancellationToken);

            if (activeOwnerCount <= 1)
            {
                return ServiceResult<bool>.ValidationError(
                    "Cannot demote the last active agency owner.");
            }
        }

        member.ChangeRole(requestedRole);

        await _agencyRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }
}
