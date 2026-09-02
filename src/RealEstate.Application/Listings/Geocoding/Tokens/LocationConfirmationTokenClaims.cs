using System.Buffers;
using System.Text;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Geocoding.Tokens;

public sealed class LocationConfirmationTokenClaims
{
    private LocationConfirmationTokenClaims(
        Guid listingId,
        Guid actorUserId,
        string providerKey,
        string providerResultReference,
        string languageCode,
        string locationFingerprint)
    {
        ListingId = listingId;
        ActorUserId = actorUserId;
        ProviderKey = providerKey;
        ProviderResultReference = providerResultReference;
        LanguageCode = languageCode;
        LocationFingerprint = locationFingerprint;
    }

    public Guid ListingId { get; }

    public Guid ActorUserId { get; }

    public string ProviderKey { get; }

    public string ProviderResultReference { get; }

    public string LanguageCode { get; }

    public string LocationFingerprint { get; }

    public static LocationConfirmationTokenClaims Create(
        Guid listingId,
        Guid actorUserId,
        string providerKey,
        string providerResultReference,
        string languageCode,
        string locationFingerprint)
    {
        if (listingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Listing id cannot be empty.",
                nameof(listingId));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Actor user id cannot be empty.",
                nameof(actorUserId));
        }

        ValidateExactRequiredText(
            providerKey,
            ListingLocationRules.GeocodingProviderKeyMaxLength,
            nameof(providerKey));
        ValidateExactRequiredText(
            providerResultReference,
            ListingLocationRules.GeocodingResultReferenceMaxLength,
            nameof(providerResultReference));

        if (languageCode is null ||
            !ListingTranslationRules.IsCanonicalLanguageCode(languageCode))
        {
            throw new ArgumentException(
                "Language code must be canonical.",
                nameof(languageCode));
        }

        if (!ListingLocationFingerprint.IsCurrentVersionFingerprint(
                locationFingerprint))
        {
            throw new ArgumentException(
                "Location fingerprint must use the current fingerprint format.",
                nameof(locationFingerprint));
        }

        return new LocationConfirmationTokenClaims(
            listingId,
            actorUserId,
            providerKey,
            providerResultReference,
            languageCode,
            locationFingerprint);
    }

    public static bool TryCreate(
        Guid listingId,
        Guid actorUserId,
        string? providerKey,
        string? providerResultReference,
        string? languageCode,
        string? locationFingerprint,
        out LocationConfirmationTokenClaims? claims)
    {
        try
        {
            claims = Create(
                listingId,
                actorUserId,
                providerKey!,
                providerResultReference!,
                languageCode!,
                locationFingerprint!);
            return true;
        }
        catch (ArgumentException)
        {
            claims = null;
            return false;
        }
    }

    private static void ValidateExactRequiredText(
        string? value,
        int maximumScalarLength,
        string parameterName)
    {
        if (value is null ||
            value.Length == 0 ||
            value != ListingTranslationRules.NormalizeRequiredText(value))
        {
            throw new ArgumentException(
                "Value must be nonblank and free of boundary whitespace.",
                parameterName);
        }

        int scalarLength = CountUnicodeScalars(value, parameterName);

        if (scalarLength > maximumScalarLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                scalarLength,
                $"Value cannot exceed {maximumScalarLength} Unicode scalars.");
        }
    }

    private static int CountUnicodeScalars(
        string value,
        string parameterName)
    {
        ReadOnlySpan<char> remaining = value;
        int scalarLength = 0;

        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(
                remaining,
                out _,
                out int consumed);

            if (status != OperationStatus.Done)
            {
                throw new ArgumentException(
                    "Value must contain valid Unicode scalar values.",
                    parameterName);
            }

            scalarLength++;
            remaining = remaining[consumed..];
        }

        return scalarLength;
    }
}
