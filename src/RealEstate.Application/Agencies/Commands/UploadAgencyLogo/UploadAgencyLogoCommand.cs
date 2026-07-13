using RealEstate.Application.Common.Files;

namespace RealEstate.Application.Agencies.Commands.UploadAgencyLogo;

public sealed record UploadAgencyLogoCommand(
    Guid AgencyId,
    UploadedFile? File);