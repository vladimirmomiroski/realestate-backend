using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;

namespace RealEstate.Application.Agencies.Queries.GetAgencyMembers;

public sealed class GetAgencyMembersHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetAgencyMembersHandler(
        IAgencyRepository agencyRepository,
        ICurrentUserService currentUserService)
    {
        _agencyRepository = agencyRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<IReadOnlyList<AgencyMemberResponse>>> HandleAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        bool agencyExists = await _agencyRepository.ExistsAsync(
            agencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.NotFound(
                "Agency was not found.");
        }

        bool isActiveMember = await _agencyRepository.IsActiveMemberAsync(
            agencyId,
            userId,
            cancellationToken);

        if (!isActiveMember)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Forbidden(
                "User is not an active member of this agency.");
        }

        IReadOnlyList<AgencyMemberReadModel> members =
            await _agencyRepository.GetMembersByAgencyIdReadOnlyAsync(
                agencyId,
                cancellationToken);

        IReadOnlyList<AgencyMemberResponse> response = members
            .Select(member => member.ToAgencyMemberResponse())
            .ToList();

        return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Success(response);
    }
}
