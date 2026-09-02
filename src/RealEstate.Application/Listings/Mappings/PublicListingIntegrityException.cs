using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Mappings;

public sealed class PublicListingIntegrityException : InvalidOperationException
{
    internal PublicListingIntegrityException(
        Guid listingId,
        IReadOnlyList<ListingPublicationReadinessViolation> violations)
        : base("A materialized public listing violated the publication integrity invariant.")
    {
        ListingId = listingId;
        Violations = violations.ToArray();
    }

    public Guid ListingId { get; }

    public IReadOnlyList<ListingPublicationReadinessViolation> Violations { get; }
}
