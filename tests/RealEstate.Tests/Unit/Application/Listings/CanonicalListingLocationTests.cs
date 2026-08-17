using FluentAssertions;
using RealEstate.Application.Listings.Geocoding;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class CanonicalListingLocationTests
{
    [Fact]
    public void From_UsesExistingH4CanonicalNormalizationAndFieldSet()
    {
        CanonicalListingLocation location = CanonicalListingLocation.From(
        [
            new CanonicalListingLocationInput(
                "\u00A0EN\u3000",
                "\u00A0Skopje\u3000",
                "\tCentar\r\n",
                "  Macedonia Street  ",
                "\u2009\u2009")
        ]);

        location.Translations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                LanguageCode = "en",
                City = "Skopje",
                Municipality = "Centar",
                AddressLine = "Macedonia Street",
                Neighborhood = (string?)null
            });
    }

    [Fact]
    public void HasSameIdentityAs_IsIndependentOfTranslationInputOrder()
    {
        CanonicalListingLocation first = Create(
            Input("mk", "Скопје"),
            Input("en", "Skopje"));
        CanonicalListingLocation reordered = Create(
            Input("en", "Skopje"),
            Input("mk", "Скопје"));

        first.HasSameIdentityAs(reordered).Should().BeTrue();
        first.Translations.Select(item => item.LanguageCode)
            .Should().Equal("en", "mk");
    }

    [Fact]
    public void HasSameIdentityAs_TreatsCanonicalLanguageAndTextAsEquivalent()
    {
        CanonicalListingLocation first = Create(
            Input("EN", " Skopje ", " Centar ", " Address ", " Center "));
        CanonicalListingLocation equivalent = Create(
            Input("\u00A0en\u3000", "Skopje", "Centar", "Address", "Center"));

        first.HasSameIdentityAs(equivalent).Should().BeTrue();
    }

    [Theory]
    [InlineData("language")]
    [InlineData("city")]
    [InlineData("municipality")]
    [InlineData("addressLine")]
    [InlineData("neighborhood")]
    public void HasSameIdentityAs_DetectsEveryH4IdentityChange(string component)
    {
        CanonicalListingLocation baseline = Create(
            Input("en", "Skopje", "Centar", "Address", "Center"));
        CanonicalListingLocation changed = component switch
        {
            "language" => Create(
                Input("mk", "Skopje", "Centar", "Address", "Center")),
            "city" => Create(
                Input("en", "Ohrid", "Centar", "Address", "Center")),
            "municipality" => Create(
                Input("en", "Skopje", "Karpos", "Address", "Center")),
            "addressLine" => Create(
                Input("en", "Skopje", "Centar", "Other", "Center")),
            "neighborhood" => Create(
                Input("en", "Skopje", "Centar", "Address", "Debar Maalo")),
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

        baseline.HasSameIdentityAs(changed).Should().BeFalse();
    }

    [Fact]
    public void HasSameIdentityAs_DetectsLanguageAdditionAndRemoval()
    {
        CanonicalListingLocation oneLanguage = Create(Input("en", "Skopje"));
        CanonicalListingLocation twoLanguages = Create(
            Input("en", "Skopje"),
            Input("mk", "Скопје"));

        oneLanguage.HasSameIdentityAs(twoLanguages).Should().BeFalse();
        twoLanguages.HasSameIdentityAs(oneLanguage).Should().BeFalse();
    }

    [Fact]
    public void From_RejectsDuplicateCanonicalLanguageLikeExistingH4Comparison()
    {
        Action act = () => Create(
            Input("EN", "Skopje"),
            Input(" en ", "Skopje"));

        act.Should().Throw<ArgumentException>();
    }

    private static CanonicalListingLocation Create(
        params CanonicalListingLocationInput[] translations)
    {
        return CanonicalListingLocation.From(translations);
    }

    private static CanonicalListingLocationInput Input(
        string languageCode,
        string? city,
        string? municipality = null,
        string? addressLine = null,
        string? neighborhood = null)
    {
        return new CanonicalListingLocationInput(
            languageCode,
            city,
            municipality,
            addressLine,
            neighborhood);
    }
}
