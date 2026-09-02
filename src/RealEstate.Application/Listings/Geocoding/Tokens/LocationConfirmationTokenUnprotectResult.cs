namespace RealEstate.Application.Listings.Geocoding.Tokens;

public enum LocationConfirmationTokenUnprotectOutcome
{
    Success,
    Invalid,
    Expired,
    UnsupportedVersion
}

public sealed class LocationConfirmationTokenUnprotectResult
{
    private LocationConfirmationTokenUnprotectResult(
        LocationConfirmationTokenUnprotectOutcome outcome,
        LocationConfirmationTokenPayload? payload)
    {
        Outcome = outcome;
        Payload = payload;
    }

    public LocationConfirmationTokenUnprotectOutcome Outcome { get; }

    public LocationConfirmationTokenPayload? Payload { get; }

    public bool Succeeded =>
        Outcome == LocationConfirmationTokenUnprotectOutcome.Success;

    public static LocationConfirmationTokenUnprotectResult Success(
        LocationConfirmationTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new LocationConfirmationTokenUnprotectResult(
            LocationConfirmationTokenUnprotectOutcome.Success,
            payload);
    }

    public static LocationConfirmationTokenUnprotectResult Failure(
        LocationConfirmationTokenUnprotectOutcome outcome)
    {
        if (outcome == LocationConfirmationTokenUnprotectOutcome.Success ||
            !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A failure result requires a defined non-success outcome.");
        }

        return new LocationConfirmationTokenUnprotectResult(outcome, null);
    }
}
