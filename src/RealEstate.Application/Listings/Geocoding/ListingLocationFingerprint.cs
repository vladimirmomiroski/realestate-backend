using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RealEstate.Application.Listings.Geocoding;

public static class ListingLocationFingerprint
{
    public const int CurrentVersion = 1;
    public const string CurrentVersionPrefix = "v1:";

    private const string FormatIdentifier =
        "RealEstate.ListingLocationFingerprint";

    private static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string Compute(CanonicalListingLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        using var canonicalBytes = new MemoryStream();

        WriteString(canonicalBytes, FormatIdentifier);
        WriteInt32(canonicalBytes, CurrentVersion);
        WriteInt32(canonicalBytes, location.Translations.Count);

        foreach (CanonicalListingLocationTranslation translation in
                 location.Translations)
        {
            WriteString(canonicalBytes, translation.LanguageCode);
            WriteNullableString(canonicalBytes, translation.City);
            WriteNullableString(canonicalBytes, translation.Municipality);
            WriteNullableString(canonicalBytes, translation.AddressLine);
            WriteNullableString(canonicalBytes, translation.Neighborhood);
        }

        byte[] digest = SHA256.HashData(canonicalBytes.GetBuffer().AsSpan(
            0,
            checked((int)canonicalBytes.Length)));

        return CurrentVersionPrefix + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void WriteNullableString(Stream destination, string? value)
    {
        if (value is null)
        {
            WriteInt32(destination, -1);
            return;
        }

        WriteString(destination, value);
    }

    private static void WriteString(Stream destination, string value)
    {
        byte[] bytes = Utf8.GetBytes(value);
        WriteInt32(destination, bytes.Length);
        destination.Write(bytes);
    }

    private static void WriteInt32(Stream destination, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        destination.Write(bytes);
    }
}
