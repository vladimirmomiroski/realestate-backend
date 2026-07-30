using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Application.Users.Repositories;

namespace RealEstate.Application.Agencies.Commands.CreateAgency;

public sealed class CreateAgencyHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly CreateAgencyValidator _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public CreateAgencyHandler(
        IAgencyRepository agencyRepository,
        CreateAgencyValidator validator,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _agencyRepository = agencyRepository;
        _validator = validator;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        CreateAgencyRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid userId)
        {
            return ServiceResult<AgencyResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            userId,
            cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<AgencyResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return ServiceResult<AgencyResponse>.Forbidden(
                "Disabled users cannot create agencies.",
                ErrorCodes.AuthorizationAccountDisabled);
        }

        CreateAgencyValidator.ValidationFailure? validationFailure =
            _validator.ValidateWithKey(request);

        if (validationFailure is not null)
        {
            return ServiceResult<AgencyResponse>.ValidationError(
                validationFailure.Error,
                validationFailure.Key,
                ErrorCodes.ValidationFailed);
        }

        string slug = NormalizeSlug(request.Slug);

        bool slugExists = await _agencyRepository.SlugExistsAsync(
            slug,
            cancellationToken);

        if (slugExists)
        {
            return ServiceResult<AgencyResponse>.Conflict(
                "Agency slug already exists.",
                ErrorCodes.ConflictAgencySlugAlreadyExists);
        }

        var agency = new Agency(
            name: request.Name.Trim(),
            slug: slug,
            description: CleanNullableText(request.Description),
            phoneNumber: CleanNullableText(request.PhoneNumber),
            email: CleanNullableText(request.Email),
            websiteUrl: CleanNullableText(request.WebsiteUrl),
            addressLine: CleanNullableText(request.AddressLine),
            city: CleanNullableText(request.City),
            municipality: CleanNullableText(request.Municipality));

        agency.AddMember(userId, AgencyMemberRole.Owner);

        await _agencyRepository.CreateAsync(agency, cancellationToken);

        return ServiceResult<AgencyResponse>.Success(agency.ToResponse());
    }

    private static string NormalizeSlug(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }

    private static string? CleanNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
