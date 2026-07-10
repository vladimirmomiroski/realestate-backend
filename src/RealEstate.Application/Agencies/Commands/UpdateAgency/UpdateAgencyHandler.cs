using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Commands.UpdateAgency;

public sealed class UpdateAgencyHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly UpdateAgencyValidator _validator;
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;

    public UpdateAgencyHandler(
        IAgencyRepository agencyRepository,
        UpdateAgencyValidator validator,
        AgencyAdminAccessChecker agencyAdminAccessChecker)
    {
        _agencyRepository = agencyRepository;
        _validator = validator;
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
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

        AgencyAdminAccessResult<AgencyResponse> accessResult =
            await _agencyAdminAccessChecker.EnsureCurrentUserIsActiveOwnerAsync<AgencyResponse>(
                agencyId,
                "User is not allowed to update this agency.",
                cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        Agency? agency = await _agencyRepository.GetByIdForUpdateAsync(
            agencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<AgencyResponse>.NotFound("Agency was not found.");
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