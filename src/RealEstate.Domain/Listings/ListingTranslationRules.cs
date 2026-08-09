using System.Text.RegularExpressions;

namespace RealEstate.Domain.Listings;

public static class ListingTranslationRules
{
    public const int LanguageCodeMaxLength = 10;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 3000;
    public const int AddressLineMaxLength = 300;
    public const int LocationMaxLength = 100;

    public const string LanguageCodePattern =
        "^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$";

    public const string BoundaryWhitespaceCharacters =
        "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680" +
        "\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008" +
        "\u2009\u200A\u2028\u2029\u202F\u205F\u3000";

    private static readonly Regex LanguageCodeRegex = new(
        LanguageCodePattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string NormalizeLanguageCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return TrimBoundaryWhitespace(value).ToLowerInvariant();
    }

    public static string NormalizeRequiredText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return TrimBoundaryWhitespace(value);
    }

    public static string? NormalizeOptionalText(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string normalized = TrimBoundaryWhitespace(value);

        return normalized.Length == 0
            ? null
            : normalized;
    }

    public static bool IsCanonicalLanguageCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length <= LanguageCodeMaxLength &&
               value == NormalizeLanguageCode(value) &&
               LanguageCodeRegex.IsMatch(value);
    }

    public static string TrimBoundaryWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int start = 0;
        int end = value.Length - 1;

        while (start <= end && IsBoundaryWhitespace(value[start]))
        {
            start++;
        }

        while (end >= start && IsBoundaryWhitespace(value[end]))
        {
            end--;
        }

        return start == 0 && end == value.Length - 1
            ? value
            : value.Substring(start, end - start + 1);
    }

    private static bool IsBoundaryWhitespace(char value)
    {
        return BoundaryWhitespaceCharacters.Contains(value);
    }
}
