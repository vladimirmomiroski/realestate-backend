using System.Collections.ObjectModel;

namespace RealEstate.Domain.Listings;

public enum ListingPublicationReadinessViolationCode
{
    MissingTranslation,
    InvalidLanguageCode,
    InvalidTitle,
    InvalidCity,
    InvalidMunicipality,
    InvalidAddressLine,
    InvalidDescription,
    MissingConfirmedLocation,
    InvalidConfirmedLocation
}

public sealed record ListingPublicationReadinessViolation(
    ListingPublicationReadinessViolationCode Code,
    Guid? TranslationId);

public sealed class ListingPublicationReadinessResult
{
    private static readonly ListingPublicationReadinessResult ReadyResult =
        new([]);

    private ListingPublicationReadinessResult(
        IReadOnlyList<ListingPublicationReadinessViolation> violations)
    {
        Violations = violations;
    }

    public bool IsReady => Violations.Count == 0;

    public IReadOnlyList<ListingPublicationReadinessViolation> Violations { get; }

    internal static ListingPublicationReadinessResult Ready => ReadyResult;

    internal static ListingPublicationReadinessResult FromViolations(
        IEnumerable<ListingPublicationReadinessViolation> violations)
    {
        var snapshot = new ReadOnlyCollection<ListingPublicationReadinessViolation>(
            violations.ToArray());

        return snapshot.Count == 0
            ? ReadyResult
            : new ListingPublicationReadinessResult(snapshot);
    }
}
