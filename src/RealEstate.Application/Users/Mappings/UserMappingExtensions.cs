using RealEstate.Application.Users.Dtos;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Users.Mappings;

public static class UserMappingExtensions
{
    public static UserProfileResponse ToProfileResponse(this User user)
    {
        return new UserProfileResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Role,
            user.Status,
            user.AvatarUrl,
            user.CreatedAtUtc,
            user.ModifiedAtUtc);
    }
}
