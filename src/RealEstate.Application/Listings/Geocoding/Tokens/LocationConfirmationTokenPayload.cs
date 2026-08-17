namespace RealEstate.Application.Listings.Geocoding.Tokens;

public sealed class LocationConfirmationTokenPayload
{
    public const int CurrentVersion = 1;

    private LocationConfirmationTokenPayload(
        LocationConfirmationTokenClaims claims,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Version = CurrentVersion;
        ListingId = claims.ListingId;
        ActorUserId = claims.ActorUserId;
        ProviderKey = claims.ProviderKey;
        ProviderResultReference = claims.ProviderResultReference;
        LanguageCode = claims.LanguageCode;
        LocationFingerprint = claims.LocationFingerprint;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public int Version { get; }

    public Guid ListingId { get; }

    public Guid ActorUserId { get; }

    public string ProviderKey { get; }

    public string ProviderResultReference { get; }

    public string LanguageCode { get; }

    public string LocationFingerprint { get; }

    public DateTimeOffset IssuedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public static LocationConfirmationTokenPayload CreateCurrent(
        LocationConfirmationTokenClaims claims,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(claims);

        if (issuedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Issue time must use UTC.",
                nameof(issuedAtUtc));
        }

        if (expiresAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Expiration time must use UTC.",
                nameof(expiresAtUtc));
        }

        if (expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentException(
                "Expiration must be after issue time.",
                nameof(expiresAtUtc));
        }

        return new LocationConfirmationTokenPayload(
            claims,
            issuedAtUtc,
            expiresAtUtc);
    }
}
