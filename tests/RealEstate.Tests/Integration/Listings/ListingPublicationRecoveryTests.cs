using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingPublicationRecoveryTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingPublicationRecoveryTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task MalformedActive_CanUnpublishRepairThroughPutAndRepublish()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            owner.UserId,
            UserStatus.Active);
        Guid imageId = await AddImageAsync(listingId);
        ListingAuthoringResponse created = await ReadListingAsync(listingId);
        Guid englishId = created.Translations
            .Single(translation => translation.LanguageCode == "en")
            .Id;
        Guid macedonianId = created.Translations
            .Single(translation => translation.LanguageCode == "mk")
            .Id;
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage initialPublish = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish?lang=en",
                null);
            initialPublish.StatusCode.Should().Be(HttpStatusCode.OK);

            await MakeActiveListingMalformedAsync(listingId);
            ListingAuthoringResponse malformed = await ReadListingAsync(listingId);
            malformed.Status.Should().Be(ListingStatus.Active);
            malformed.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Description.Should().BeNull();
            malformed.ModifiedAtUtc.Should().NotBeNull();

            HttpResponseMessage rejectedRepublish = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish?lang=en",
                null);
            await ApiFailureAssertions.AssertProblemAsync(
                rejectedRepublish,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictListingNotReady,
                $"/api/listings/{listingId}/publish");
            ListingAuthoringResponse afterRejectedPublish =
                await ReadListingAsync(listingId);
            afterRejectedPublish.Should().BeEquivalentTo(malformed);

            HttpResponseMessage unpublish = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/unpublish?lang=en",
                null);
            unpublish.StatusCode.Should().Be(HttpStatusCode.OK);
            ListingAuthoringResponse unpublished = await ReadListingAsync(listingId);
            unpublished.Status.Should().Be(ListingStatus.Draft);
            unpublished.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Description.Should().BeNull(
                    "unpublish must not require or repair publication content");
            unpublished.ModifiedAtUtc.Should().NotBeNull();
            unpublished.ModifiedAtUtc!.Value.Should().BeAfter(
                malformed.ModifiedAtUtc!.Value);

            UpdateListingRequest repairRequest = CreateRepairRequest();
            HttpResponseMessage repair = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                repairRequest);
            repair.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingAuthoringResponse repaired = await ReadListingAsync(listingId);
            repaired.Status.Should().Be(ListingStatus.Draft);
            repaired.Price.Should().Be(125_000m);
            repaired.Translations.Should().OnlyContain(translation =>
                !string.IsNullOrWhiteSpace(translation.LanguageCode) &&
                !string.IsNullOrWhiteSpace(translation.Title) &&
                !string.IsNullOrWhiteSpace(translation.City) &&
                !string.IsNullOrWhiteSpace(translation.Description));
            repaired.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Id.Should().Be(englishId);
            repaired.Translations.Single(translation =>
                    translation.LanguageCode == "mk")
                .Id.Should().Be(macedonianId);
            repaired.CreatedByUserId.Should().Be(created.CreatedByUserId);
            repaired.AgencyId.Should().Be(created.AgencyId);
            repaired.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
            repaired.Images.Should().ContainSingle();
            repaired.Images.Single().Id.Should().Be(imageId);
            repaired.ModifiedAtUtc.Should().NotBeNull();
            repaired.ModifiedAtUtc!.Value.Should().BeAfter(
                unpublished.ModifiedAtUtc!.Value);

            HttpResponseMessage finalPublish = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish?lang=en",
                null);
            finalPublish.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingAuthoringResponse final = await ReadListingAsync(listingId);
            final.Status.Should().Be(ListingStatus.Active);
            final.Price.Should().Be(125_000m);
            final.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Title.Should().Be("Repaired English title");
            final.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Description.Should().Be("Repaired English description");
            final.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Id.Should().Be(englishId);
            final.Images.Should().ContainSingle();
            final.Images.Single().Id.Should().Be(imageId);
            final.CreatedByUserId.Should().Be(created.CreatedByUserId);
            final.AgencyId.Should().Be(created.AgencyId);
            final.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
            final.ModifiedAtUtc.Should().NotBeNull();
            final.ModifiedAtUtc!.Value.Should().BeAfter(
                repaired.ModifiedAtUtc!.Value);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<ListingAuthoringResponse> ReadListingAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        IListingAuthoringRepository repository = scope.ServiceProvider
            .GetRequiredService<IListingAuthoringRepository>();
        Listing listing = await repository.GetByIdReadOnlyAsync(
                listingId,
                CancellationToken.None)
            ?? throw new InvalidOperationException("Listing was not found.");

        return listing.ToAuthoringResponse();
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
            OriginalFileName = "recovery-proof.jpg",
            StoredFileName = $"{imageId:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 4096,
            Url = $"/uploads/listings/{imageId:N}.jpg",
            SortOrder = 0,
            IsPrimary = true
        });
        await dbContext.SaveChangesAsync();

        return imageId;
    }

    private async Task MakeActiveListingMalformedAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        int updated = await dbContext.Set<ListingTranslation>()
            .Where(translation =>
                translation.ListingId == listingId &&
                translation.LanguageCode == "en")
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                translation => translation.Description,
                (string?)null));

        updated.Should().Be(1);
    }

    private static UpdateListingRequest CreateRepairRequest()
    {
        return new UpdateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 125_000m,
            Currency = "EUR",
            AreaSquareMeters = 78m,
            Rooms = 3m,
            Bathrooms = 2m,
            BalconyCount = 1,
            ParkingSpaces = 1,
            HasBasement = true,
            IsExchangePossible = false,
            HeatingType = HeatingType.Central,
            FurnishingStatus = FurnishingStatus.Furnished,
            Condition = PropertyCondition.Good,
            YearRenovated = 2024,
            Orientation = Orientation.South,
            YearBuilt = 2015,
            Latitude = 41.9981m,
            Longitude = 21.4254m,
            ApartmentDetails = new UpdateListingApartmentDetailsRequest
            {
                ApartmentType = ApartmentType.Standard,
                Floor = 5,
                TotalFloors = 9,
                HasElevator = true
            },
            HouseDetails = null,
            Translations =
            [
                new UpdateListingTranslationRequest
                {
                    LanguageCode = "en",
                    Title = "Repaired English title",
                    Description = "Repaired English description",
                    AddressLine = "Repaired address",
                    City = "Skopje",
                    Municipality = "Centar",
                    Neighborhood = "Center"
                },
                new UpdateListingTranslationRequest
                {
                    LanguageCode = "mk",
                    Title = "Поправен македонски наслов",
                    Description = "Поправен македонски опис",
                    AddressLine = "Поправена адреса",
                    City = "Скопје",
                    Municipality = "Центар",
                    Neighborhood = "Центар"
                }
            ]
        };
    }
}
