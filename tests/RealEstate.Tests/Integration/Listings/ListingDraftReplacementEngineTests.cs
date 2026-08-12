using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingDraftReplacementEngineTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingDraftReplacementEngineTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task ApplyAndPersist_ReplacesRootTranslationsAndApartmentDetails()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        Guid imageId = await AddImageAsync(listingId);
        await SetCoordinatesAsync(listingId, 41.9981m, 21.4254m);
        Listing original = await ReadListingAsync(listingId);
        ListingTranslation originalEnglish = original.Translations
            .Single(translation => translation.LanguageCode == "en");
        Guid removedMacedonianId = original.Translations
            .Single(translation => translation.LanguageCode == "mk")
            .Id;

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.ListingType = ListingType.Rent;
                request.Price = 135_000m;
                request.Currency = " usd ";
                request.AreaSquareMeters = 75m;
                request.Rooms = null;
                request.Bathrooms = null;
                request.BalconyCount = null;
                request.ParkingSpaces = null;
                request.HasBasement = null;
                request.IsExchangePossible = null;
                request.HeatingType = HeatingType.Unknown;
                request.FurnishingStatus = FurnishingStatus.Unknown;
                request.Condition = PropertyCondition.Unknown;
                request.YearRenovated = null;
                request.Orientation = Orientation.Unknown;
                request.YearBuilt = null;
                request.ApartmentDetails = new UpdateListingApartmentDetailsRequest
                {
                    ApartmentType = ApartmentType.Penthouse,
                    Floor = 7,
                    TotalFloors = 9,
                    HasElevator = false
                };
                request.Translations =
                [
                    CreateTranslation(
                        " DE ",
                        " Neue Wohnung ",
                        " Neue Beschreibung ",
                        " Neue Adresse ",
                        " Berlin ",
                        " Mitte ",
                        " Zentrum "),
                    CreateTranslation(
                        " EN ",
                        " Updated title ",
                        " Updated description ",
                        " Updated address ",
                        " London ",
                        " Camden ",
                        " Central ")
                ];

                return request;
            });

        Listing persisted = await ReadListingAsync(listingId);

        persisted.Id.Should().Be(original.Id);
        persisted.CreatedByUserId.Should().Be(original.CreatedByUserId);
        persisted.AgencyId.Should().Be(original.AgencyId);
        persisted.Status.Should().Be(ListingStatus.Draft);
        persisted.CreatedAtUtc.Should().Be(original.CreatedAtUtc);
        persisted.ModifiedAtUtc.Should().NotBeNull();
        persisted.ModifiedAtUtc.Should().BeAfter(original.CreatedAtUtc);
        persisted.ListingType.Should().Be(ListingType.Rent);
        persisted.PropertyType.Should().Be(PropertyType.Apartment);
        persisted.Price.Should().Be(135_000m);
        persisted.Currency.Should().Be("USD");
        persisted.AreaSquareMeters.Should().Be(75m);
        persisted.Rooms.Should().BeNull();
        persisted.Bathrooms.Should().BeNull();
        persisted.BalconyCount.Should().BeNull();
        persisted.ParkingSpaces.Should().BeNull();
        persisted.HasBasement.Should().BeNull();
        persisted.IsExchangePossible.Should().BeNull();
        persisted.HeatingType.Should().Be(HeatingType.Unknown);
        persisted.FurnishingStatus.Should().Be(FurnishingStatus.Unknown);
        persisted.Condition.Should().Be(PropertyCondition.Unknown);
        persisted.YearRenovated.Should().BeNull();
        persisted.Orientation.Should().Be(Orientation.Unknown);
        persisted.YearBuilt.Should().BeNull();
        persisted.Latitude.Should().Be(original.Latitude);
        persisted.Longitude.Should().Be(original.Longitude);

        persisted.ApartmentDetails.Should().NotBeNull();
        persisted.ApartmentDetails!.ApartmentType.Should().Be(ApartmentType.Penthouse);
        persisted.ApartmentDetails.Floor.Should().Be(7);
        persisted.ApartmentDetails.TotalFloors.Should().Be(9);
        persisted.ApartmentDetails.HasElevator.Should().BeFalse();
        persisted.HouseDetails.Should().BeNull();

        persisted.Translations.Should().HaveCount(2);
        ListingTranslation english = persisted.Translations
            .Single(translation => translation.LanguageCode == "en");
        english.Id.Should().Be(originalEnglish.Id);
        english.Title.Should().Be("Updated title");
        english.Description.Should().Be("Updated description");
        english.AddressLine.Should().Be("Updated address");
        english.City.Should().Be("London");
        english.Municipality.Should().Be("Camden");
        english.Neighborhood.Should().Be("Central");

        ListingTranslation german = persisted.Translations
            .Single(translation => translation.LanguageCode == "de");
        german.Id.Should().NotBeEmpty();
        german.Id.Should().NotBe(originalEnglish.Id);
        german.Id.Should().NotBe(removedMacedonianId);
        persisted.Translations.Should().NotContain(
            translation => translation.Id == removedMacedonianId);

        persisted.Images.Should().ContainSingle();
        persisted.Images.Single().Id.Should().Be(imageId);
        persisted.Images.Single().SortOrder.Should().Be(0);
        persisted.Images.Single().IsPrimary.Should().BeTrue();

        ListingAuthoringResponse response = persisted.ToAuthoringResponse();
        response.Translations.Select(translation => translation.LanguageCode)
            .Should().Equal("de", "en");
        response.Images.Should().ContainSingle(image => image.Id == imageId);
        response.ApartmentDetails.Should().NotBeNull();
        response.HouseDetails.Should().BeNull();
        response.Price.Should().Be(135_000m);
    }

    [Fact]
    public async Task ApplyAndPersist_ReorderedTranslationsPreserveEveryExistingId()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations =
                [
                    CreateTranslation("sq", "Titull"),
                    CreateTranslation("en", "English"),
                    CreateTranslation("mk", "Македонски")
                ];
                return request;
            });

        Listing afterFirstReplacement = await ReadListingAsync(listingId);
        Dictionary<string, Guid> idsByLanguage = afterFirstReplacement.Translations
            .ToDictionary(
                translation => translation.LanguageCode,
                translation => translation.Id,
                StringComparer.Ordinal);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations =
                [
                    CreateTranslation("mk", "Македонски"),
                    CreateTranslation("sq", "Titull"),
                    CreateTranslation("en", "English")
                ];
                return request;
            });

        Listing persisted = await ReadListingAsync(listingId);
        persisted.Translations.ToDictionary(
                translation => translation.LanguageCode,
                translation => translation.Id,
                StringComparer.Ordinal)
            .Should().BeEquivalentTo(idsByLanguage);
    }

    [Fact]
    public async Task ApplyAndPersist_ConvertsApartmentToHouseUpdatesHouseAndConvertsBack()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.PropertyType = PropertyType.House;
                request.ApartmentDetails = null;
                request.HouseDetails = new UpdateListingHouseDetailsRequest
                {
                    HouseType = HouseType.Detached,
                    NumberOfFloors = 2,
                    YardAreaSquareMeters = 300m
                };
                return request;
            });

        await AssertSubtypeRowsAsync(
            listingId,
            expectedApartmentRows: 0,
            expectedHouseRows: 1);
        Listing house = await ReadListingAsync(listingId);
        house.PropertyType.Should().Be(PropertyType.House);
        house.ApartmentDetails.Should().BeNull();
        house.HouseDetails.Should().NotBeNull();
        house.HouseDetails!.HouseType.Should().Be(HouseType.Detached);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.HouseDetails = new UpdateListingHouseDetailsRequest
                {
                    HouseType = HouseType.Villa,
                    NumberOfFloors = 3,
                    YardAreaSquareMeters = 425m
                };
                return request;
            });

        await AssertSubtypeRowsAsync(
            listingId,
            expectedApartmentRows: 0,
            expectedHouseRows: 1);
        Listing updatedHouse = await ReadListingAsync(listingId);
        updatedHouse.HouseDetails!.HouseType.Should().Be(HouseType.Villa);
        updatedHouse.HouseDetails.NumberOfFloors.Should().Be(3);
        updatedHouse.HouseDetails.YardAreaSquareMeters.Should().Be(425m);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.PropertyType = PropertyType.Apartment;
                request.HouseDetails = null;
                request.ApartmentDetails = new UpdateListingApartmentDetailsRequest
                {
                    ApartmentType = ApartmentType.Duplex,
                    Floor = 5,
                    TotalFloors = 6,
                    HasElevator = true
                };
                return request;
            });

        await AssertSubtypeRowsAsync(
            listingId,
            expectedApartmentRows: 1,
            expectedHouseRows: 0);
        Listing apartment = await ReadListingAsync(listingId);
        apartment.PropertyType.Should().Be(PropertyType.Apartment);
        apartment.HouseDetails.Should().BeNull();
        apartment.ApartmentDetails.Should().NotBeNull();
        apartment.ApartmentDetails!.ApartmentType.Should().Be(ApartmentType.Duplex);
    }

    [Fact]
    public async Task ApplyAndPersist_TranslationOnlyChangeAdvancesRootAudit()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        Listing original = await ReadListingAsync(listingId);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations
                    .Single(translation => translation.LanguageCode == "en")
                    .Title = "Translation-only change";
                return request;
            });

        Listing persisted = await ReadListingAsync(listingId);
        persisted.Price.Should().Be(original.Price);
        persisted.PropertyType.Should().Be(original.PropertyType);
        persisted.CreatedAtUtc.Should().Be(original.CreatedAtUtc);
        persisted.ModifiedAtUtc.Should().NotBeNull();
        persisted.ModifiedAtUtc.Should().BeAfter(original.CreatedAtUtc);
        persisted.Translations.Single(translation =>
                translation.LanguageCode == "en")
            .Title.Should().Be("Translation-only change");
    }

    [Fact]
    public async Task ApplyAndPersist_SubtypeOnlyChangeAdvancesRootAudit()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        Listing original = await ReadListingAsync(listingId);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.ApartmentDetails!.Floor = 6;
                return request;
            });

        Listing persisted = await ReadListingAsync(listingId);
        persisted.Price.Should().Be(original.Price);
        persisted.CreatedAtUtc.Should().Be(original.CreatedAtUtc);
        persisted.ModifiedAtUtc.Should().NotBeNull();
        persisted.ModifiedAtUtc.Should().BeAfter(original.CreatedAtUtc);
        persisted.ApartmentDetails!.Floor.Should().Be(6);
    }

    [Fact]
    public async Task SaveFailure_DisposalRollsBackEntireReplacement()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        Guid imageId = await AddImageAsync(listingId);
        Listing original = await ReadListingAsync(listingId);

        await using (AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope())
        {
            IListingAuthoringRepository repository = serviceScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>();
            ListingDraftReplacementEngine engine = serviceScope.ServiceProvider
                .GetRequiredService<ListingDraftReplacementEngine>();
            var validator = new UpdateListingValidator();
            IListingAuthoringWriteScope? writeScope =
                await repository.BeginWriteAsync(
                    listingId,
                    CancellationToken.None);
            writeScope.Should().NotBeNull();

            await using (writeScope!)
            {
                UpdateListingRequest request =
                    CreateReplacementRequest(writeScope.Listing);
                request.Price = 777_777m;
                request.PropertyType = PropertyType.House;
                request.ApartmentDetails = null;
                request.HouseDetails = new UpdateListingHouseDetailsRequest
                {
                    HouseType = HouseType.Villa,
                    NumberOfFloors = 4,
                    YardAreaSquareMeters = 500m
                };
                request.Translations =
                [
                    CreateTranslation("en", "Replacement English"),
                    CreateTranslation("de", "Replacement German")
                ];

                validator.ValidateWithKey(request).Should().BeNull();
                validator.Normalize(request);
                engine.Apply(writeScope, request);

                writeScope.Listing.Translations
                    .Single(translation => translation.LanguageCode == "de")
                    .Title = string.Empty;

                Func<Task> save = () => writeScope.SaveChangesAsync(
                    CancellationToken.None);

                await save.Should().ThrowAsync<DbUpdateException>();
            }
        }

        Listing persisted = await ReadListingAsync(listingId);
        persisted.Price.Should().Be(original.Price);
        persisted.PropertyType.Should().Be(original.PropertyType);
        persisted.Status.Should().Be(original.Status);
        persisted.CreatedAtUtc.Should().Be(original.CreatedAtUtc);
        persisted.ModifiedAtUtc.Should().Be(original.ModifiedAtUtc);
        persisted.Translations.Select(translation => new
            {
                translation.Id,
                translation.LanguageCode,
                translation.Title
            })
            .Should().BeEquivalentTo(original.Translations.Select(translation => new
            {
                translation.Id,
                translation.LanguageCode,
                translation.Title
            }));
        persisted.ApartmentDetails.Should().NotBeNull();
        persisted.HouseDetails.Should().BeNull();
        persisted.Images.Should().ContainSingle(image => image.Id == imageId);
        await AssertSubtypeRowsAsync(
            listingId,
            expectedApartmentRows: 1,
            expectedHouseRows: 0);
    }

    [Fact]
    public async Task Apply_WhenListingIsNotDraft_RejectsBeforeMutation()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);
        Listing original = await ReadListingAsync(listingId);

        await using AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope();
        IListingAuthoringRepository repository = serviceScope.ServiceProvider
            .GetRequiredService<IListingAuthoringRepository>();
        ListingDraftReplacementEngine engine = serviceScope.ServiceProvider
            .GetRequiredService<ListingDraftReplacementEngine>();
        IListingAuthoringWriteScope? writeScope = await repository.BeginWriteAsync(
            listingId,
            CancellationToken.None);
        writeScope.Should().NotBeNull();

        await using (writeScope!)
        {
            UpdateListingRequest request = CreateReplacementRequest(writeScope.Listing);
            request.Price = original.Price + 10_000m;

            Action apply = () => engine.Apply(writeScope, request);

            apply.Should().Throw<InvalidOperationException>()
                .WithMessage("Only draft listings can be replaced.");
            writeScope.Listing.Price.Should().Be(original.Price);
        }
    }

    private async Task ApplyAndCommitAsync(
        Guid listingId,
        Func<Listing, UpdateListingRequest> createRequest)
    {
        await using AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope();
        IListingAuthoringRepository repository = serviceScope.ServiceProvider
            .GetRequiredService<IListingAuthoringRepository>();
        ListingDraftReplacementEngine engine = serviceScope.ServiceProvider
            .GetRequiredService<ListingDraftReplacementEngine>();
        var validator = new UpdateListingValidator();
        IListingAuthoringWriteScope? writeScope = await repository.BeginWriteAsync(
            listingId,
            CancellationToken.None);
        writeScope.Should().NotBeNull();

        await using (writeScope!)
        {
            UpdateListingRequest request = createRequest(writeScope.Listing);

            validator.ValidateWithKey(request).Should().BeNull();
            validator.Normalize(request);
            engine.Apply(writeScope, request);

            await writeScope.SaveChangesAsync(CancellationToken.None);
            await writeScope.CommitAsync(CancellationToken.None);
        }
    }

    private async Task<Listing> ReadListingAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        IListingAuthoringRepository repository = scope.ServiceProvider
            .GetRequiredService<IListingAuthoringRepository>();

        return await repository.GetByIdReadOnlyAsync(
                listingId,
                CancellationToken.None)
            ?? throw new InvalidOperationException("Listing was not found.");
    }

    private async Task<Guid> AddImageAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid imageId = Guid.NewGuid();

        dbContext.Set<ListingImage>().Add(new ListingImage
        {
            Id = imageId,
            ListingId = listingId,
            OriginalFileName = "replacement-proof.jpg",
            StoredFileName = $"{imageId:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            Url = $"/uploads/listings/{imageId:N}.jpg",
            SortOrder = 0,
            IsPrimary = true
        });
        await dbContext.SaveChangesAsync();

        return imageId;
    }

    private async Task AssertSubtypeRowsAsync(
        Guid listingId,
        int expectedApartmentRows,
        int expectedHouseRows)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        int apartmentRows = await dbContext.Set<ListingApartmentDetails>()
            .AsNoTracking()
            .CountAsync(details => details.ListingId == listingId);
        int houseRows = await dbContext.Set<ListingHouseDetails>()
            .AsNoTracking()
            .CountAsync(details => details.ListingId == listingId);

        apartmentRows.Should().Be(expectedApartmentRows);
        houseRows.Should().Be(expectedHouseRows);
    }

    private async Task SetCoordinatesAsync(
        Guid listingId,
        decimal latitude,
        decimal longitude)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings.SingleAsync(
            current => current.Id == listingId);

        listing.Latitude = latitude;
        listing.Longitude = longitude;

        await dbContext.SaveChangesAsync();
    }

    private static UpdateListingRequest CreateReplacementRequest(Listing listing)
    {
        return new UpdateListingRequest
        {
            ListingType = listing.ListingType,
            PropertyType = listing.PropertyType,
            Price = listing.Price,
            Currency = listing.Currency,
            AreaSquareMeters = listing.AreaSquareMeters,
            Rooms = listing.Rooms,
            Bathrooms = listing.Bathrooms,
            BalconyCount = listing.BalconyCount,
            ParkingSpaces = listing.ParkingSpaces,
            HasBasement = listing.HasBasement,
            IsExchangePossible = listing.IsExchangePossible,
            HeatingType = listing.HeatingType,
            FurnishingStatus = listing.FurnishingStatus,
            Condition = listing.Condition,
            YearRenovated = listing.YearRenovated,
            Orientation = listing.Orientation,
            YearBuilt = listing.YearBuilt,
            ApartmentDetails = listing.ApartmentDetails is null
                ? null
                : new UpdateListingApartmentDetailsRequest
                {
                    ApartmentType = listing.ApartmentDetails.ApartmentType,
                    Floor = listing.ApartmentDetails.Floor,
                    TotalFloors = listing.ApartmentDetails.TotalFloors,
                    HasElevator = listing.ApartmentDetails.HasElevator
                },
            HouseDetails = listing.HouseDetails is null
                ? null
                : new UpdateListingHouseDetailsRequest
                {
                    HouseType = listing.HouseDetails.HouseType,
                    NumberOfFloors = listing.HouseDetails.NumberOfFloors,
                    YardAreaSquareMeters = listing.HouseDetails.YardAreaSquareMeters
                },
            Translations = listing.Translations
                .Select(translation => CreateTranslation(
                    translation.LanguageCode,
                    translation.Title,
                    translation.Description,
                    translation.AddressLine,
                    translation.City,
                    translation.Municipality,
                    translation.Neighborhood))
                .ToList()
        };
    }

    private static UpdateListingTranslationRequest CreateTranslation(
        string languageCode,
        string title,
        string? description = "Description",
        string? addressLine = "Address",
        string? city = "Skopje",
        string? municipality = "Centar",
        string? neighborhood = "Center")
    {
        return new UpdateListingTranslationRequest
        {
            LanguageCode = languageCode,
            Title = title,
            Description = description,
            AddressLine = addressLine,
            City = city,
            Municipality = municipality,
            Neighborhood = neighborhood
        };
    }
}
