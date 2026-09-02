using FluentAssertions;
using RealEstate.Domain.Listings;

namespace RealEstate.Tests.Unit.Domain.Listings;

public sealed class ListingTranslationRulesTests
{
    public static TheoryData<string> BoundaryWhitespaceCases => new()
    {
        " ",
        "\t",
        "\r\n",
        "\u00A0",
        "\u2003",
        "\u3000"
    };

    [Theory]
    [MemberData(nameof(BoundaryWhitespaceCases))]
    public void NormalizeLanguageCode_TrimsApprovedWhitespaceAndLowercases(
        string whitespace)
    {
        string result =
            ListingTranslationRules.NormalizeLanguageCode(
                $"{whitespace}EN-US{whitespace}");

        result.Should().Be("en-us");
    }

    [Theory]
    [MemberData(nameof(BoundaryWhitespaceCases))]
    public void NormalizeRequiredText_TrimsApprovedBoundaryWhitespace(
        string whitespace)
    {
        string result =
            ListingTranslationRules.NormalizeRequiredText(
                $"{whitespace}Title{whitespace}");

        result.Should().Be("Title");
    }

    [Theory]
    [MemberData(nameof(BoundaryWhitespaceCases))]
    public void NormalizeOptionalText_ReturnsNullForApprovedWhitespace(
        string whitespace)
    {
        string? result =
            ListingTranslationRules.NormalizeOptionalText(whitespace);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("mk")]
    [InlineData("en")]
    [InlineData("sq")]
    [InlineData("de")]
    [InlineData("en-us")]
    [InlineData("sr-latn")]
    public void IsCanonicalLanguageCode_ReturnsTrueForApprovedGrammar(
        string languageCode)
    {
        ListingTranslationRules.IsCanonicalLanguageCode(languageCode)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("engl")]
    [InlineData("EN")]
    [InlineData(" en")]
    [InlineData("en_")]
    [InlineData("en-")]
    [InlineData("en-u")]
    [InlineData("en-abcdefghi")]
    [InlineData("ен")]
    public void IsCanonicalLanguageCode_ReturnsFalseForInvalidOrNoncanonicalValue(
        string languageCode)
    {
        ListingTranslationRules.IsCanonicalLanguageCode(languageCode)
            .Should()
            .BeFalse();
    }
}
