using FluentAssertions;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class LocationConfirmationTokenClaimsTests
{
    [Fact]
    public void Create_PreservesEveryProvenanceClaimExactly()
    {
        Guid listingId = Guid.NewGuid();
        Guid actorUserId = Guid.NewGuid();
        string reference = "opaque/reference:AbC-123_\U0001F3E0";
        string fingerprint = CreateFingerprint();

        LocationConfirmationTokenClaims claims =
            LocationConfirmationTokenClaims.Create(
                listingId,
                actorUserId,
                "provider",
                reference,
                "mk",
                fingerprint);

        claims.ListingId.Should().Be(listingId);
        claims.ActorUserId.Should().Be(actorUserId);
        claims.ProviderKey.Should().Be("provider");
        claims.ProviderResultReference.Should().Be(reference);
        claims.LanguageCode.Should().Be("mk");
        claims.LocationFingerprint.Should().Be(fingerprint);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_EnforcesProviderKeyUnicodeScalarBoundary(
        bool withinBoundary)
    {
        string providerKey = new(
            'p',
            withinBoundary ? 64 : 65);

        Action act = () => CreateClaims(providerKey: providerKey);

        if (withinBoundary)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [Fact]
    public void Create_AcceptsExactly512SupplementaryUnicodeScalars()
    {
        string reference = string.Concat(
            Enumerable.Repeat("\U0001F3E0", 512));

        LocationConfirmationTokenClaims claims =
            CreateClaims(providerResultReference: reference);

        claims.ProviderResultReference.Should().Be(reference);
        claims.ProviderResultReference.EnumerateRunes().Should().HaveCount(512);
        claims.ProviderResultReference.Length.Should().Be(1024);
    }

    [Fact]
    public void Create_RejectsReferenceOver512UnicodeScalarsWithoutTruncating()
    {
        string reference = string.Concat(
            Enumerable.Repeat("\U0001F3E0", 513));

        Action act = () => CreateClaims(
            providerResultReference: reference);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_RejectsInvalidUtf16InsteadOfChangingTheReference()
    {
        string invalidReference = "reference\uD800";

        Action act = () => CreateClaims(
            providerResultReference: invalidReference);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*valid Unicode scalar values*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" provider")]
    [InlineData("provider ")]
    public void Create_RejectsBlankOrBoundaryWhitespaceProviderKey(
        string providerKey)
    {
        Action act = () => CreateClaims(providerKey: providerKey);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    [InlineData(" reference")]
    [InlineData("reference\r\n")]
    public void Create_RejectsBlankOrBoundaryWhitespaceReference(
        string reference)
    {
        Action act = () => CreateClaims(
            providerResultReference: reference);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsEmptyActorAndListingIdentifiers()
    {
        Action emptyListing = () => CreateClaims(listingId: Guid.Empty);
        Action emptyActor = () => CreateClaims(actorUserId: Guid.Empty);

        emptyListing.Should().Throw<ArgumentException>();
        emptyActor.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("MK")]
    [InlineData(" mk ")]
    [InlineData("not_a_language")]
    public void Create_RejectsNoncanonicalLanguageCode(string languageCode)
    {
        Action act = () => CreateClaims(languageCode: languageCode);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("v2:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("v1:0000")]
    [InlineData("v1:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("v1:gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Create_RejectsMalformedOrUnsupportedFingerprint(
        string fingerprint)
    {
        Action act = () => CreateClaims(
            locationFingerprint: fingerprint);

        act.Should().Throw<ArgumentException>();
    }

    private static LocationConfirmationTokenClaims CreateClaims(
        Guid? listingId = null,
        Guid? actorUserId = null,
        string providerKey = "provider",
        string providerResultReference = "reference",
        string languageCode = "mk",
        string? locationFingerprint = null)
    {
        return LocationConfirmationTokenClaims.Create(
            listingId ?? Guid.NewGuid(),
            actorUserId ?? Guid.NewGuid(),
            providerKey,
            providerResultReference,
            languageCode,
            locationFingerprint ?? CreateFingerprint());
    }

    private static string CreateFingerprint()
    {
        CanonicalListingLocation location = CanonicalListingLocation.From(
        [
            new CanonicalListingLocationInput(
                "mk",
                "Скопје",
                "Центар",
                "Македонија 10",
                "Дебар Маало")
        ]);

        return ListingLocationFingerprint.Compute(location);
    }
}
