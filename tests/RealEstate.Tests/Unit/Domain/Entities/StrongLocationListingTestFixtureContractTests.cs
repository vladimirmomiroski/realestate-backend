using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class StrongLocationListingTestFixtureContractTests
{
    [Fact]
    public void CreateUnresolvedValidDraft_HasSupportedDraftShapeWithoutLocation()
    {
        Listing listing =
            StrongLocationListingTestFixtures.CreateUnresolvedValidDraft();

        listing.Status.Should().Be(ListingStatus.Draft);
        listing.Translations.Should().ContainSingle();
        ListingTranslation translation = listing.Translations.Single();
        ListingTranslationRules.IsCanonicalLanguageCode(
                translation.LanguageCode)
            .Should().BeTrue();
        translation.Title.Should().NotBeNullOrWhiteSpace();
        translation.City.Should().BeNull();
        translation.Municipality.Should().BeNull();
        translation.AddressLine.Should().BeNull();
        translation.Description.Should().BeNull();
        translation.Neighborhood.Should().BeNull();
        AssertUnresolvedLocation(listing);
    }

    [Fact]
    public void CreatePublishableConfirmedDraft_HasFinalTranslationAndRootTruth()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();

        listing.Status.Should().Be(ListingStatus.Draft);
        listing.Translations.Should().NotBeEmpty();
        listing.Translations.Should().OnlyContain(translation =>
            ListingTranslationRules.IsCanonicalLanguageCode(
                translation.LanguageCode) &&
            !string.IsNullOrWhiteSpace(translation.Title) &&
            !string.IsNullOrWhiteSpace(translation.City) &&
            !string.IsNullOrWhiteSpace(translation.Municipality) &&
            !string.IsNullOrWhiteSpace(translation.AddressLine) &&
            !string.IsNullOrWhiteSpace(translation.Description));
        listing.Translations.Should().OnlyContain(translation =>
            translation.Neighborhood == null);
        listing.EvaluatePublicationReadiness().IsReady.Should().BeTrue();
        AssertConfirmedLocation(listing);
    }

    [Fact]
    public void CreateValidActive_PublishesOnlyCompleteStrongLocationFixture()
    {
        Listing listing =
            StrongLocationListingTestFixtures.CreateValidActive();

        listing.Status.Should().Be(ListingStatus.Active);
        listing.Translations.Should().OnlyContain(translation =>
            !string.IsNullOrWhiteSpace(translation.Municipality) &&
            !string.IsNullOrWhiteSpace(translation.AddressLine));
        AssertConfirmedLocation(listing);
    }

    [Fact]
    public void CreateCorruptActiveForUnitTest_KeepsCorruptionInMemoryOnly()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item => item.ClearLocation());

        listing.Status.Should().Be(ListingStatus.Active);
        AssertUnresolvedLocation(listing);
    }

    [Fact]
    public void CreateDirectDatabaseActivationRejectionSetup_IsDraftWithoutChildren()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateDirectDatabaseActivationRejectionSetup();

        listing.Status.Should().Be(ListingStatus.Draft);
        listing.Translations.Should().BeEmpty();
        AssertUnresolvedLocation(listing);
    }

    [Fact]
    public void AttachTrustedTestOnlyConfirmedLocation_WithBothCoordinatesAbsent_UsesFixedPair()
    {
        Listing listing =
            StrongLocationListingTestFixtures.CreateUnresolvedValidDraft();

        StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(listing);

        AssertConfirmedLocation(listing);
    }

    [Fact]
    public void AttachTrustedTestOnlyConfirmedLocation_WithBothCoordinatesPresent_PreservesPair()
    {
        const decimal latitude = 42.100001m;
        const decimal longitude = 22.100001m;
        Listing listing =
            StrongLocationListingTestFixtures.CreateUnresolvedValidDraft();
        MaterializeCoordinatePairForUnitTest(
            listing,
            latitude,
            longitude);

        StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(listing);

        AssertConfirmedLocation(listing, latitude, longitude);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AttachTrustedTestOnlyConfirmedLocation_WithOneSidedPair_RejectsWithoutSnapshot(
        bool hasLatitude)
    {
        decimal? latitude = hasLatitude ? 42.100001m : null;
        decimal? longitude = hasLatitude ? null : 22.100001m;
        Listing listing =
            StrongLocationListingTestFixtures.CreateUnresolvedValidDraft();
        MaterializeCoordinatePairForUnitTest(
            listing,
            latitude,
            longitude);

        Action action = () => StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(listing);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage(
                "Trusted test coordinates must be either both present or both absent.");
        listing.Latitude.Should().Be(latitude);
        listing.Longitude.Should().Be(longitude);
        listing.LocationPrecision.Should().BeNull();
        listing.GeocodingProviderKey.Should().BeNull();
        listing.GeocodingResultReference.Should().BeNull();
        listing.GeocodedDisplayName.Should().BeNull();
        listing.LocationConfirmedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(Listing.Status))]
    [InlineData(nameof(Listing.Latitude))]
    [InlineData(nameof(Listing.Longitude))]
    [InlineData(nameof(Listing.LocationPrecision))]
    [InlineData(nameof(Listing.GeocodingProviderKey))]
    [InlineData(nameof(Listing.GeocodingResultReference))]
    [InlineData(nameof(Listing.GeocodedDisplayName))]
    [InlineData(nameof(Listing.LocationConfirmedAtUtc))]
    public void LifecycleAndLocationProperties_DoNotExposePublicSetters(
        string propertyName)
    {
        System.Reflection.PropertyInfo property = typeof(Listing)
            .GetProperty(propertyName)!;

        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse();
    }

    private static void AssertConfirmedLocation(Listing listing)
    {
        AssertConfirmedLocation(
            listing,
            StrongLocationListingTestFixtures.ConfirmedLatitude,
            StrongLocationListingTestFixtures.ConfirmedLongitude);
    }

    private static void AssertConfirmedLocation(
        Listing listing,
        decimal expectedLatitude,
        decimal expectedLongitude)
    {
        listing.Latitude.Should().Be(expectedLatitude);
        listing.Longitude.Should().Be(expectedLongitude);
        listing.LocationPrecision.Should().Be(LocationPrecision.ExactAddress);
        listing.GeocodingProviderKey.Should().Be(
            StrongLocationListingTestFixtures.TestProviderKey);
        listing.GeocodingResultReference.Should().Be(
            StrongLocationListingTestFixtures.TestResultReference);
        listing.GeocodedDisplayName.Should().Be(
            StrongLocationListingTestFixtures.TestDisplayName);
        listing.LocationConfirmedAtUtc.Should().Be(
            StrongLocationListingTestFixtures.ConfirmedAtUtc);
    }

    private static void MaterializeCoordinatePairForUnitTest(
        Listing listing,
        decimal? latitude,
        decimal? longitude)
    {
        typeof(Listing).GetProperty(nameof(Listing.Latitude))!
            .SetValue(listing, latitude);
        typeof(Listing).GetProperty(nameof(Listing.Longitude))!
            .SetValue(listing, longitude);
    }

    private static void AssertUnresolvedLocation(Listing listing)
    {
        listing.Latitude.Should().BeNull();
        listing.Longitude.Should().BeNull();
        listing.LocationPrecision.Should().BeNull();
        listing.GeocodingProviderKey.Should().BeNull();
        listing.GeocodingResultReference.Should().BeNull();
        listing.GeocodedDisplayName.Should().BeNull();
        listing.LocationConfirmedAtUtc.Should().BeNull();
    }
}
