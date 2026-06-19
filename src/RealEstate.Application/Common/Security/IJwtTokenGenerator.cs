using RealEstate.Domain.Entities;

namespace RealEstate.Application.Common.Security;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
}
