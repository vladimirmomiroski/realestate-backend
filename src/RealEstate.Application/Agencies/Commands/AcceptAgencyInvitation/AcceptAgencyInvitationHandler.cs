using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.AcceptAgencyInvitation;

public sealed class AcceptAgencyInvitationHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAgencyInvitationRepository _agencyInvitationRepository;
    private readonly IAgencyRepository _agencyRepository;
    private readonly AcceptAgencyInvitationValidator _validator;

    public AcceptAgencyInvitationHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAgencyInvitationRepository agencyInvitationRepository,
        IAgencyRepository agencyRepository,
        AcceptAgencyInvitationValidator validator)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _agencyInvitationRepository = agencyInvitationRepository;
        _agencyRepository = agencyRepository;
        _validator = validator;
    }

    public async Task<
    ServiceResult<AgencyInvitationListItemResponse>>
    HandleAsync(
        AcceptAgencyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId
                is not Guid currentUserId)
        {
            return ServiceResult<
                AgencyInvitationListItemResponse>
                .Unauthorized(
                    "Current user could not be resolved.");
        }

        User? currentUser =
            await _userRepository.GetByIdReadOnlyAsync(
                currentUserId,
                cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<
                AgencyInvitationListItemResponse>
                .Unauthorized(
                    "Current user could not be resolved.");
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return ServiceResult<
                AgencyInvitationListItemResponse>
                .Forbidden(
                    "Disabled users cannot accept invitations.");
        }

        string? validationError =
            _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<
                AgencyInvitationListItemResponse>
                .ValidationError(validationError);
        }

        string token = request.Token.Trim();

        IAgencyInvitationTerminalMutationScope?
            terminalMutationScope =
                await _agencyInvitationRepository
                    .BeginTerminalMutationByTokenAsync(
                        token,
                        cancellationToken);

        if (terminalMutationScope is null)
        {
            return ServiceResult<
                AgencyInvitationListItemResponse>
                .NotFound(
                    "Invitation was not found.");
        }

        await using (terminalMutationScope)
        {
            AgencyInvitation invitation =
                terminalMutationScope.Invitation;

            if (invitation.Status ==
                AgencyInvitationStatus.Accepted)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .ValidationError(
                        "Invitation has already been accepted.");
            }

            if (invitation.Status ==
                AgencyInvitationStatus.Cancelled)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .ValidationError(
                        "Cancelled invitation cannot be accepted.");
            }

            if (invitation.Status ==
                AgencyInvitationStatus.Expired)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .ValidationError(
                        "Expired invitation cannot be accepted.");
            }

            DateTime utcNow = DateTime.UtcNow;

            if (invitation.ExpiresAtUtc <= utcNow)
            {
                invitation.MarkExpired(utcNow);

                await terminalMutationScope
                    .PersistTerminalTransitionAsync(
                        cancellationToken);

                await terminalMutationScope.CommitAsync(
                    cancellationToken);

                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .ValidationError(
                        "Expired invitation cannot be accepted.");
            }

            if (currentUser.NormalizedEmail !=
                invitation.NormalizedEmail)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .Forbidden(
                        "Invitation email does not match the current user.");
            }

            Agency? agency =
                await _agencyRepository
                    .GetByIdWithMembersForUpdateAsync(
                        invitation.AgencyId,
                        cancellationToken);

            if (agency is null)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .NotFound(
                        "Agency was not found.");
            }

            if (agency.Members.Any(member =>
                member.UserId == currentUserId))
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .ValidationError(
                        "User is already a member of this agency.");
            }

            AgencyMember member =
                agency.AddMember(
                    currentUserId,
                    invitation.Role,
                    AgencyMemberStatus.Active);

            _agencyRepository.AddMember(member);

            invitation.Accept(
                currentUserId,
                utcNow);

            AgencyInvitationAcceptancePersistenceResult
                persistenceResult =
                    await terminalMutationScope
                        .PersistAcceptanceAsync(
                            cancellationToken);

            if (persistenceResult ==
                AgencyInvitationAcceptancePersistenceResult
                    .MembershipAlreadyExists)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .ValidationError(
                        "User is already a member of this agency.");
            }

            await terminalMutationScope.CommitAsync(
                cancellationToken);

            AgencyInvitationListItemResponse response =
                invitation.ToListItemResponse();

            return ServiceResult<
                AgencyInvitationListItemResponse>
                .Success(response);
        }
    }
}
