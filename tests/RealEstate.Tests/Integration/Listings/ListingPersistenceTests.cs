using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingPersistenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public void Commercial_and_land_taxonomy_has_exact_dormant_domain_shape()
    {
        Enum.GetValues<CommercialType>().Should().Equal(
            CommercialType.Unknown,
            CommercialType.Office,
            CommercialType.Shop,
            CommercialType.Other);
        Enum.GetValues<CommercialType>().Select(value => (int)value)
            .Should().Equal(0, 1, 2, 3);

        Enum.GetValues<LandType>().Should().Equal(
            LandType.Unknown,
            LandType.BuildingPlot,
            LandType.AgriculturalLand,
            LandType.Other);
        Enum.GetValues<LandType>().Select(value => (int)value)
            .Should().Equal(0, 1, 2, 3);

        new ListingCommercialDetails().CommercialType
            .Should().Be(CommercialType.Unknown);
        new ListingLandDetails().LandType.Should().Be(LandType.Unknown);

        var listing = new Listing();
        listing.CommercialDetails.Should().BeNull();
        listing.LandDetails.Should().BeNull();
    }

    [Fact]
    public async Task Can_round_trip_dormant_commercial_and_land_enum_names()
    {
        Guid listingId = Guid.NewGuid();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            var listing = CreateDormantTaxonomyFixture(listingId);
            listing.CommercialDetails = new ListingCommercialDetails
            {
                ListingId = listingId,
                CommercialType = CommercialType.Shop
            };
            listing.LandDetails = new ListingLandDetails
            {
                ListingId = listingId,
                LandType = LandType.AgriculturalLand
            };

            dbContext.Listings.Add(listing);
            await dbContext.SaveChangesAsync();
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            Listing persisted = await dbContext.Listings
                .Include(listing => listing.CommercialDetails)
                .Include(listing => listing.LandDetails)
                .SingleAsync(listing => listing.Id == listingId);

            persisted.PropertyType.Should().Be(PropertyType.Apartment);
            persisted.CommercialDetails!.CommercialType
                .Should().Be(CommercialType.Shop);
            persisted.LandDetails!.LandType
                .Should().Be(LandType.AgriculturalLand);

            string commercialStorage = await dbContext.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT "CommercialType" AS "Value"
                    FROM "ListingCommercialDetails"
                    WHERE "ListingId" = {0}
                    """,
                    listingId)
                .SingleAsync();
            string landStorage = await dbContext.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT "LandType" AS "Value"
                    FROM "ListingLandDetails"
                    WHERE "ListingId" = {0}
                    """,
                    listingId)
                .SingleAsync();

            commercialStorage.Should().Be("Shop");
            landStorage.Should().Be("AgriculturalLand");
        }
    }

    [Fact]
    public async Task Dormant_subtype_storage_enforces_defaults_ownership_and_cardinality()
    {
        Guid listingId = Guid.NewGuid();

        using IServiceScope scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        dbContext.Listings.Add(CreateDormantTaxonomyFixture(listingId));
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ListingCommercialDetails" ("ListingId")
             VALUES ({listingId})
             """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ListingLandDetails" ("ListingId")
             VALUES ({listingId})
             """);

        dbContext.ChangeTracker.Clear();
        (await dbContext.Set<ListingCommercialDetails>()
                .SingleAsync(details => details.ListingId == listingId))
            .CommercialType.Should().Be(CommercialType.Unknown);
        (await dbContext.Set<ListingLandDetails>()
                .SingleAsync(details => details.ListingId == listingId))
            .LandType.Should().Be(LandType.Unknown);

        Func<Task> duplicateCommercial = () =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO "ListingCommercialDetails" ("ListingId")
                 VALUES ({listingId})
                 """);
        PostgresException duplicateException =
            (await duplicateCommercial.Should().ThrowAsync<PostgresException>())
            .Which;
        duplicateException.SqlState.Should().Be(
            PostgresErrorCodes.UniqueViolation);

        Guid orphanListingId = Guid.NewGuid();
        Func<Task> insertOrphan = () =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO "ListingLandDetails" ("ListingId")
                 VALUES ({orphanListingId})
                 """);
        PostgresException orphanException =
            (await insertOrphan.Should().ThrowAsync<PostgresException>()).Which;
        orphanException.SqlState.Should().Be(
            PostgresErrorCodes.ForeignKeyViolation);

        dbContext.ChangeTracker.Clear();
        Listing listing = await dbContext.Listings
            .SingleAsync(candidate => candidate.Id == listingId);
        dbContext.Listings.Remove(listing);
        await dbContext.SaveChangesAsync();

        (await dbContext.Set<ListingCommercialDetails>()
                .CountAsync(details => details.ListingId == listingId))
            .Should().Be(0);
        (await dbContext.Set<ListingLandDetails>()
                .CountAsync(details => details.ListingId == listingId))
            .Should().Be(0);
    }

    [Fact]
    public async Task Can_save_and_load_listing_with_agency()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid listingId;
        Guid agencyId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var agency = CreateAgency();
            var listing = CreateListing(user.UserId);

            listing.AssignAgency(agency.Id);

            dbContext.Agencies.Add(agency);
            dbContext.Listings.Add(listing);

            await dbContext.SaveChangesAsync();
            listing.Publish();
            await dbContext.SaveChangesAsync();

            listingId = listing.Id;
            agencyId = agency.Id;
        }

        // Act
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var savedListing = await dbContext.Listings
                .Include(listing => listing.Agency)
                .SingleAsync(listing => listing.Id == listingId);

            // Assert
            savedListing.AgencyId.Should().Be(agencyId);
            savedListing.Agency.Should().NotBeNull();
            savedListing.Agency!.Id.Should().Be(agencyId);
            savedListing.Agency.Name.Should().StartWith("Dom Real Estate");
            savedListing.CreatedByUserId.Should().Be(user.UserId);
            savedListing.Latitude.Should().Be(41.9981m);
            savedListing.Longitude.Should().Be(21.4254m);
            savedListing.LocationPrecision.Should().Be(
                LocationPrecision.ExactAddress);
            savedListing.GeocodingProviderKey.Should().Be("persistence-test");
            savedListing.GeocodingResultReference.Should().Be(
                "opaque-result-reference");
            savedListing.GeocodedDisplayName.Should().Be(
                "Skopje test location");
            savedListing.LocationConfirmedAtUtc.Should().Be(
                new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        }
    }

    private static Agency CreateAgency()
    {
        return new Agency(
            name: $"Dom Real Estate {Guid.NewGuid():N}",
            slug: $"dom-real-estate-{Guid.NewGuid():N}",
            description: "Real estate agency in Skopje.",
            phoneNumber: "+38970123456",
            email: "agency@test.com",
            websiteUrl: "https://agency.test",
            addressLine: "Partizanska 1",
            city: "Skopje",
            municipality: "Centar");
    }

    private static Listing CreateDormantTaxonomyFixture(Guid listingId)
    {
        return new Listing
        {
            Id = listingId,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 100_000m,
            Currency = "EUR",
            AreaSquareMeters = 50m
        };
    }

    private static Listing CreateListing(Guid userId)
    {
        var listingId = Guid.NewGuid();

        var listing = new Listing
        {
            Id = listingId,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 120_000m,
            Currency = "EUR",
            AreaSquareMeters = 60m,
            Rooms = 3,
            Bathrooms = 1,
            YearBuilt = 2015,
            YearRenovated = 2020,
            BalconyCount = 1,
            ParkingSpaces = 1,
            HasBasement = true,
            IsExchangePossible = false,
            HeatingType = HeatingType.Central,
            FurnishingStatus = FurnishingStatus.Furnished,
            Condition = PropertyCondition.Good,
            Orientation = Orientation.SouthEast,
            ApartmentDetails = new ListingApartmentDetails
            {
                ListingId = listingId,
                ApartmentType = ApartmentType.Standard,
                Floor = 3,
                TotalFloors = 8,
                HasElevator = true
            },
            Translations = new List<ListingTranslation>
            {
                new ListingTranslation
                {
                    Id = Guid.NewGuid(),
                    ListingId = listingId,
                    LanguageCode = "mk",
                    Title = "Стан во Центар",
                    Description = "Тест опис",
                    AddressLine = "Партизанска 1",
                    City = "Скопје",
                    Municipality = "Центар",
                    Neighborhood = "Центар"
                }
            }
        };

        listing.ConfirmLocation(
            41.9981m,
            21.4254m,
            LocationPrecision.ExactAddress,
            "persistence-test",
            "opaque-result-reference",
            "Skopje test location",
            new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));

        listing.AssignCreator(userId);

        return listing;
    }
}
