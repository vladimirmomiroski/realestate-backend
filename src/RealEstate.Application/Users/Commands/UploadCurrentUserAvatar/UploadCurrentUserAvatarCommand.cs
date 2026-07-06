using RealEstate.Application.Common.Files;

namespace RealEstate.Application.Users.Commands.UploadCurrentUserAvatar;

public sealed record UploadCurrentUserAvatarCommand(
    UploadedFile? File);
