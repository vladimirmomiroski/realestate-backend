using RealEstate.Application.Common;

namespace RealEstate.Application.Agencies.Permissions;

public sealed class AgencyAdminAccessResult<TResponse>
{
    private AgencyAdminAccessResult(
        Guid currentUserId,
        ServiceResult<TResponse>? failure)
    {
        CurrentUserId = currentUserId;
        Failure = failure;
    }

    public Guid CurrentUserId { get; }

    public ServiceResult<TResponse>? Failure { get; }

    public bool HasFailure => Failure is not null;

    public static AgencyAdminAccessResult<TResponse> Succeeded(Guid currentUserId)
    {
        return new AgencyAdminAccessResult<TResponse>(
            currentUserId,
            failure: null);
    }

    public static AgencyAdminAccessResult<TResponse> Failed(
        ServiceResult<TResponse> failure)
    {
        return new AgencyAdminAccessResult<TResponse>(
            currentUserId: Guid.Empty,
            failure);
    }
}
