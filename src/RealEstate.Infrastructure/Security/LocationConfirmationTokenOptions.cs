namespace RealEstate.Infrastructure.Security;

public sealed class LocationConfirmationTokenOptions
{
    public const string SectionName = "LocationConfirmationToken";
    public const int DefaultLifetimeMinutes = 10;
    public const int MinimumLifetimeMinutes = 1;
    public const int MaximumLifetimeMinutes = 30;

    public int LifetimeMinutes { get; init; } = DefaultLifetimeMinutes;

    internal TimeSpan GetLifetime()
    {
        if (LifetimeMinutes < MinimumLifetimeMinutes ||
            LifetimeMinutes > MaximumLifetimeMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LifetimeMinutes),
                LifetimeMinutes,
                $"Token lifetime must be between {MinimumLifetimeMinutes} and {MaximumLifetimeMinutes} minutes.");
        }

        return TimeSpan.FromMinutes(LifetimeMinutes);
    }
}
