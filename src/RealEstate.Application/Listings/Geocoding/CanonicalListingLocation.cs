using System.Collections.ObjectModel;
using RealEstate.Domain.Listings;

namespace RealEstate.Application.Listings.Geocoding;

public sealed record CanonicalListingLocationInput(
    string LanguageCode,
    string? City,
    string? Municipality,
    string? AddressLine,
    string? Neighborhood);

public sealed record CanonicalListingLocationTranslation
{
    private CanonicalListingLocationTranslation(
        string languageCode,
        string? city,
        string? municipality,
        string? addressLine,
        string? neighborhood)
    {
        LanguageCode = languageCode;
        City = city;
        Municipality = municipality;
        AddressLine = addressLine;
        Neighborhood = neighborhood;
    }

    public string LanguageCode { get; }

    public string? City { get; }

    public string? Municipality { get; }

    public string? AddressLine { get; }

    public string? Neighborhood { get; }

    internal static CanonicalListingLocationTranslation From(
        CanonicalListingLocationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new CanonicalListingLocationTranslation(
            ListingTranslationRules.NormalizeLanguageCode(input.LanguageCode),
            ListingTranslationRules.NormalizeOptionalText(input.City),
            ListingTranslationRules.NormalizeOptionalText(input.Municipality),
            ListingTranslationRules.NormalizeOptionalText(input.AddressLine),
            ListingTranslationRules.NormalizeOptionalText(input.Neighborhood));
    }
}

public sealed class CanonicalListingLocation
{
    private CanonicalListingLocation(
        IReadOnlyList<CanonicalListingLocationTranslation> translations)
    {
        Translations = translations;
    }

    public IReadOnlyList<CanonicalListingLocationTranslation> Translations
    {
        get;
    }

    public static CanonicalListingLocation From(
        IEnumerable<CanonicalListingLocationInput> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        Dictionary<string, CanonicalListingLocationTranslation> byLanguage =
            translations
                .Select(CanonicalListingLocationTranslation.From)
                .ToDictionary(
                    translation => translation.LanguageCode,
                    StringComparer.Ordinal);

        CanonicalListingLocationTranslation[] ordered = byLanguage
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToArray();

        return new CanonicalListingLocation(
            new ReadOnlyCollection<CanonicalListingLocationTranslation>(
                ordered));
    }

    public bool HasSameIdentityAs(CanonicalListingLocation other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Translations.SequenceEqual(other.Translations);
    }
}
