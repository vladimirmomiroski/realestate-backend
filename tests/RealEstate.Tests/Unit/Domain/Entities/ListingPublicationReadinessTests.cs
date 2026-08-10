using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

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
    public void Publish_WhenReadyDraft_ActivatesListing()
    {
        Listing listing = CreateListing(CreateValidTranslation("en"));

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
        Listing listing = CreateListing(CreateValidTranslation("en"));
        listing.Publish();

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
        Listing listing = CreateMalformedActiveListing(
            translation => translation.City = null);

        listing.Unpublish();

        listing.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public void Archive_WhenMalformedActive_ArchivesListing()
    {
        Listing listing = CreateMalformedActiveListing(
            translation => translation.Description = null);

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
        ListingTranslation translation = CreateValidTranslation("en");
        Listing listing = CreateListing(translation);
        listing.Publish().IsReady.Should().BeTrue();

        corruptTranslation(translation);

        return listing;
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
        return new Listing
        {
            Id = Guid.NewGuid(),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 100_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            Translations = translations.ToList()
        };
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
            Description = "Ready description"
        };
    }
}
