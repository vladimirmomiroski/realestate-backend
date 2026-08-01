namespace RealEstate.Application.Common.Health;

public interface IDatabaseReadinessProbe
{
    Task<bool> CanConnectAsync(
        CancellationToken cancellationToken);
}
