using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.CreateAgency;

public sealed class CreateAgencyHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly CreateAgencyValidator _validator;
    private readonly ICurrentUserService _currentUserService;

    public CreateAgencyHandler(
        IAgencyRepository agencyRepository,
        CreateAgencyValidator validator,
        ICurrentUserService currentUserService)
    {
        _agencyRepository = agencyRepository;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        CreateAgencyRequest request,
        CancellationToken cancellationToken)
    {
        string? validationError = _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<AgencyResponse>.ValidationError(validationError);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        string slug = NormalizeSlug(request.Slug);

        bool slugExists = await _agencyRepository.SlugExistsAsync(
            slug,
            cancellationToken);

        if (slugExists)
        {
            return ServiceResult<AgencyResponse>.ValidationError(
                "Agency slug already exists.");
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