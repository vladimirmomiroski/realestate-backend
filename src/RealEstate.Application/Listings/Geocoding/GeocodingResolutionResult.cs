namespace RealEstate.Application.Listings.Geocoding;

public enum GeocodingResolutionOutcome
{
    Success,
    NotFound,
    Stale,
    PermanentFailure,
    RateLimited,
    Unavailable,
    MalformedResponse
}

public sealed class GeocodingResolutionResult
{
    private GeocodingResolutionResult(
        GeocodingResolutionOutcome outcome,
        ResolvedGeocodingSnapshot? snapshot)
    {
        Outcome = outcome;
        Snapshot = snapshot;
    }

    public GeocodingResolutionOutcome Outcome { get; }

    public ResolvedGeocodingSnapshot? Snapshot { get; }

    public bool Succeeded => Outcome == GeocodingResolutionOutcome.Success;

    public static GeocodingResolutionResult Success(
        ResolvedGeocodingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new GeocodingResolutionResult(
            GeocodingResolutionOutcome.Success,
            snapshot);
    }

    public static GeocodingResolutionResult Failure(
        GeocodingResolutionOutcome outcome)
    {
        if (outcome == GeocodingResolutionOutcome.Success ||
            !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A failure result requires a defined non-success outcome.");
        }

        return new GeocodingResolutionResult(outcome, null);
    }
}
