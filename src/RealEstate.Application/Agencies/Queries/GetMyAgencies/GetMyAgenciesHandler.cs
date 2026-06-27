using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common.Authentication;

namespace RealEstate.Application.Agencies.Queries.GetMyAgencies;

public sealed class GetMyAgenciesHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyAgenciesHandler(
        IAgencyRepository agencyRepository,
        ICurrentUserService currentUserService)
    {
        _agencyRepository = agencyRepository;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<MyAgencyResponse>> HandleAsync(
        CancellationToken cancellationToken)
    {
        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        IReadOnlyList<UserAgencyMembershipReadModel> memberships =
            await _agencyRepository.GetByUserIdReadOnlyAsync(
                userId,
                cancellationToken);

        return memberships
            .Select(membership => membership.ToMyAgencyResponse())
            .ToList();
    }
}
