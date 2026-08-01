using System.Globalization;
using System.Security.Cryptography;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;

public sealed class CreateAgencyInvitationHandler
{
    private const int InvitationExpiresInDays = 7;

    private readonly IUserRepository _userRepository;
    private readonly IAgencyRepository _agencyRepository;
    private readonly IAgencyInvitationRepository
        _agencyInvitationRepository;
    private readonly CreateAgencyInvitationValidator _validator;
    private readonly AgencyAdminAccessChecker
        _agencyAdminAccessChecker;

    public CreateAgencyInvitationHandler(
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        IAgencyInvitationRepository agencyInvitationRepository,
        CreateAgencyInvitationValidator validator,
        AgencyAdminAccessChecker agencyAdminAccessChecker)
    {
        _userRepository = userRepository;
        _agencyRepository = agencyRepository;
        _agencyInvitationRepository =
            agencyInvitationRepository;
        _validator = validator;
        _agencyAdminAccessChecker =
            agencyAdminAccessChecker;
    }

    public async Task<
        ServiceResult<AgencyInvitationCreatedResponse>>
        HandleAsync(
            Guid agencyId,
            CreateAgencyInvitationRequest request,
            CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<
            AgencyInvitationCreatedResponse>
            accessResult =
                await _agencyAdminAccessChecker
                    .EnsureCurrentUserIsActiveOwnerAsync<
                        AgencyInvitationCreatedResponse>(
                        agencyId,
                        "Only active agency owners can invite members.",
                        cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        Guid currentUserId =
            accessResult.CurrentUserId;

        CreateAgencyInvitationValidator.ValidationFailure?
            validationFailure =
                _validator.ValidateWithKey(request);

        if (validationFailure is not null)
        {
            return ServiceResult<
                AgencyInvitationCreatedResponse>
                .ValidationError(
                    validationFailure.Error,
                    validationFailure.Key,
                    ErrorCodes.ValidationFailed);
        }

        string email = request.Email.Trim();

        string normalizedEmail =
            email.ToUpperInvariant();

        IAgencyInvitationCreationScope creationScope =
            await _agencyInvitationRepository
                .BeginCreateOrReplaceAsync(
                    agencyId,
                    normalizedEmail,
                    cancellationToken);

        await using (creationScope)
        {
            User? invitedUser =
                await _userRepository
                    .GetByNormalizedEmailReadOnlyAsync(
                        normalizedEmail,
                        cancellationToken);

            bool invitedUserIsAlreadyMember = false;

            if (invitedUser is not null)
            {
                var existingMemberAccess =
                    await _agencyRepository
                        .GetMemberAccessReadOnlyAsync(
                            agencyId,
                            invitedUser.Id,
                            cancellationToken);

                invitedUserIsAlreadyMember =
                    existingMemberAccess is not null;
            }

            DateTime utcNow = DateTime.UtcNow;

            AgencyInvitation? pendingInvitation =
                creationScope.PendingInvitation;

            if (pendingInvitation is not null &&
                pendingInvitation.ExpiresAtUtc > utcNow)
            {
                return ServiceResult<
                    AgencyInvitationCreatedResponse>
                    .Conflict(
                        "A pending invitation already exists for this email.",
                        ErrorCodes.ConflictResourceState);
            }

            if (invitedUserIsAlreadyMember)
            {
                return ServiceResult<
                    AgencyInvitationCreatedResponse>
                    .Conflict(
                        "User is already a member of this agency.",
                        ErrorCodes.ConflictResourceState);
            }

            if (pendingInvitation is not null)
            {
                pendingInvitation.MarkExpired(utcNow);

                await creationScope
                    .PersistObservedExpiryAsync(
                        cancellationToken);
            }

            var invitation = new AgencyInvitation(
                agencyId: agencyId,
                email: email,
                normalizedEmail: normalizedEmail,
                token: GenerateToken(),
                code: GenerateCode(),
                role: request.Role,
                invitedByUserId: currentUserId,
                expiresAtUtc:
                    DateTime.UtcNow.AddDays(
                        InvitationExpiresInDays));

            AgencyInvitationCreationPersistenceResult
                persistenceResult =
                    await creationScope
                        .PersistNewInvitationAsync(
                            invitation,
                            cancellationToken);

            switch (persistenceResult)
            {
                case AgencyInvitationCreationPersistenceResult.Succeeded:
                    break;

                case AgencyInvitationCreationPersistenceResult
                    .PendingInvitationAlreadyExists:
                    return ServiceResult<
                        AgencyInvitationCreatedResponse>
                        .Conflict(
                            "A pending invitation already exists for this email.",
                            ErrorCodes.ConflictResourceState);

                default:
                    throw new InvalidOperationException(
                        "The invitation persistence result was not mapped.");
            }

            await creationScope.CommitAsync(
                cancellationToken);

            AgencyInvitationCreatedResponse response =
                invitation.ToCreatedResponse();

            return ServiceResult<
                AgencyInvitationCreatedResponse>
                .Success(response);
        }
    }

    private static string GenerateToken()
    {
        byte[] bytes =
            RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace(
                "+",
                "-",
                StringComparison.Ordinal)
            .Replace(
                "/",
                "_",
                StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString(
                "D6",
                CultureInfo.InvariantCulture);
    }
}
