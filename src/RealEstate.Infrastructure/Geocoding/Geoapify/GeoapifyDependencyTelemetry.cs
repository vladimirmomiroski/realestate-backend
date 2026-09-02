using Microsoft.Extensions.Logging;

namespace RealEstate.Infrastructure.Geocoding.Geoapify;

internal static partial class GeoapifyDependencyTelemetry
{
    public const int TerminalEventId = 13600;

    [LoggerMessage(
        EventId = TerminalEventId,
        Level = LogLevel.Information,
        Message = "Geocoding dependency operation completed. Provider={ProviderKey} Operation={Operation} Outcome={Outcome} Attempts={AttemptCount} ElapsedMilliseconds={ElapsedMilliseconds}")]
    public static partial void LogTerminal(
        ILogger logger,
        string providerKey,
        string operation,
        string outcome,
        int attemptCount,
        double elapsedMilliseconds);
}
