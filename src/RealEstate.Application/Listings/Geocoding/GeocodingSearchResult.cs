using System.Collections.ObjectModel;

namespace RealEstate.Application.Listings.Geocoding;

public enum GeocodingSearchOutcome
{
    Success,
    PermanentFailure,
    RateLimited,
    Unavailable,
    MalformedResponse
}

public sealed class GeocodingSearchResult
{
    private static readonly IReadOnlyList<GeocodingCandidate> NoCandidates =
        Array.AsReadOnly(Array.Empty<GeocodingCandidate>());

    private GeocodingSearchResult(
        GeocodingSearchOutcome outcome,
        IReadOnlyList<GeocodingCandidate> candidates)
    {
        Outcome = outcome;
        Candidates = candidates;
    }

    public GeocodingSearchOutcome Outcome { get; }

    public IReadOnlyList<GeocodingCandidate> Candidates { get; }

    public bool Succeeded => Outcome == GeocodingSearchOutcome.Success;

    public static GeocodingSearchResult Success(
        IEnumerable<GeocodingCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        GeocodingCandidate[] snapshot = candidates.ToArray();

        if (snapshot.Any(candidate => candidate is null))
        {
            throw new ArgumentException(
                "Candidates cannot contain null values.",
                nameof(candidates));
        }

        return new GeocodingSearchResult(
            GeocodingSearchOutcome.Success,
            new ReadOnlyCollection<GeocodingCandidate>(snapshot));
    }

    public static GeocodingSearchResult Failure(
        GeocodingSearchOutcome outcome)
    {
        if (outcome == GeocodingSearchOutcome.Success ||
            !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A failure result requires a defined non-success outcome.");
        }

        return new GeocodingSearchResult(outcome, NoCandidates);
    }
}
