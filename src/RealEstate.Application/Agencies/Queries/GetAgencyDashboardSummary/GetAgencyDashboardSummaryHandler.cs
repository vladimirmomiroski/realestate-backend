using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyDashboardSummary;

public sealed class GetAgencyDashboardSummaryHandler
{
    private readonly AgencyListingAccessChecker
        _agencyListingAccessChecker;

    private readonly IAgencyRepository _agencyRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetAgencyDashboardSummaryHandler(
        AgencyListingAccessChecker agencyListingAccessChecker,
        IAgencyRepository agencyRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _agencyListingAccessChecker = agencyListingAccessChecker;
        _agencyRepository = agencyRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<AgencyDashboardSummaryResponse>>
        HandleAsync(
            Guid agencyId,
            CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid userId)
        {
            return ServiceResult<AgencyDashboardSummaryResponse>
                .Unauthorized(
                    "Current user could not be resolved.",
                    ErrorCodes.AuthenticationInvalidPrincipal);
        }

        User? currentUser =
            await _userRepository.GetByIdReadOnlyAsync(
                userId,
                cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<AgencyDashboardSummaryResponse>
                .Unauthorized(
                    "Current user could not be resolved.",
                    ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return ServiceResult<AgencyDashboardSummaryResponse>
                .Forbidden(
                    "User is not allowed to view the agency dashboard.",
                    ErrorCodes.AuthorizationAccountDisabled);
        }

        ServiceResult<AgencyDashboardSummaryResponse>? accessFailure =
            await _agencyListingAccessChecker
                .EnsureCanManageAgencyListingsAsync
                    <AgencyDashboardSummaryResponse>(
                        agencyId,
                        userId,
                        "User is not allowed to view the agency dashboard.",
                        cancellationToken);

        if (accessFailure is not null)
        {
            return accessFailure;
        }

        AgencyDashboardSummaryReadModel? summary =
            await _agencyRepository
                .GetDashboardSummaryReadOnlyAsync(
                    agencyId,
                    DateTime.UtcNow,
                    cancellationToken);

        if (summary is null)
        {
            return ServiceResult<AgencyDashboardSummaryResponse>
                .NotFound(
                    "Agency was not found.",
                    ErrorCodes.ResourceNotFound);
        }

        var response = new AgencyDashboardSummaryResponse
        {
            AgencyId = summary.AgencyId,
            AgencyName = summary.AgencyName,
            AgencyStatus = summary.AgencyStatus,
            TotalListings = summary.TotalListings,
            DraftListings = summary.DraftListings,
            ActiveListings = summary.ActiveListings,
            ArchivedListings = summary.ArchivedListings,
            MembersCount = summary.MembersCount,
            ActiveMembersCount = summary.ActiveMembersCount,
            PendingInvitationsCount =
                summary.PendingInvitationsCount
        };

        return ServiceResult<AgencyDashboardSummaryResponse>
            .Success(response);
    }
}
