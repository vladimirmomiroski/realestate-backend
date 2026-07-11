using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Storage;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Commands.DeleteAgencyLogo;

public sealed class DeleteAgencyLogoHandler
{
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;
    private readonly IAgencyRepository _agencyRepository;
    private readonly IFileStorageService _fileStorageService;

    public DeleteAgencyLogoHandler(
        AgencyAdminAccessChecker agencyAdminAccessChecker,
        IAgencyRepository agencyRepository,
        IFileStorageService fileStorageService)
    {
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
        _agencyRepository = agencyRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ServiceResult<bool>> HandleAsync(
        DeleteAgencyLogoCommand command,
        CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<bool> accessResult =
            await _agencyAdminAccessChecker
                .EnsureCurrentUserIsActiveOwnerAsync<bool>(
                    command.AgencyId,
                    "Only active agency owners can delete the agency logo.",
                    cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        Agency? agency = await _agencyRepository.GetByIdForUpdateAsync(
            command.AgencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<bool>.NotFound(
                "Agency was not found.");
        }

        bool hasLogoMetadata =
            !string.IsNullOrWhiteSpace(agency.LogoUrl) ||
            !string.IsNullOrWhiteSpace(agency.LogoStoredFileName) ||
            !string.IsNullOrWhiteSpace(agency.LogoContentType) ||
            agency.LogoSizeBytes.HasValue;

        if (!hasLogoMetadata)
        {
            return ServiceResult<bool>.Success(true);
        }

        string? oldStoredFileName = agency.LogoStoredFileName;

        agency.RemoveLogo();

        await _agencyRepository.SaveChangesAsync(
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldStoredFileName))
        {
            await _fileStorageService.DeleteAgencyLogoAsync(
                agency.Id,
                oldStoredFileName,
                cancellationToken);
        }

        return ServiceResult<bool>.Success(true);
    }
}