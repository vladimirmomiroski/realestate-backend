namespace RealEstate.Application.Common.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string password);
}
