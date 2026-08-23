using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class ListingPublicationReadinessTests
{
    public static TheoryData<string> CanonicalBoundaryWhitespace =>
        new()
        {
            " ",
            "\t",
            "\r\n",
            "\u00A0",
            "\u2003",
            "\u3000"
        };

    public static TheoryData<string> AllBoundaryWhitespace
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (char character in
                     ListingTranslationRules.BoundaryWhitespaceCharacters)
            {
                data.Add(character.ToString());
            }

            return data;
        }
    }

    public static TheoryData<string?> InvalidLanguageCodes =>
        new()
        {
            null,
            string.Empty,
            " ",
            "EN",
            "e",
            "en_uk",
            "en-"
        };

    [Fact]
    public void EvaluatePublicationReadiness_WithoutTranslations_IsNotReady()
    {
        var listing = new Listing();

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        AssertSingleViolation(
            result,
            ListingPublicationReadinessViolationCode.MissingTranslation,
            translationId: null);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithOneValidTranslation_IsReady()
    {
        Listing listing = CreateListing(CreateValidTranslation("en"));

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        result.IsReady.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithMultipleValidTranslations_IsReady()
    {
        Listing listing = CreateListing(
            CreateValidTranslation("en"),
            CreateValidTranslation("mk"),
            CreateValidTranslation("sq"));

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        result.IsReady.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void EvaluatePublicationReadiness_WhenOneOfMultipleTranslationsIsInvalid_IsNotReady()
    {
        ListingTranslation invalid = CreateValidTranslation("mk");
        invalid.City = null;
        Listing listing = CreateListing(
            CreateValidTranslation("en"),
            invalid);

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        AssertSingleViolation(
            result,
            ListingPublicationReadinessViolationCode.InvalidCity,
            invalid.Id);
    }

    [Theory]
    [InlineData(ListingPublicationReadinessViolationCode.InvalidMunicipality)]
    [InlineData(ListingPublicationReadinessViolationCode.InvalidAddressLine)]
    public void EvaluatePublicationReadiness_WhenOneTranslationHasInvalidRequiredLocationText_IsNotReady(
        ListingPublicationReadinessViolationCode expectedCode)
    {
        ListingTranslation invalid = CreateValidTranslation("mk");

        if (expectedCode ==
            ListingPublicationReadinessViolationCode.InvalidMunicipality)
        {
            invalid.Municipality = null;
        }
        else
        {
            invalid.AddressLine = null;
        }

        Listing listing = CreateListing(
            CreateValidTranslation("en"),
            invalid);

        AssertSingleViolation(
            listing.EvaluatePublicationReadiness(),
            expectedCode,
            invalid.Id);
    }

    [Theory]
    [MemberData(nameof(InvalidLanguageCodes))]
    public void EvaluatePublicationReadiness_WithInvalidLanguageCode_IsNotReady(
        string? languageCode)
    {
        ListingTranslation translation = CreateValidTranslation("en");
        translation.LanguageCode = languageCode!;
        Listing listing = CreateListing(translation);

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        AssertSingleViolation(
            result,
            ListingPublicationReadinessViolationCode.InvalidLanguageCode,
            translation.Id);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("mk")]
    [InlineData("en-us")]
    [InlineData("sr-latn")]
    public void EvaluatePublicationReadiness_WithCanonicalLanguageCode_IsReady(
        string languageCode)
    {
        Listing listing = CreateListing(CreateValidTranslation(languageCode));

        listing.EvaluatePublicationReadiness().IsReady.Should().BeTrue();
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithEmptyTitle_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.Title = string.Empty,
            ListingPublicationReadinessViolationCode.InvalidTitle);
    }

    [Theory]
    [MemberData(nameof(CanonicalBoundaryWhitespace))]
    public void EvaluatePublicationReadiness_WithWhitespaceOnlyTitle_IsNotReady(
        string whitespace)
    {
        AssertInvalidRequiredField(
            translation => translation.Title = whitespace,
            ListingPublicationReadinessViolationCode.InvalidTitle);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithNullCity_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.City = null,
            ListingPublicationReadinessViolationCode.InvalidCity);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithEmptyCity_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.City = string.Empty,
            ListingPublicationReadinessViolationCode.InvalidCity);
    }

    [Theory]
    [MemberData(nameof(CanonicalBoundaryWhitespace))]
    public void EvaluatePublicationReadiness_WithWhitespaceOnlyCity_IsNotReady(
        string whitespace)
    {
        AssertInvalidRequiredField(
            translation => translation.City = whitespace,
            ListingPublicationReadinessViolationCode.InvalidCity);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithNullMunicipality_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.Municipality = null,
            ListingPublicationReadinessViolationCode.InvalidMunicipality);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithEmptyMunicipality_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.Municipality = string.Empty,
            ListingPublicationReadinessViolationCode.InvalidMunicipality);
    }

    [Theory]
    [MemberData(nameof(AllBoundaryWhitespace))]
    public void EvaluatePublicationReadiness_WithWhitespaceOnlyMunicipality_IsNotReady(
        string whitespace)
    {
        AssertInvalidRequiredField(
            translation => translation.Municipality = whitespace,
            ListingPublicationReadinessViolationCode.InvalidMunicipality);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithNullAddressLine_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.AddressLine = null,
            ListingPublicationReadinessViolationCode.InvalidAddressLine);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithEmptyAddressLine_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.AddressLine = string.Empty,
            ListingPublicationReadinessViolationCode.InvalidAddressLine);
    }

    [Theory]
    [MemberData(nameof(AllBoundaryWhitespace))]
    public void EvaluatePublicationReadiness_WithWhitespaceOnlyAddressLine_IsNotReady(
        string whitespace)
    {
        AssertInvalidRequiredField(
            translation => translation.AddressLine = whitespace,
            ListingPublicationReadinessViolationCode.InvalidAddressLine);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithNullDescription_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.Description = null,
            ListingPublicationReadinessViolationCode.InvalidDescription);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithEmptyDescription_IsNotReady()
    {
        AssertInvalidRequiredField(
            translation => translation.Description = string.Empty,
            ListingPublicationReadinessViolationCode.InvalidDescription);
    }

    [Theory]
    [MemberData(nameof(CanonicalBoundaryWhitespace))]
    public void EvaluatePublicationReadiness_WithWhitespaceOnlyDescription_IsNotReady(
        string whitespace)
    {
        AssertInvalidRequiredField(
            translation => translation.Description = whitespace,
            ListingPublicationReadinessViolationCode.InvalidDescription);
    }

    [Theory]
    [InlineData(" title ", ListingPublicationReadinessViolationCode.InvalidTitle)]
    [InlineData(" city ", ListingPublicationReadinessViolationCode.InvalidCity)]
    [InlineData(" municipality ", ListingPublicationReadinessViolationCode.InvalidMunicipality)]
    [InlineData(" address ", ListingPublicationReadinessViolationCode.InvalidAddressLine)]
    [InlineData(" description ", ListingPublicationReadinessViolationCode.InvalidDescription)]
    public void EvaluatePublicationReadiness_WithUntrimmedRequiredContent_IsNotReady(
        string value,
        ListingPublicationReadinessViolationCode expectedCode)
    {
        Action<ListingTranslation> corrupt = expectedCode switch
        {
            ListingPublicationReadinessViolationCode.InvalidTitle =>
                translation => translation.Title = value,
            ListingPublicationReadinessViolationCode.InvalidCity =>
                translation => translation.City = value,
            ListingPublicationReadinessViolationCode.InvalidMunicipality =>
                translation => translation.Municipality = value,
            ListingPublicationReadinessViolationCode.InvalidAddressLine =>
                translation => translation.AddressLine = value,
            ListingPublicationReadinessViolationCode.InvalidDescription =>
                translation => translation.Description = value,
            _ => throw new ArgumentOutOfRangeException(
                nameof(expectedCode),
                expectedCode,
                null)
        };

        AssertInvalidRequiredField(corrupt, expectedCode);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithUnresolvedRoot_ReportsMissingConfirmedLocation()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item => item.ClearLocation());

        AssertSingleViolation(
            listing.EvaluatePublicationReadiness(),
            ListingPublicationReadinessViolationCode.MissingConfirmedLocation,
            translationId: null);
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithLegacyUnverifiedRoot_ReportsMissingConfirmedLocation()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item =>
            {
                item.ClearLocation();
                SetListingProperty(
                    item,
                    nameof(Listing.Latitude),
                    StrongLocationListingTestFixtures.ConfirmedLatitude);
                SetListingProperty(
                    item,
                    nameof(Listing.Longitude),
                    StrongLocationListingTestFixtures.ConfirmedLongitude);
            });

        AssertSingleViolation(
            listing.EvaluatePublicationReadiness(),
            ListingPublicationReadinessViolationCode.MissingConfirmedLocation,
            translationId: null);
    }

    [Theory]
    [InlineData(PartialRootCorruption.LatitudeOnly)]
    [InlineData(PartialRootCorruption.LongitudeOnly)]
    [InlineData(PartialRootCorruption.MissingLatitude)]
    [InlineData(PartialRootCorruption.MissingLongitude)]
    [InlineData(PartialRootCorruption.MissingPrecision)]
    [InlineData(PartialRootCorruption.MissingProviderKey)]
    [InlineData(PartialRootCorruption.MissingResultReference)]
    [InlineData(PartialRootCorruption.MissingConfirmationTime)]
    [InlineData(PartialRootCorruption.MetadataWithoutCoordinates)]
    public void EvaluatePublicationReadiness_WithPartialRoot_ReportsInvalidConfirmedLocation(
        PartialRootCorruption corruption)
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item =>
                CorruptPartialRoot(item, corruption));

        AssertSingleViolation(
            listing.EvaluatePublicationReadiness(),
            ListingPublicationReadinessViolationCode.InvalidConfirmedLocation,
            translationId: null);
    }

    [Theory]
    [InlineData(InvalidConfirmedRootCorruption.LatitudeBelowRange)]
    [InlineData(InvalidConfirmedRootCorruption.LatitudeAboveRange)]
    [InlineData(InvalidConfirmedRootCorruption.LongitudeBelowRange)]
    [InlineData(InvalidConfirmedRootCorruption.LongitudeAboveRange)]
    [InlineData(InvalidConfirmedRootCorruption.UndefinedPrecision)]
    [InlineData(InvalidConfirmedRootCorruption.EmptyProviderKey)]
    [InlineData(InvalidConfirmedRootCorruption.UntrimmedProviderKey)]
    [InlineData(InvalidConfirmedRootCorruption.ProviderKeyTooLong)]
    [InlineData(InvalidConfirmedRootCorruption.EmptyResultReference)]
    [InlineData(InvalidConfirmedRootCorruption.UntrimmedResultReference)]
    [InlineData(InvalidConfirmedRootCorruption.ResultReferenceTooLong)]
    [InlineData(InvalidConfirmedRootCorruption.EmptyDisplayName)]
    [InlineData(InvalidConfirmedRootCorruption.UntrimmedDisplayName)]
    [InlineData(InvalidConfirmedRootCorruption.DisplayNameTooLong)]
    public void EvaluatePublicationReadiness_WithInvalidConfirmedRoot_ReportsInvalidConfirmedLocation(
        InvalidConfirmedRootCorruption corruption)
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item =>
                CorruptConfirmedRoot(item, corruption));

        AssertSingleViolation(
            listing.EvaluatePublicationReadiness(),
            ListingPublicationReadinessViolationCode.InvalidConfirmedLocation,
            translationId: null);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void EvaluatePublicationReadiness_WithInclusiveCoordinateBoundaries_IsReady(
        decimal latitude,
        decimal longitude)
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();
        listing.ConfirmLocation(
            latitude,
            longitude,
            LocationPrecision.Approximate,
            StrongLocationListingTestFixtures.TestProviderKey,
            StrongLocationListingTestFixtures.TestResultReference,
            geocodedDisplayName: null,
            StrongLocationListingTestFixtures.ConfirmedAtUtc);

        listing.EvaluatePublicationReadiness().IsReady.Should().BeTrue();
    }

    [Theory]
    [InlineData(LocationPrecision.ExactAddress)]
    [InlineData(LocationPrecision.Street)]
    [InlineData(LocationPrecision.Neighborhood)]
    [InlineData(LocationPrecision.Municipality)]
    [InlineData(LocationPrecision.City)]
    [InlineData(LocationPrecision.Approximate)]
    public void EvaluatePublicationReadiness_WithDefinedPrecision_IsReady(
        LocationPrecision precision)
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();
        listing.ConfirmLocation(
            StrongLocationListingTestFixtures.ConfirmedLatitude,
            StrongLocationListingTestFixtures.ConfirmedLongitude,
            precision,
            StrongLocationListingTestFixtures.TestProviderKey,
            StrongLocationListingTestFixtures.TestResultReference,
            geocodedDisplayName: null,
            StrongLocationListingTestFixtures.ConfirmedAtUtc);

        listing.EvaluatePublicationReadiness().IsReady.Should().BeTrue();
    }

    [Fact]
    public void EvaluatePublicationReadiness_WithMultipleFailures_ReturnsDeterministicSanitizedCodes()
    {
        const string ProviderPayload = " private-provider-payload ";
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();
        ListingTranslation first = listing.Translations.First();
        ListingTranslation second = listing.Translations.Skip(1).First();
        first.Municipality = null;
        second.AddressLine = null;
        SetListingProperty(
            listing,
            nameof(Listing.GeocodingProviderKey),
            ProviderPayload);

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        result.Violations.Should().Equal(
            new ListingPublicationReadinessViolation(
                ListingPublicationReadinessViolationCode.InvalidMunicipality,
                first.Id),
            new ListingPublicationReadinessViolation(
                ListingPublicationReadinessViolationCode.InvalidAddressLine,
                second.Id),
            new ListingPublicationReadinessViolation(
                ListingPublicationReadinessViolationCode.InvalidConfirmedLocation,
                TranslationId: null));
        result.Violations.Select(violation => violation.ToString())
            .Should().NotContain(value => value.Contains(
                ProviderPayload,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Publish_WhenReadyDraft_ActivatesListing()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();

        ListingPublicationReadinessResult result = listing.Publish();

        result.IsReady.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public void Publish_WhenIncompleteDraft_ReturnsReadinessFailureAndRemainsDraft()
    {
        Listing listing = CreateListing();

        ListingPublicationReadinessResult result = listing.Publish();

        result.IsReady.Should().BeFalse();
        result.Violations.Should().ContainSingle(violation =>
            violation.Code ==
            ListingPublicationReadinessViolationCode.MissingTranslation);
        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public void Publish_WhenValidActive_IsIdempotent()
    {
        Listing listing =
            StrongLocationListingTestFixtures.CreateValidActive();

        ListingPublicationReadinessResult result = listing.Publish();

        result.IsReady.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public void Publish_WhenMalformedActive_ReturnsReadinessFailureAndRemainsActive()
    {
        Listing listing = CreateMalformedActiveListing(
            translation => translation.Description = null);

        ListingPublicationReadinessResult result = listing.Publish();

        result.IsReady.Should().BeFalse();
        result.Violations.Should().ContainSingle(violation =>
            violation.Code ==
            ListingPublicationReadinessViolationCode.InvalidDescription);
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public void Publish_WhenActiveRootIsMalformed_ReturnsReadinessFailureAndRemainsActive()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item => item.ClearLocation());

        ListingPublicationReadinessResult result = listing.Publish();

        AssertSingleViolation(
            result,
            ListingPublicationReadinessViolationCode.MissingConfirmedLocation,
            translationId: null);
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Theory]
    [InlineData(ListingStatus.Archived)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public void Publish_WhenLifecycleCannotPublish_ThrowsBeforeReadiness(
        ListingStatus status)
    {
        Listing listing = CreateListingInStatus(status);
        listing.Translations.Clear();

        Action publish = () => listing.Publish();

        publish.Should().Throw<InvalidOperationException>()
            .WithMessage("Only draft listings can be published.");
        listing.Status.Should().Be(status);
    }

    [Fact]
    public void Unpublish_WhenMalformedActive_ReturnsListingToDraft()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item => item.ClearLocation());

        listing.Unpublish();

        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public void Archive_WhenMalformedActive_ArchivesListing()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(item => item.ClearLocation());

        listing.Archive();

        listing.Status.Should().Be(ListingStatus.Archived);
    }

    private static void AssertInvalidRequiredField(
        Action<ListingTranslation> corrupt,
        ListingPublicationReadinessViolationCode expectedCode)
    {
        ListingTranslation translation = CreateValidTranslation("en");
        corrupt(translation);
        Listing listing = CreateListing(translation);

        ListingPublicationReadinessResult result =
            listing.EvaluatePublicationReadiness();

        AssertSingleViolation(result, expectedCode, translation.Id);
    }

    private static void AssertSingleViolation(
        ListingPublicationReadinessResult result,
        ListingPublicationReadinessViolationCode expectedCode,
        Guid? translationId)
    {
        result.IsReady.Should().BeFalse();
        result.Violations.Should().ContainSingle();
        result.Violations.Single().Code.Should().Be(expectedCode);
        result.Violations.Single().TranslationId.Should().Be(translationId);
    }

    private static Listing CreateMalformedActiveListing(
        Action<ListingTranslation> corruptTranslation)
    {
        return StrongLocationListingTestFixtures
            .CreateCorruptActiveForUnitTest(listing =>
                corruptTranslation(listing.Translations.First()));
    }

    private static Listing CreateListingInStatus(ListingStatus status)
    {
        Listing listing = CreateListing(CreateValidTranslation("en"));

        switch (status)
        {
            case ListingStatus.Archived:
                listing.Archive();
                break;
            case ListingStatus.Reserved:
            case ListingStatus.Sold:
            case ListingStatus.Rented:
                ListingStatusTestMaterializer.MaterializeUnreachableStatus(
                    listing,
                    status);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return listing;
    }

    private static Listing CreateListing(
        params ListingTranslation[] translations)
    {
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 100_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            Translations = translations.ToList()
        };

        if (translations.Length > 0)
        {
            StrongLocationListingTestFixtures
                .AttachTrustedTestOnlyConfirmedLocation(listing);
        }

        return listing;
    }

    private static ListingTranslation CreateValidTranslation(
        string languageCode)
    {
        return new ListingTranslation
        {
            Id = Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = "Ready title",
            City = "Skopje",
            Municipality = "Centar",
            AddressLine = "Macedonia Street 1",
            Description = "Ready description"
        };
    }

    private static void CorruptPartialRoot(
        Listing listing,
        PartialRootCorruption corruption)
    {
        switch (corruption)
        {
            case PartialRootCorruption.LatitudeOnly:
                listing.ClearLocation();
                SetListingProperty(
                    listing,
                    nameof(Listing.Latitude),
                    StrongLocationListingTestFixtures.ConfirmedLatitude);
                break;
            case PartialRootCorruption.LongitudeOnly:
                listing.ClearLocation();
                SetListingProperty(
                    listing,
                    nameof(Listing.Longitude),
                    StrongLocationListingTestFixtures.ConfirmedLongitude);
                break;
            case PartialRootCorruption.MissingLatitude:
                SetListingProperty<decimal?>(
                    listing,
                    nameof(Listing.Latitude),
                    null);
                break;
            case PartialRootCorruption.MissingLongitude:
                SetListingProperty<decimal?>(
                    listing,
                    nameof(Listing.Longitude),
                    null);
                break;
            case PartialRootCorruption.MissingPrecision:
                SetListingProperty<LocationPrecision?>(
                    listing,
                    nameof(Listing.LocationPrecision),
                    null);
                break;
            case PartialRootCorruption.MissingProviderKey:
                SetListingProperty<string?>(
                    listing,
                    nameof(Listing.GeocodingProviderKey),
                    null);
                break;
            case PartialRootCorruption.MissingResultReference:
                SetListingProperty<string?>(
                    listing,
                    nameof(Listing.GeocodingResultReference),
                    null);
                break;
            case PartialRootCorruption.MissingConfirmationTime:
                SetListingProperty<DateTime?>(
                    listing,
                    nameof(Listing.LocationConfirmedAtUtc),
                    null);
                break;
            case PartialRootCorruption.MetadataWithoutCoordinates:
                listing.ClearLocation();
                SetListingProperty<LocationPrecision?>(
                    listing,
                    nameof(Listing.LocationPrecision),
                    LocationPrecision.City);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(corruption),
                    corruption,
                    null);
        }
    }

    private static void CorruptConfirmedRoot(
        Listing listing,
        InvalidConfirmedRootCorruption corruption)
    {
        switch (corruption)
        {
            case InvalidConfirmedRootCorruption.LatitudeBelowRange:
                SetListingProperty(
                    listing,
                    nameof(Listing.Latitude),
                    ListingLocationRules.MinimumLatitude - 0.000001m);
                break;
            case InvalidConfirmedRootCorruption.LatitudeAboveRange:
                SetListingProperty(
                    listing,
                    nameof(Listing.Latitude),
                    ListingLocationRules.MaximumLatitude + 0.000001m);
                break;
            case InvalidConfirmedRootCorruption.LongitudeBelowRange:
                SetListingProperty(
                    listing,
                    nameof(Listing.Longitude),
                    ListingLocationRules.MinimumLongitude - 0.000001m);
                break;
            case InvalidConfirmedRootCorruption.LongitudeAboveRange:
                SetListingProperty(
                    listing,
                    nameof(Listing.Longitude),
                    ListingLocationRules.MaximumLongitude + 0.000001m);
                break;
            case InvalidConfirmedRootCorruption.UndefinedPrecision:
                SetListingProperty<LocationPrecision?>(
                    listing,
                    nameof(Listing.LocationPrecision),
                    (LocationPrecision)int.MaxValue);
                break;
            case InvalidConfirmedRootCorruption.EmptyProviderKey:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodingProviderKey),
                    string.Empty);
                break;
            case InvalidConfirmedRootCorruption.UntrimmedProviderKey:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodingProviderKey),
                    " provider ");
                break;
            case InvalidConfirmedRootCorruption.ProviderKeyTooLong:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodingProviderKey),
                    new string(
                        'p',
                        ListingLocationRules.GeocodingProviderKeyMaxLength + 1));
                break;
            case InvalidConfirmedRootCorruption.EmptyResultReference:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodingResultReference),
                    string.Empty);
                break;
            case InvalidConfirmedRootCorruption.UntrimmedResultReference:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodingResultReference),
                    " reference ");
                break;
            case InvalidConfirmedRootCorruption.ResultReferenceTooLong:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodingResultReference),
                    new string(
                        'r',
                        ListingLocationRules.GeocodingResultReferenceMaxLength + 1));
                break;
            case InvalidConfirmedRootCorruption.EmptyDisplayName:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodedDisplayName),
                    string.Empty);
                break;
            case InvalidConfirmedRootCorruption.UntrimmedDisplayName:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodedDisplayName),
                    " display ");
                break;
            case InvalidConfirmedRootCorruption.DisplayNameTooLong:
                SetListingProperty(
                    listing,
                    nameof(Listing.GeocodedDisplayName),
                    new string(
                        'd',
                        ListingLocationRules.GeocodedDisplayNameMaxLength + 1));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(corruption),
                    corruption,
                    null);
        }
    }

    private static void SetListingProperty<T>(
        Listing listing,
        string propertyName,
        T value)
    {
        typeof(Listing).GetProperty(propertyName)!
            .SetValue(listing, value);
    }

    public enum PartialRootCorruption
    {
        LatitudeOnly,
        LongitudeOnly,
        MissingLatitude,
        MissingLongitude,
        MissingPrecision,
        MissingProviderKey,
        MissingResultReference,
        MissingConfirmationTime,
        MetadataWithoutCoordinates
    }

    public enum InvalidConfirmedRootCorruption
    {
        LatitudeBelowRange,
        LatitudeAboveRange,
        LongitudeBelowRange,
        LongitudeAboveRange,
        UndefinedPrecision,
        EmptyProviderKey,
        UntrimmedProviderKey,
        ProviderKeyTooLong,
        EmptyResultReference,
        UntrimmedResultReference,
        ResultReferenceTooLong,
        EmptyDisplayName,
        UntrimmedDisplayName,
        DisplayNameTooLong
    }
}
