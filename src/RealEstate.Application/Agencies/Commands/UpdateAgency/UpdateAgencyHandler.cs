using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.UpdateAgency;

public sealed class UpdateAgencyHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly UpdateAgencyValidator _validator;
    private readonly ICurrentUserService _currentUserService;

    public UpdateAgencyHandler(
        IAgencyRepository agencyRepository,
        UpdateAgencyValidator validator,
        ICurrentUserService currentUserService)
    {
        _agencyRepository = agencyRepository;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        Guid agencyId,
        UpdateAgencyRequest request,
        CancellationToken cancellationToken)
    {
        string? validationError = _validator.Validate(request);

        if (validationError is not null)
        {
            return ServiceResult<AgencyResponse>.ValidationError(validationError);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        Agency? agency = await _agencyRepository.GetByIdForUpdateAsync(
            agencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<AgencyResponse>.NotFound("Agency was not found.");
        }

        AgencyMemberAccessReadModel? memberAccess =
            await _agencyRepository.GetMemberAccessReadOnlyAsync(
                agencyId,
                userId,
                cancellationToken);

        bool canUpdate =
            memberAccess is not null &&
            memberAccess.Status == AgencyMemberStatus.Active &&
            memberAccess.Role == AgencyMemberRole.Owner;

        if (!canUpdate)
        {
            return ServiceResult<AgencyResponse>.Forbidden(
                "User is not allowed to update this agency.");
        }

        agency.UpdateProfile(
            request.Name,
            request.Description,
            request.PhoneNumber,
            request.Email,
            request.WebsiteUrl,
            request.AddressLine,
            request.City,
            request.Municipality);

        await _agencyRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult<AgencyResponse>.Success(agency.ToResponse());
    }
}
