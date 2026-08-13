using System.Reflection;
using System.Text;
using FluentAssertions;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Tests.Unit.Domain.Entities;

public sealed class ListingLocationTests
{
    private static readonly DateTime ConfirmationTime =
        new(2026, 8, 13, 10, 30, 0, DateTimeKind.Utc);

    public static TheoryData<string, int> MetadataScalarLimits =>
        new()
        {
            {
                nameof(Listing.GeocodingProviderKey),
                ListingLocationRules.GeocodingProviderKeyMaxLength
            },
            {
                nameof(Listing.GeocodingResultReference),
                ListingLocationRules.GeocodingResultReferenceMaxLength
            },
            {
                nameof(Listing.GeocodedDisplayName),
                ListingLocationRules.GeocodedDisplayNameMaxLength
            }
        };

    [Theory]
    [InlineData(LocationPrecision.ExactAddress)]
    [InlineData(LocationPrecision.Street)]
    [InlineData(LocationPrecision.Neighborhood)]
    [InlineData(LocationPrecision.Municipality)]
    [InlineData(LocationPrecision.City)]
    [InlineData(LocationPrecision.Approximate)]
    public void ConfirmLocation_EstablishesCompleteSnapshot(
        LocationPrecision precision)
    {
        var listing = new Listing();

        listing.ConfirmLocation(
            -90m,
            -180m,
            precision,
            "provider-Key",
            "Opaque:Result/ABC-123",
            null,
            ConfirmationTime);

        AssertSnapshot(
            listing,
            -90m,
            -180m,
            precision,
            "provider-Key",
            "Opaque:Result/ABC-123",
            null,
            ConfirmationTime);
    }

    [Fact]
    public void ConfirmLocation_AcceptsMaximumCoordinatesAndDisplayName()
    {
        var listing = new Listing();

        listing.ConfirmLocation(
            90m,
            180m,
            LocationPrecision.ExactAddress,
            new string('p', ListingLocationRules.GeocodingProviderKeyMaxLength),
            new string('R', ListingLocationRules.GeocodingResultReferenceMaxLength),
            new string('D', ListingLocationRules.GeocodedDisplayNameMaxLength),
            ConfirmationTime);

        listing.Latitude.Should().Be(90m);
        listing.Longitude.Should().Be(180m);
        listing.GeocodingResultReference.Should().StartWith("R");
        listing.GeocodedDisplayName!.Length.Should().Be(
            ListingLocationRules.GeocodedDisplayNameMaxLength);
    }

    [Theory]
    [MemberData(nameof(MetadataScalarLimits))]
    public void ConfirmLocation_AcceptsSupplementaryUnicodeAtScalarLimit(
        string propertyName,
        int maximumScalarCount)
    {
        var listing = new Listing();
        string value = CreateSupplementaryScalarValue(maximumScalarCount);

        ConfirmWithMetadataValue(listing, propertyName, value);

        value.Length.Should().Be(maximumScalarCount * 2);
        value.EnumerateRunes().Count().Should().Be(maximumScalarCount);
        ReadMetadataValue(listing, propertyName).Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(MetadataScalarLimits))]
    public void ConfirmLocation_RejectsSupplementaryUnicodeOverScalarLimitAtomically(
        string propertyName,
        int maximumScalarCount)
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);
        string value = CreateSupplementaryScalarValue(maximumScalarCount + 1);
        string parameterName = ReadParameterName(propertyName);

        Action action = () => ConfirmWithMetadataValue(
            listing,
            propertyName,
            value);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(parameterName);
        Capture(listing).Should().Be(before);
    }

    [Theory]
    [InlineData(-90.000001)]
    [InlineData(90.000001)]
    public void ConfirmLocation_RejectsOutOfRangeLatitudeAtomically(
        decimal latitude)
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);

        Action action = () => listing.ConfirmLocation(
            latitude,
            21m,
            LocationPrecision.City,
            "new-provider",
            "new-result",
            null,
            ConfirmationTime);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("latitude");
        Capture(listing).Should().Be(before);
    }

    [Theory]
    [InlineData(-180.000001)]
    [InlineData(180.000001)]
    public void ConfirmLocation_RejectsOutOfRangeLongitudeAtomically(
        decimal longitude)
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);

        Action action = () => listing.ConfirmLocation(
            41m,
            longitude,
            LocationPrecision.City,
            "new-provider",
            "new-result",
            null,
            ConfirmationTime);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("longitude");
        Capture(listing).Should().Be(before);
    }

    [Fact]
    public void ConfirmLocation_RejectsUndefinedPrecisionAtomically()
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);

        Action action = () => listing.ConfirmLocation(
            41m,
            21m,
            (LocationPrecision)999,
            "new-provider",
            "new-result",
            null,
            ConfirmationTime);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("precision");
        Capture(listing).Should().Be(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\u00A0provider")]
    [InlineData("provider\u3000")]
    public void ConfirmLocation_RejectsInvalidProviderKeyAtomically(string value)
    {
        AssertInvalidTextIsAtomic(
            (listing, invalid) => listing.ConfirmLocation(
                41m,
                21m,
                LocationPrecision.City,
                invalid,
                "result",
                null,
                ConfirmationTime),
            value,
            "geocodingProviderKey");
    }

    [Fact]
    public void ConfirmLocation_RejectsProviderKeyOverMaximumAtomically()
    {
        AssertInvalidTextIsAtomic(
            (listing, invalid) => listing.ConfirmLocation(
                41m,
                21m,
                LocationPrecision.City,
                invalid,
                "result",
                null,
                ConfirmationTime),
            new string('p', ListingLocationRules.GeocodingProviderKeyMaxLength + 1),
            "geocodingProviderKey",
            outOfRange: true);
    }

    [Fact]
    public void ConfirmLocation_RejectsNullProviderKeyAtomically()
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);

        Action action = () => listing.ConfirmLocation(
            41m,
            21m,
            LocationPrecision.City,
            null!,
            "result",
            null,
            ConfirmationTime);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("geocodingProviderKey");
        Capture(listing).Should().Be(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    [InlineData("\u2003result")]
    [InlineData("result\u00A0")]
    public void ConfirmLocation_RejectsInvalidOpaqueResultReferenceAtomically(
        string value)
    {
        AssertInvalidTextIsAtomic(
            (listing, invalid) => listing.ConfirmLocation(
                41m,
                21m,
                LocationPrecision.City,
                "provider",
                invalid,
                null,
                ConfirmationTime),
            value,
            "geocodingResultReference");
    }

    [Fact]
    public void ConfirmLocation_RejectsResultReferenceOverMaximumAtomically()
    {
        AssertInvalidTextIsAtomic(
            (listing, invalid) => listing.ConfirmLocation(
                41m,
                21m,
                LocationPrecision.City,
                "provider",
                invalid,
                null,
                ConfirmationTime),
            new string('r', ListingLocationRules.GeocodingResultReferenceMaxLength + 1),
            "geocodingResultReference",
            outOfRange: true);
    }

    [Fact]
    public void ConfirmLocation_RejectsNullResultReferenceAtomically()
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);

        Action action = () => listing.ConfirmLocation(
            41m,
            21m,
            LocationPrecision.City,
            "provider",
            null!,
            null,
            ConfirmationTime);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("geocodingResultReference");
        Capture(listing).Should().Be(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    [InlineData("\u3000display")]
    [InlineData("display\u202F")]
    public void ConfirmLocation_RejectsInvalidDisplayNameAtomically(string value)
    {
        AssertInvalidTextIsAtomic(
            (listing, invalid) => listing.ConfirmLocation(
                41m,
                21m,
                LocationPrecision.City,
                "provider",
                "result",
                invalid,
                ConfirmationTime),
            value,
            "geocodedDisplayName");
    }

    [Fact]
    public void ConfirmLocation_RejectsDisplayNameOverMaximumAtomically()
    {
        AssertInvalidTextIsAtomic(
            (listing, invalid) => listing.ConfirmLocation(
                41m,
                21m,
                LocationPrecision.City,
                "provider",
                "result",
                invalid,
                ConfirmationTime),
            new string('d', ListingLocationRules.GeocodedDisplayNameMaxLength + 1),
            "geocodedDisplayName",
            outOfRange: true);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ConfirmLocation_RejectsNonUtcTimestampAtomically(DateTimeKind kind)
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);
        DateTime invalidTime = DateTime.SpecifyKind(ConfirmationTime, kind);

        Action action = () => listing.ConfirmLocation(
            41m,
            21m,
            LocationPrecision.City,
            "new-provider",
            "new-result",
            null,
            invalidTime);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("confirmedAtUtc");
        Capture(listing).Should().Be(before);
    }

    [Fact]
    public void ClearLocation_ClearsConfirmedSnapshotAndIsIdempotent()
    {
        Listing listing = CreateConfirmedListing();

        listing.ClearLocation();
        listing.ClearLocation();

        AssertUnresolved(listing);
    }

    [Fact]
    public void ClearLocation_ClearsLegacyUnverifiedCoordinates()
    {
        var listing = new Listing();
        MaterializeLegacyCoordinates(listing, 41.9981m, 21.4254m);

        listing.ClearLocation();

        AssertUnresolved(listing);
    }

    [Fact]
    public void ConfirmLocation_ReplacesConfirmedAndLegacyState()
    {
        Listing confirmed = CreateConfirmedListing();
        var legacy = new Listing();
        MaterializeLegacyCoordinates(legacy, 40m, 20m);

        foreach (Listing listing in new[] { confirmed, legacy })
        {
            listing.ConfirmLocation(
                42m,
                22m,
                LocationPrecision.Municipality,
                "replacement-provider",
                "Case-Sensitive/Replacement:42",
                "Replacement display",
                ConfirmationTime.AddHours(1));

            AssertSnapshot(
                listing,
                42m,
                22m,
                LocationPrecision.Municipality,
                "replacement-provider",
                "Case-Sensitive/Replacement:42",
                "Replacement display",
                ConfirmationTime.AddHours(1));
        }
    }

    [Fact]
    public void LocationSnapshotProperties_HavePrivateSetters()
    {
        string[] propertyNames =
        [
            nameof(Listing.Latitude),
            nameof(Listing.Longitude),
            nameof(Listing.LocationPrecision),
            nameof(Listing.GeocodingProviderKey),
            nameof(Listing.GeocodingResultReference),
            nameof(Listing.GeocodedDisplayName),
            nameof(Listing.LocationConfirmedAtUtc)
        ];

        propertyNames.Should().OnlyContain(propertyName =>
            typeof(Listing).GetProperty(propertyName)!.SetMethod!.IsPrivate);
    }

    private static Listing CreateConfirmedListing()
    {
        var listing = new Listing();
        listing.ConfirmLocation(
            41.9981m,
            21.4254m,
            LocationPrecision.ExactAddress,
            "original-provider",
            "Original/Result:ABC",
            "Original display",
            ConfirmationTime);
        return listing;
    }

    private static void ConfirmWithMetadataValue(
        Listing listing,
        string propertyName,
        string value)
    {
        listing.ConfirmLocation(
            41m,
            21m,
            LocationPrecision.City,
            propertyName == nameof(Listing.GeocodingProviderKey)
                ? value
                : "provider",
            propertyName == nameof(Listing.GeocodingResultReference)
                ? value
                : "result",
            propertyName == nameof(Listing.GeocodedDisplayName)
                ? value
                : null,
            ConfirmationTime);
    }

    private static string? ReadMetadataValue(
        Listing listing,
        string propertyName)
    {
        return propertyName switch
        {
            nameof(Listing.GeocodingProviderKey) =>
                listing.GeocodingProviderKey,
            nameof(Listing.GeocodingResultReference) =>
                listing.GeocodingResultReference,
            nameof(Listing.GeocodedDisplayName) =>
                listing.GeocodedDisplayName,
            _ => throw new ArgumentOutOfRangeException(
                nameof(propertyName),
                propertyName,
                "Unknown location metadata property.")
        };
    }

    private static string ReadParameterName(string propertyName)
    {
        return propertyName switch
        {
            nameof(Listing.GeocodingProviderKey) =>
                "geocodingProviderKey",
            nameof(Listing.GeocodingResultReference) =>
                "geocodingResultReference",
            nameof(Listing.GeocodedDisplayName) =>
                "geocodedDisplayName",
            _ => throw new ArgumentOutOfRangeException(
                nameof(propertyName),
                propertyName,
                "Unknown location metadata property.")
        };
    }

    private static string CreateSupplementaryScalarValue(int scalarCount)
    {
        return string.Concat(Enumerable.Repeat("\U0001F642", scalarCount));
    }

    private static void AssertInvalidTextIsAtomic(
        Action<Listing, string> confirm,
        string invalidValue,
        string parameterName,
        bool outOfRange = false)
    {
        Listing listing = CreateConfirmedListing();
        LocationState before = Capture(listing);

        Action action = () => confirm(listing, invalidValue);

        if (outOfRange)
        {
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName(parameterName);
        }
        else
        {
            action.Should().Throw<ArgumentException>()
                .WithParameterName(parameterName);
        }

        Capture(listing).Should().Be(before);
    }

    private static void AssertUnresolved(Listing listing)
    {
        Capture(listing).Should().Be(new LocationState(
            null,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    private static void AssertSnapshot(
        Listing listing,
        decimal latitude,
        decimal longitude,
        LocationPrecision precision,
        string providerKey,
        string resultReference,
        string? displayName,
        DateTime confirmedAtUtc)
    {
        Capture(listing).Should().Be(new LocationState(
            latitude,
            longitude,
            precision,
            providerKey,
            resultReference,
            displayName,
            confirmedAtUtc));
    }

    private static LocationState Capture(Listing listing)
    {
        return new LocationState(
            listing.Latitude,
            listing.Longitude,
            listing.LocationPrecision,
            listing.GeocodingProviderKey,
            listing.GeocodingResultReference,
            listing.GeocodedDisplayName,
            listing.LocationConfirmedAtUtc);
    }

    private static void MaterializeLegacyCoordinates(
        Listing listing,
        decimal latitude,
        decimal longitude)
    {
        SetBackingField(listing, nameof(Listing.Latitude), latitude);
        SetBackingField(listing, nameof(Listing.Longitude), longitude);
    }

    private static void SetBackingField(
        Listing listing,
        string propertyName,
        decimal value)
    {
        typeof(Listing)
            .GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(listing, value);
    }

    private sealed record LocationState(
        decimal? Latitude,
        decimal? Longitude,
        LocationPrecision? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc);
}
