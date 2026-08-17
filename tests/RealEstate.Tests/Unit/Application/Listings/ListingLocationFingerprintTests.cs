using System.Globalization;
using FluentAssertions;
using RealEstate.Application.Listings.Geocoding;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class ListingLocationFingerprintTests
{
    [Fact]
    public void Compute_ReturnsStableVersionedMacedonianVector()
    {
        CanonicalListingLocation location = Create(
            Input(
                "mk",
                "Скопје",
                "Центар",
                "Македонија 10",
                "Дебар Маало"));

        string fingerprint = ListingLocationFingerprint.Compute(location);

        fingerprint.Should().Be(
            "v1:0d465c65eee5a67292471d475dee5ca19b45c700f9d79fcf3f91e66dfdcb4c1e");
        fingerprint.Should().StartWith(
            ListingLocationFingerprint.CurrentVersionPrefix);
        ListingLocationFingerprint.CurrentVersion.Should().Be(1);
    }

    [Fact]
    public void Compute_IsIdenticalForRepeatedAndCanonicalEquivalentInput()
    {
        CanonicalListingLocation baseline = Create(
            Input("en", "Skopje", "Centar", "Address", null));
        CanonicalListingLocation equivalent = Create(
            Input(
                "\u00A0EN\u3000",
                "\u00A0Skopje\u3000",
                " Centar ",
                "\tAddress\r\n",
                "\u2009"));

        string first = ListingLocationFingerprint.Compute(baseline);
        string repeated = ListingLocationFingerprint.Compute(baseline);
        string normalizedEquivalent =
            ListingLocationFingerprint.Compute(equivalent);

        repeated.Should().Be(first);
        normalizedEquivalent.Should().Be(first);
    }

    [Theory]
    [InlineData("language")]
    [InlineData("city")]
    [InlineData("municipality")]
    [InlineData("addressLine")]
    [InlineData("neighborhood")]
    public void Compute_ChangesForEveryLocationIdentityComponent(
        string component)
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

        ListingLocationFingerprint.Compute(changed).Should().NotBe(
            ListingLocationFingerprint.Compute(baseline));
    }

    [Fact]
    public void Compute_IsIndependentOfTranslationOrder()
    {
        CanonicalListingLocation first = Create(
            Input("mk", "Скопје"),
            Input("en", "Skopje"));
        CanonicalListingLocation reordered = Create(
            Input("en", "Skopje"),
            Input("mk", "Скопје"));

        ListingLocationFingerprint.Compute(reordered).Should().Be(
            ListingLocationFingerprint.Compute(first));
    }

    [Fact]
    public void Compute_ChangesWhenLanguageIsAddedOrRemoved()
    {
        CanonicalListingLocation oneLanguage = Create(Input("en", "Skopje"));
        CanonicalListingLocation twoLanguages = Create(
            Input("en", "Skopje"),
            Input("mk", "Скопје"));

        ListingLocationFingerprint.Compute(twoLanguages).Should().NotBe(
            ListingLocationFingerprint.Compute(oneLanguage));
    }

    [Fact]
    public void Compute_LengthPrefixPreventsConcatenationAmbiguity()
    {
        CanonicalListingLocation left = Create(
            Input("en", "ab", "c", null, null));
        CanonicalListingLocation right = Create(
            Input("en", "a", "bc", null, null));

        ListingLocationFingerprint.Compute(left).Should().NotBe(
            ListingLocationFingerprint.Compute(right));
    }

    [Fact]
    public void Compute_NullOptionalValueIsUnambiguous()
    {
        CanonicalListingLocation nullNeighborhood = Create(
            Input("en", "Skopje", "Centar", "Address", null));
        CanonicalListingLocation textNeighborhood = Create(
            Input("en", "Skopje", "Centar", "Address", "null"));

        ListingLocationFingerprint.Compute(nullNeighborhood).Should().NotBe(
            ListingLocationFingerprint.Compute(textNeighborhood));
    }

    [Fact]
    public void Compute_IsIndependentOfCurrentCultureAndUiCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CanonicalListingLocation location = Create(
                Input("MK", " Штип ", "Штип", "Маршал Тито 1", null));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            string turkish = ListingLocationFingerprint.Compute(location);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            string french = ListingLocationFingerprint.Compute(location);

            french.Should().Be(turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
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
