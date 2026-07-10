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
    private readonly IAgencyInvitationRepository _agencyInvitationRepository;
    private readonly CreateAgencyInvitationValidator _validator;
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;

    public CreateAgencyInvitationHandler(
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        IAgencyInvitationRepository agencyInvitationRepository,
        CreateAgencyInvitationValidator validator,
        AgencyAdminAccessChecker agencyAdminAccessChecker)
    {
        _userRepository = userRepository;
        _agencyRepository = agencyRepository;
        _agencyInvitationRepository = agencyInvitationRepository;
        _validator = validator;
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
    }

    public async Task<ServiceResult<AgencyInvitationResponse>> HandleAsync(
        Guid agencyId,
        CreateAgencyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<AgencyInvitationResponse> accessResult =
            await _agencyAdminAccessChecker.EnsureCurrentUserIsActiveOwnerAsync<AgencyInvitationResponse>(
                agencyId,
                "Only active agency owners can invite members.",
                cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        Guid currentUserId = accessResult.CurrentUserId;

        string? validationError = _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<AgencyInvitationResponse>.ValidationError(validationError);
        }

        string email = request.Email.Trim();
        string normalizedEmail = email.ToUpperInvariant();

        bool pendingInvitationExists =
            await _agencyInvitationRepository.ExistsPendingForAgencyEmailAsync(
                agencyId,
                normalizedEmail,
                cancellationToken);

        if (pendingInvitationExists)
        {
            return ServiceResult<AgencyInvitationResponse>.ValidationError(
                "A pending invitation already exists for this email.");
        }

        User? invitedUser = await _userRepository.GetByNormalizedEmailReadOnlyAsync(
            normalizedEmail,
            cancellationToken);

        if (invitedUser is not null)
        {
            var existingMemberAccess = await _agencyRepository.GetMemberAccessReadOnlyAsync(
                agencyId,
                invitedUser.Id,
                cancellationToken);

            if (existingMemberAccess is not null)
            {
                return ServiceResult<AgencyInvitationResponse>.ValidationError(
                    "User is already a member of this agency.");
            }
        }

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: normalizedEmail,
            token: GenerateToken(),
            code: GenerateCode(),
            role: request.Role,
            invitedByUserId: currentUserId,
            expiresAtUtc: DateTime.UtcNow.AddDays(InvitationExpiresInDays));

        await _agencyInvitationRepository.CreateAsync(
            invitation,
            cancellationToken);

        AgencyInvitationResponse response = invitation.ToResponse();

        return ServiceResult<AgencyInvitationResponse>.Success(response);
    }

    private static string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
    }
}
