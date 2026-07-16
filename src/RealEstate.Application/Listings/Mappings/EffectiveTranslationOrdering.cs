using System.Text;

namespace RealEstate.Application.Listings.Mappings;

public static class EffectiveTranslationOrdering
{
    public static IComparer<string> LanguageCodeComparer { get; } =
        new Utf8LexicographicComparer();

    public static IComparer<Guid> TranslationIdComparer { get; } =
        new CanonicalGuidComparer();

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