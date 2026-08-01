using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Mappings;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Users.Commands.UpdateCurrentUserProfile;

public sealed class UpdateCurrentUserProfileHandler
{
    private const int FirstNameMaxLength = 100;
    private const int LastNameMaxLength = 100;
    private const int PhoneNumberMaxLength = 50;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public UpdateCurrentUserProfileHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<UserProfileResponse>> HandleAsync(
        UpdateCurrentUserProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        var user = await _userRepository.GetByIdForUpdateAsync(
            _currentUserService.UserId.Value,
            cancellationToken);

        if (user is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (user.Status == UserStatus.Disabled)
        {
            return ServiceResult<UserProfileResponse>.Forbidden(
                "Disabled users cannot update profile.",
                ErrorCodes.AuthorizationAccountDisabled);
        }

        ServiceResult<UserProfileResponse>? validationResult = Validate(command);

        if (validationResult is not null)
        {
            return validationResult;
        }

        user.UpdateProfile(
            command.FirstName!,
            command.LastName!,
            command.PhoneNumber);

        await _userRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserProfileResponse>.Success(
            user.ToProfileResponse());
    }

    private static ServiceResult<UserProfileResponse>? Validate(
        UpdateCurrentUserProfileCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FirstName))
        {
            return ServiceResult<UserProfileResponse>.ValidationError(
                "First name is required.",
                "firstName",
                ErrorCodes.ValidationFailed);
        }

        if (command.FirstName.Trim().Length > FirstNameMaxLength)
        {
            return ServiceResult<UserProfileResponse>.ValidationError(
                "First name cannot be longer than 100 characters.",
                "firstName",
                ErrorCodes.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(command.LastName))
        {
            return ServiceResult<UserProfileResponse>.ValidationError(
                "Last name is required.",
                "lastName",
                ErrorCodes.ValidationFailed);
        }

        if (command.LastName.Trim().Length > LastNameMaxLength)
        {
            return ServiceResult<UserProfileResponse>.ValidationError(
                "Last name cannot be longer than 100 characters.",
                "lastName",
                ErrorCodes.ValidationFailed);
        }

        if (command.PhoneNumber is not null &&
            command.PhoneNumber.Trim().Length > PhoneNumberMaxLength)
        {
            return ServiceResult<UserProfileResponse>.ValidationError(
                "Phone number cannot be longer than 50 characters.",
                "phoneNumber",
                ErrorCodes.ValidationFailed);
        }

        return null;
    }
}
