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
    private static readonly DateTime ConfirmationTime =
        new(2026, 8, 13, 14, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, string?, string?, string>
        LocationTextChangeCases =>
        new()
        {
            {
                nameof(UpdateListingTranslationRequest.City),
                "Skopje",
                "Ohrid",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.Municipality),
                "Centar",
                "Karpos",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.AddressLine),
                "Original address",
                "Replacement address",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.Neighborhood),
                "Center",
                "Debar Maalo",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.Municipality),
                null,
                "Centar",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.Municipality),
                "Centar",
                null,
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.AddressLine),
                null,
                "New address",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.AddressLine),
                "Address",
                null,
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.Neighborhood),
                null,
                "Center",
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.Neighborhood),
                "Center",
                null,
                "en"
            },
            {
                nameof(UpdateListingTranslationRequest.City),
                "Скопје",
                "Охрид",
                "mk"
            }
        };

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
        AssertUnresolvedLocation(persisted);

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

    [Theory]
    [MemberData(nameof(LocationTextChangeCases))]
    public async Task ApplyAndPersist_LocationTextChangeClearsConfirmedSnapshot(
        string field,
        string? oldValue,
        string? newValue,
        string languageCode)
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                SetLocationText(request, languageCode, field, oldValue);
                return request;
            });
        await ConfirmLocationAsync(listingId);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                SetLocationText(request, languageCode, field, newValue);
                return request;
            });

        AssertUnresolvedLocation(await ReadListingAsync(listingId));
    }

    [Fact]
    public async Task ApplyAndPersist_LocationTextChangeClearsLegacyCoordinates()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await SetCoordinatesAsync(listingId, 41.9981m, 21.4254m);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations
                    .Single(translation => translation.LanguageCode == "en")
                    .City = "Ohrid";
                return request;
            });

        AssertUnresolvedLocation(await ReadListingAsync(listingId));
    }

    [Fact]
    public async Task ApplyAndPersist_AddedLanguageClearsConfirmedSnapshot()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ConfirmLocationAsync(listingId);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations.Add(CreateTranslation("sq", "Titull"));
                return request;
            });

        AssertUnresolvedLocation(await ReadListingAsync(listingId));
    }

    [Fact]
    public async Task ApplyAndPersist_RemovedLanguageClearsLegacyCoordinates()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await SetCoordinatesAsync(listingId, 41.9981m, 21.4254m);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations.RemoveAll(
                    translation => translation.LanguageCode == "mk");
                return request;
            });

        AssertUnresolvedLocation(await ReadListingAsync(listingId));
    }

    [Fact]
    public async Task ApplyAndPersist_ReorderedTranslationsPreserveConfirmedSnapshot()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ConfirmLocationAsync(listingId);
        LocationSnapshot original = CaptureLocation(
            await ReadListingAsync(listingId));

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations.Reverse();
                return request;
            });

        CaptureLocation(await ReadListingAsync(listingId)).Should().Be(original);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("root")]
    [InlineData("canonical-location")]
    public async Task ApplyAndPersist_NonLocationOrCanonicalNoOpPreservesConfirmedSnapshot(
        string change)
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ConfirmLocationAsync(listingId);
        LocationSnapshot original = CaptureLocation(
            await ReadListingAsync(listingId));

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                UpdateListingTranslationRequest english = request.Translations
                    .Single(translation => translation.LanguageCode == "en");

                switch (change)
                {
                    case "title":
                        english.Title = "Changed title";
                        break;
                    case "description":
                        english.Description = "Changed description";
                        break;
                    case "root":
                        request.Price += 10_000m;
                        break;
                    case "canonical-location":
                        AddCanonicalBoundaryWhitespace(request.Translations);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(change));
                }

                return request;
            });

        CaptureLocation(await ReadListingAsync(listingId)).Should().Be(original);
    }

    [Fact]
    public async Task ApplyAndPersist_CanonicalLocationNoOpPreservesLegacyCoordinates()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await SetCoordinatesAsync(listingId, 41.9981m, 21.4254m);
        LocationSnapshot original = CaptureLocation(
            await ReadListingAsync(listingId));

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                AddCanonicalBoundaryWhitespace(request.Translations);
                return request;
            });

        CaptureLocation(await ReadListingAsync(listingId)).Should().Be(original);
    }

    [Fact]
    public async Task ApplyAndPersist_LocationTextChangeKeepsUnresolvedListingUnresolved()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ApplyAndCommitAsync(
            listingId,
            listing =>
            {
                UpdateListingRequest request = CreateReplacementRequest(listing);
                request.Translations
                    .Single(translation => translation.LanguageCode == "en")
                    .City = "Ohrid";
                return request;
            });

        AssertUnresolvedLocation(await ReadListingAsync(listingId));
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
        await ConfirmLocationAsync(listingId);
        Listing original = await ReadListingAsync(listingId);
        LocationSnapshot originalLocation = CaptureLocation(original);

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
        CaptureLocation(persisted).Should().Be(originalLocation);
        await AssertSubtypeRowsAsync(
            listingId,
            expectedApartmentRows: 1,
            expectedHouseRows: 0);
    }

    [Fact]
    public async Task Apply_WhenListingIsNotDraft_RejectsBeforeMutation()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ConfirmLocationAsync(listingId);
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
            CaptureLocation(writeScope.Listing).Should().Be(
                CaptureLocation(original));
        }
    }

    [Fact]
    public async Task Apply_WithUnsupportedPropertyType_RejectsBeforeTrackedMutation()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ConfirmLocationAsync(listingId);
        Listing original = await ReadListingAsync(listingId);

        await using (AsyncServiceScope serviceScope =
            _factory.Services.CreateAsyncScope())
        {
            IListingAuthoringRepository repository = serviceScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>();
            ListingDraftReplacementEngine engine = serviceScope.ServiceProvider
                .GetRequiredService<ListingDraftReplacementEngine>();
            IListingAuthoringWriteScope? writeScope =
                await repository.BeginWriteAsync(
                    listingId,
                    CancellationToken.None);
            writeScope.Should().NotBeNull();

            await using (writeScope!)
            {
                Listing tracked = writeScope.Listing;
                ListingType originalListingType = tracked.ListingType;
                PropertyType originalPropertyType = tracked.PropertyType;
                decimal originalPrice = tracked.Price;
                string originalCurrency = tracked.Currency;
                decimal originalArea = tracked.AreaSquareMeters;
                decimal? originalRooms = tracked.Rooms;
                LocationSnapshot originalLocation = CaptureLocation(tracked);
                var originalTranslations = tracked.Translations
                    .OrderBy(translation => translation.LanguageCode)
                    .Select(translation => new
                    {
                        translation.Id,
                        translation.LanguageCode,
                        translation.Title,
                        translation.Description,
                        translation.AddressLine,
                        translation.City,
                        translation.Municipality,
                        translation.Neighborhood
                    })
                    .ToArray();
                ListingApartmentDetails originalApartment =
                    tracked.ApartmentDetails
                    ?? throw new InvalidOperationException(
                        "The test fixture requires apartment details.");
                var originalApartmentDetails = new
                {
                    originalApartment.ApartmentType,
                    originalApartment.Floor,
                    originalApartment.TotalFloors,
                    originalApartment.HasElevator
                };

                UpdateListingRequest request =
                    CreateReplacementRequest(tracked);
                request.ListingType = ListingType.Rent;
                request.Price = originalPrice + 123_456m;
                request.Currency = "USD";
                request.AreaSquareMeters = originalArea + 50m;
                request.Rooms = 99m;
                request.Translations =
                [
                    CreateTranslation(
                        "en",
                        "Rejected replacement title",
                        "Rejected replacement description",
                        "Rejected replacement address",
                        "Ohrid",
                        "Ohrid",
                        "Old Town"),
                    CreateTranslation(
                        "de",
                        "Rejected German title",
                        city: "Berlin")
                ];
                request.ApartmentDetails =
                    new UpdateListingApartmentDetailsRequest
                    {
                        ApartmentType = ApartmentType.Penthouse,
                        Floor = 12,
                        TotalFloors = 15,
                        HasElevator = false
                    };
                request.HouseDetails = new UpdateListingHouseDetailsRequest
                {
                    HouseType = HouseType.Detached,
                    NumberOfFloors = 2,
                    YardAreaSquareMeters = 300m
                };
                request.PropertyType = (PropertyType)999;

                Action apply = () => engine.Apply(writeScope, request);

                ArgumentOutOfRangeException exception = apply.Should()
                    .Throw<ArgumentOutOfRangeException>()
                    .WithMessage("Unsupported property type.*")
                    .Which;
                exception.ParamName.Should().Be("PropertyType");

                tracked.ListingType.Should().Be(originalListingType);
                tracked.PropertyType.Should().Be(originalPropertyType);
                tracked.Price.Should().Be(originalPrice);
                tracked.Currency.Should().Be(originalCurrency);
                tracked.AreaSquareMeters.Should().Be(originalArea);
                tracked.Rooms.Should().Be(originalRooms);
                tracked.Translations
                    .OrderBy(translation => translation.LanguageCode)
                    .Select(translation => new
                    {
                        translation.Id,
                        translation.LanguageCode,
                        translation.Title,
                        translation.Description,
                        translation.AddressLine,
                        translation.City,
                        translation.Municipality,
                        translation.Neighborhood
                    })
                    .Should().BeEquivalentTo(
                        originalTranslations,
                        options => options.WithStrictOrdering());
                CaptureLocation(tracked).Should().Be(originalLocation);
                tracked.ApartmentDetails.Should().NotBeNull();
                new
                {
                    tracked.ApartmentDetails!.ApartmentType,
                    tracked.ApartmentDetails.Floor,
                    tracked.ApartmentDetails.TotalFloors,
                    tracked.ApartmentDetails.HasElevator
                }.Should().BeEquivalentTo(originalApartmentDetails);
                tracked.HouseDetails.Should().BeNull();
            }
        }

        Listing persisted = await ReadListingAsync(listingId);
        persisted.PropertyType.Should().Be(original.PropertyType);
        persisted.Price.Should().Be(original.Price);
        CaptureLocation(persisted).Should().Be(CaptureLocation(original));
        persisted.ApartmentDetails.Should().NotBeNull();
        persisted.HouseDetails.Should().BeNull();
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
        await ListingTestHelpers.SetLegacyCoordinatesAsync(
            _factory,
            listingId,
            latitude,
            longitude);
    }

    private async Task ConfirmLocationAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings.SingleAsync(
            current => current.Id == listingId);

        listing.ConfirmLocation(
            41.9981m,
            21.4254m,
            LocationPrecision.ExactAddress,
            "replacement-test",
            "Opaque/Replacement:ABC-123",
            "Confirmed replacement test location",
            ConfirmationTime);

        await dbContext.SaveChangesAsync();
    }

    private static void SetLocationText(
        UpdateListingRequest request,
        string languageCode,
        string field,
        string? value)
    {
        UpdateListingTranslationRequest translation = request.Translations
            .Single(current => current.LanguageCode == languageCode);

        switch (field)
        {
            case nameof(UpdateListingTranslationRequest.City):
                translation.City = value;
                break;
            case nameof(UpdateListingTranslationRequest.Municipality):
                translation.Municipality = value;
                break;
            case nameof(UpdateListingTranslationRequest.AddressLine):
                translation.AddressLine = value;
                break;
            case nameof(UpdateListingTranslationRequest.Neighborhood):
                translation.Neighborhood = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(field),
                    field,
                    "Unknown location-text field.");
        }
    }

    private static void AddCanonicalBoundaryWhitespace(
        IEnumerable<UpdateListingTranslationRequest> translations)
    {
        foreach (UpdateListingTranslationRequest translation in translations)
        {
            translation.City = AddCanonicalBoundaryWhitespace(translation.City);
            translation.Municipality =
                AddCanonicalBoundaryWhitespace(translation.Municipality);
            translation.AddressLine =
                AddCanonicalBoundaryWhitespace(translation.AddressLine);
            translation.Neighborhood =
                AddCanonicalBoundaryWhitespace(translation.Neighborhood);
        }
    }

    private static string? AddCanonicalBoundaryWhitespace(string? value)
    {
        return value is null
            ? null
            : $"\u00A0{value}\u3000";
    }

    private static LocationSnapshot CaptureLocation(Listing listing)
    {
        return new LocationSnapshot(
            listing.Latitude,
            listing.Longitude,
            listing.LocationPrecision,
            listing.GeocodingProviderKey,
            listing.GeocodingResultReference,
            listing.GeocodedDisplayName,
            listing.LocationConfirmedAtUtc);
    }

    private static void AssertUnresolvedLocation(Listing listing)
    {
        CaptureLocation(listing).Should().Be(LocationSnapshot.Unresolved);
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

    private sealed record LocationSnapshot(
        decimal? Latitude,
        decimal? Longitude,
        LocationPrecision? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc)
    {
        public static LocationSnapshot Unresolved { get; } = new(
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
