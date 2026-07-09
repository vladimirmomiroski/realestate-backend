using System.Globalization;
using System.Security.Cryptography;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Application.Agencies.Mappings;

namespace RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;

public sealed class CreateAgencyInvitationHandler
{
    private const int InvitationExpiresInDays = 7;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAgencyRepository _agencyRepository;
    private readonly IAgencyInvitationRepository _agencyInvitationRepository;
    private readonly CreateAgencyInvitationValidator _validator;

    public CreateAgencyInvitationHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        IAgencyInvitationRepository agencyInvitationRepository,
        CreateAgencyInvitationValidator validator)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _agencyRepository = agencyRepository;
        _agencyInvitationRepository = agencyInvitationRepository;
        _validator = validator;
    }

    public async Task<ServiceResult<AgencyInvitationResponse>> HandleAsync(
        Guid agencyId,
        CreateAgencyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid currentUserId)
        {
            return ServiceResult<AgencyInvitationResponse>.Unauthorized(
                "Current user could not be resolved.");
        }

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            currentUserId,
            cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<AgencyInvitationResponse>.Unauthorized(
                "Current user could not be resolved.");
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return ServiceResult<AgencyInvitationResponse>.Forbidden(
                "Disabled users cannot invite agency members.");
        }

        bool agencyExists = await _agencyRepository.ExistsAsync(
            agencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return ServiceResult<AgencyInvitationResponse>.NotFound(
                "Agency was not found.");
        }

        var memberAccess = await _agencyRepository.GetMemberAccessReadOnlyAsync(
            agencyId,
            currentUserId,
            cancellationToken);

        if (memberAccess is null ||
            memberAccess.Status != AgencyMemberStatus.Active ||
            memberAccess.Role != AgencyMemberRole.Owner)
        {
            return ServiceResult<AgencyInvitationResponse>.Forbidden(
                "Only active agency owners can invite members.");
        }

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
