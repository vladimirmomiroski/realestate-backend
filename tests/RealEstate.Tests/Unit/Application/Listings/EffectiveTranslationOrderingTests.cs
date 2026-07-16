using FluentAssertions;
using RealEstate.Application.Listings.Mappings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class EffectiveTranslationOrderingTests
{
    [Fact]
    public void LanguageCodeComparer_ShouldOrderValuesLikePostgreSqlCCollation()
    {
        // Arrange
        string[] values =
        [
            "\U00010000",
            "\u00E4",
            "aa",
            "\uE000",
            "A",
            "z",
            "a\u0308",
            "a"
        ];

        string[] expected =
        [
            "A",
            "a",
            "aa",
            "a\u0308",
            "z",
            "\u00E4",
            "\uE000",
            "\U00010000"
        ];

        // Act
        string[] ordered = values
            .OrderBy(
                value => value,
                EffectiveTranslationOrdering.LanguageCodeComparer)
            .ToArray();

        // Assert
        ordered.Should().Equal(expected);
    }

    [Fact]
    public void LanguageCodeComparer_ShouldDifferFromUtf16OrdinalOrdering()
    {
        // Arrange
        const string privateUseCharacter = "\uE000";
        const string supplementaryCharacter = "\U00010000";

        // Act
        int postgresEquivalentResult =
            EffectiveTranslationOrdering.LanguageCodeComparer.Compare(
                privateUseCharacter,
                supplementaryCharacter);

        int utf16OrdinalResult =
            StringComparer.Ordinal.Compare(
                privateUseCharacter,
                supplementaryCharacter);

        // Assert
        postgresEquivalentResult.Should().BeLessThan(0);
        utf16OrdinalResult.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TranslationIdComparer_ShouldOrderValuesLikePostgreSqlUuid()
    {
        // Arrange
        Guid[] values =
        [
            Guid.Parse("01000000-0000-0000-0000-000000000000"),
            Guid.Parse("00000000-0100-0000-0000-000000000000"),
            Guid.Parse("00000000-0000-0100-0000-000000000000"),
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            Guid.Parse("00000000-0001-0000-0000-000000000000"),
            Guid.Parse("00000000-0000-0001-0000-000000000000")
        ];

        Guid[] expected =
        [
            Guid.Parse("00000000-0000-0001-0000-000000000000"),
            Guid.Parse("00000000-0000-0100-0000-000000000000"),
            Guid.Parse("00000000-0001-0000-0000-000000000000"),
            Guid.Parse("00000000-0100-0000-0000-000000000000"),
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            Guid.Parse("01000000-0000-0000-0000-000000000000")
        ];

        // Act
        Guid[] ordered = values
            .OrderBy(
                value => value,
                EffectiveTranslationOrdering.TranslationIdComparer)
            .ToArray();

        // Assert
        ordered.Should().Equal(expected);
    }

    [Fact]
    public void TranslationIdComparer_ShouldDifferFromGuidByteArrayOrdering()
    {
        // Arrange
        Guid lowerPostgreSqlUuid =
            Guid.Parse("00000001-0000-0000-0000-000000000000");

        Guid higherPostgreSqlUuid =
            Guid.Parse("01000000-0000-0000-0000-000000000000");

        // Act
        int postgresEquivalentResult =
            EffectiveTranslationOrdering.TranslationIdComparer.Compare(
                lowerPostgreSqlUuid,
                higherPostgreSqlUuid);

        int mixedEndianByteResult =
            CompareByteArrays(
                lowerPostgreSqlUuid.ToByteArray(),
                higherPostgreSqlUuid.ToByteArray());

        // Assert
        postgresEquivalentResult.Should().BeLessThan(0);
        mixedEndianByteResult.Should().BeGreaterThan(0);
    }

    private static int CompareByteArrays(byte[] left, byte[] right)
    {
        int comparedLength = Math.Min(left.Length, right.Length);

        for (int index = 0; index < comparedLength; index++)
        {
            int comparison = left[index].CompareTo(right[index]);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
