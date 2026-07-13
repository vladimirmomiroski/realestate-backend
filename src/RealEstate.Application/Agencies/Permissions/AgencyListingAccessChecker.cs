using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Permissions;

public sealed class AgencyListingAccessChecker
{
    private readonly IAgencyRepository _agencyRepository;

    public AgencyListingAccessChecker(IAgencyRepository agencyRepository)
    {
        _agencyRepository = agencyRepository;
    }

    public Task<ServiceResult<TResponse>?> EnsureCanPublishAgencyListingsAsync<TResponse>(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return EnsureCanAccessAgencyListingsAsync<TResponse>(
            agencyId,
            userId,
            requireActiveAgency: true,
            inactiveAgencyError: "Agency is not allowed to publish listings.",
            forbiddenRoleError: "User is not allowed to publish listings for this agency.",
            cancellationToken);
    }

    public Task<ServiceResult<TResponse>?> EnsureCanManageAgencyListingsAsync<TResponse>(
        Guid agencyId,
        Guid userId,
        string forbiddenRoleError,
        CancellationToken cancellationToken)
    {
        return EnsureCanAccessAgencyListingsAsync<TResponse>(
            agencyId,
            userId,
            requireActiveAgency: false,
            inactiveAgencyError: null,
            forbiddenRoleError,
            cancellationToken);
    }

    private async Task<ServiceResult<TResponse>?> EnsureCanAccessAgencyListingsAsync<TResponse>(
        Guid agencyId,
        Guid userId,
        bool requireActiveAgency,
        string? inactiveAgencyError,
        string forbiddenRoleError,
        CancellationToken cancellationToken)
    {
        var agency = await _agencyRepository.GetByIdReadOnlyAsync(
            agencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<TResponse>.NotFound("Agency was not found.");
        }

        if (requireActiveAgency && agency.Status != AgencyStatus.Active)
        {
            return ServiceResult<TResponse>.Forbidden(inactiveAgencyError!);
        }

        var memberAccess = await _agencyRepository.GetMemberAccessReadOnlyAsync(
            agencyId,
            userId,
            cancellationToken);

        if (memberAccess is null ||
            memberAccess.Status != AgencyMemberStatus.Active)
        {
            return ServiceResult<TResponse>.Forbidden(
                "User is not an active member of this agency.");
        }

        if (memberAccess.Role != AgencyMemberRole.Owner &&
            memberAccess.Role != AgencyMemberRole.Agent)
        {
            return ServiceResult<TResponse>.Forbidden(forbiddenRoleError);
        }

        return null;
    }
}