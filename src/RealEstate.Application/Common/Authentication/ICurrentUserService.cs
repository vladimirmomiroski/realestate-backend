namespace RealEstate.Application.Common.Authentication;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}