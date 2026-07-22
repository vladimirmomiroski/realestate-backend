using System.Text;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Mappings;

public static class EffectiveTranslationOrdering
{
    public static IComparer<string> LanguageCodeComparer { get; } =
        new Utf8LexicographicComparer();

    public static IComparer<Guid> TranslationIdComparer { get; } =
        new CanonicalGuidComparer();

    public static ListingTranslation? SelectEffectiveTranslation(
        IEnumerable<ListingTranslation> translations,
        string? requestedLanguageCode)
    {
        ArgumentNullException.ThrowIfNull(translations);

        string normalizedLanguageCode =
            NormalizeRequestedLanguageCode(requestedLanguageCode);

        return translations
            .OrderBy(translation =>
                GetLanguagePriority(
                    translation.LanguageCode,
                    normalizedLanguageCode))
            .ThenBy(
                translation => translation.LanguageCode,
                LanguageCodeComparer)
            .ThenBy(
                translation => translation.Id,
                TranslationIdComparer)
            .FirstOrDefault();
    }

    public static string NormalizeRequestedLanguageCode(
        string? languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "mk"
            : languageCode.Trim().ToLowerInvariant();
    }

    private static int GetLanguagePriority(
        string storedLanguageCode,
        string requestedLanguageCode)
    {
        if (storedLanguageCode.Equals(
                requestedLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (storedLanguageCode.Equals(
                "mk",
                StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private sealed class Utf8LexicographicComparer : IComparer<string>
    {
        private static readonly Encoding Utf8 =
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            byte[] leftBytes = Utf8.GetBytes(left);
            byte[] rightBytes = Utf8.GetBytes(right);

            return leftBytes
                .AsSpan()
                .SequenceCompareTo(rightBytes);
        }
    }

    private sealed class CanonicalGuidComparer : IComparer<Guid>
    {
        public int Compare(Guid left, Guid right)
        {
            return StringComparer.Ordinal.Compare(
                left.ToString("D"),
                right.ToString("D"));
        }
    }
}