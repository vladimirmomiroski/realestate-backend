using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    private static Listing CreateListing(Guid userId)
    {
        var listingId = Guid.NewGuid();

        var listing = new Listing
        {
            Id = listingId,
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Status = ListingStatus.Active,
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
            Latitude = 41.9981m,
            Longitude = 21.4254m,
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

        listing.AssignCreator(userId);

        return listing;
    }
}