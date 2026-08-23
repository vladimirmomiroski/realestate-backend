using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Listings;

internal static class StrongLocationListingTestFixtures
{
    public const decimal ConfirmedLatitude = 41.9981m;
    public const decimal ConfirmedLongitude = 21.4254m;
    public const string TestProviderKey = "test-fixture";
    public const string TestResultReference = "strong-location-fixture";
    public const string TestDisplayName = "Fixture address, Skopje";

    public static readonly DateTime ConfirmedAtUtc =
        new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

    public static Listing CreateUnresolvedValidDraft()
    {
        Listing listing = CreateBaseDraft();
        listing.Translations.Add(CreateTranslation(
            listing,
            "en",
            "Unresolved fixture listing",
            city: null,
            municipality: null,
            addressLine: null,
            description: null));

        return listing;
    }

    public static Listing CreatePublishableConfirmedDraft()
    {
        Listing listing = CreateBaseDraft();
        listing.Translations.Add(CreateTranslation(
            listing,
            "en",
            "Publishable fixture listing",
            "Skopje",
            "Centar",
            "Macedonia Street 1",
            "Complete fixture description."));
        listing.Translations.Add(CreateTranslation(
            listing,
            "mk",
            "Оглас подготвен за објавување",
            "Скопје",
            "Центар",
            "Улица Македонија 1",
            "Целосен опис за тест оглас."));
        AttachTrustedTestOnlyConfirmedLocation(listing);

        return listing;
    }

    public static Listing CreateValidActive()
    {
        Listing listing = CreatePublishableConfirmedDraft();

        if (!listing.Publish().IsReady)
        {
            throw new InvalidOperationException(
                "The strong-location Active fixture must be publishable.");
        }

        return listing;
    }

    public static Listing CreateCorruptActiveForUnitTest(
        Action<Listing> corrupt)
    {
        ArgumentNullException.ThrowIfNull(corrupt);

        Listing listing = CreateValidActive();
        corrupt(listing);

        return listing;
    }

    public static Listing CreateDirectDatabaseActivationRejectionSetup()
    {
        return CreateBaseDraft();
    }

    public static void AttachTrustedTestOnlyConfirmedLocation(Listing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);

        bool hasLatitude = listing.Latitude.HasValue;
        bool hasLongitude = listing.Longitude.HasValue;

        if (hasLatitude != hasLongitude)
        {
            throw new InvalidOperationException(
                "Trusted test coordinates must be either both present or both absent.");
        }

        decimal latitude = hasLatitude
            ? listing.Latitude!.Value
            : ConfirmedLatitude;
        decimal longitude = hasLongitude
            ? listing.Longitude!.Value
            : ConfirmedLongitude;

        listing.ConfirmLocation(
            latitude,
            longitude,
            LocationPrecision.ExactAddress,
            TestProviderKey,
            TestResultReference,
            TestDisplayName,
            ConfirmedAtUtc);
    }

    private static Listing CreateBaseDraft()
    {
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 100_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m
        };
        listing.ApartmentDetails = new ListingApartmentDetails
        {
            ListingId = listing.Id,
            ApartmentType = ApartmentType.Standard,
            Floor = 3,
            TotalFloors = 8,
            HasElevator = true,
            Listing = listing
        };

        return listing;
    }

    private static ListingTranslation CreateTranslation(
        Listing listing,
        string languageCode,
        string title,
        string? city,
        string? municipality,
        string? addressLine,
        string? description)
    {
        return new ListingTranslation
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            LanguageCode = languageCode,
            Title = title,
            City = city,
            Municipality = municipality,
            AddressLine = addressLine,
            Description = description,
            Neighborhood = null,
            Listing = listing
        };
    }
}
