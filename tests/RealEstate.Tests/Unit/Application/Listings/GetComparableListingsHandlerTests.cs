using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Listings;
using RealEstate.Domain.Listings;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class GetComparableListingsHandlerTests
{
    [Fact]
    public async Task Handle_ValidCandidates_ReturnsStrictResponsesInRepositoryOrder()
    {
        Listing first = CreateActiveListing(
            "en",
            "First title",
            "First city",
            "First description");
        Listing second = CreateActiveListing(
            "en",
            "Second title",
            "Second city",
            "Second description");
        var repository = new StubListingRepository(
            new ComparableListingsReadResult(
                true,
                [first, second],
                Array.Empty<ListingPublicationReadinessViolation>()));
        var handler = new GetComparableListingsHandler(
            repository,
            new GetComparableListingsValidator());

        ServiceResult<IReadOnlyList<PublicListingResponse>> result =
            await handler.HandleAsync(
                new GetComparableListingsQuery
                {
                    ListingId = Guid.NewGuid(),
                    LanguageCode = " EN ",
                    Limit = 6
                },
                CancellationToken.None);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value.Should().NotBeNull();
        result.Value!.Select(item => item.Id).Should().Equal(
            first.Id,
            second.Id);
        result.Value[0].LanguageCode.Should().Be("en");
        result.Value[0].Title.Should().Be("First title");
        result.Value[0].City.Should().Be("First city");
        result.Value[0].Description.Should().Be("First description");
        result.Value[1].LanguageCode.Should().Be("en");
        result.Value[1].Title.Should().Be("Second title");
        result.Value[1].City.Should().Be("Second city");
        result.Value[1].Description.Should().Be("Second description");
        repository.LanguageCode.Should().Be("en");
    }

    [Fact]
    public async Task Handle_CorruptMaterializedCandidate_ThrowsIntegrityFailure()
    {
        Listing candidate = CreateActiveListing(
            "en",
            "Candidate title",
            "Candidate city",
            "Candidate description");
        candidate.Translations.Single().Description = null;
        var repository = new StubListingRepository(
            new ComparableListingsReadResult(
                true,
                [candidate],
                Array.Empty<ListingPublicationReadinessViolation>()));
        var handler = new GetComparableListingsHandler(
            repository,
            new GetComparableListingsValidator());

        Func<Task> act = async () => await handler.HandleAsync(
            new GetComparableListingsQuery
            {
                ListingId = Guid.NewGuid(),
                LanguageCode = "en",
                Limit = 6
            },
            CancellationToken.None);

        PublicListingIntegrityException exception =
            (await act.Should()
                .ThrowExactlyAsync<PublicListingIntegrityException>())
            .Which;
        exception.ListingId.Should().Be(candidate.Id);
        exception.Violations.Select(violation => violation.Code)
            .Should().Equal(
                ListingPublicationReadinessViolationCode.InvalidDescription);
    }

    [Theory]
    [InlineData(ListingPublicationReadinessViolationCode.MissingTranslation)]
    [InlineData(ListingPublicationReadinessViolationCode.InvalidCity)]
    public async Task Handle_ActiveSourceMissingComparableIdentity_ThrowsIntegrityFailure(
        ListingPublicationReadinessViolationCode violationCode)
    {
        Guid sourceListingId = Guid.NewGuid();
        var violation = new ListingPublicationReadinessViolation(
            violationCode,
            TranslationId: null);
        var repository = new StubListingRepository(
            new ComparableListingsReadResult(
                true,
                Array.Empty<Listing>(),
                [violation]));
        var handler = new GetComparableListingsHandler(
            repository,
            new GetComparableListingsValidator());

        Func<Task> act = async () => await handler.HandleAsync(
            new GetComparableListingsQuery
            {
                ListingId = sourceListingId,
                LanguageCode = "en",
                Limit = 6
            },
            CancellationToken.None);

        PublicListingIntegrityException exception =
            (await act.Should()
                .ThrowExactlyAsync<PublicListingIntegrityException>())
            .Which;
        exception.ListingId.Should().Be(sourceListingId);
        exception.Violations.Should().Equal(violation);
    }

    private static Listing CreateActiveListing(
        string languageCode,
        string title,
        string city,
        string description)
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreatePublishableConfirmedDraft();
        ListingTranslation translation = listing.Translations.First();
        translation.LanguageCode = languageCode;
        translation.Title = title;
        translation.City = city;
        translation.Description = description;
        listing.Translations = [translation];
        StrongLocationListingTestFixtures
            .AttachTrustedTestOnlyConfirmedLocation(listing);
        listing.Publish().IsReady.Should().BeTrue();

        return listing;
    }

    private sealed class StubListingRepository(
        ComparableListingsReadResult readResult) : IListingRepository
    {
        public string? LanguageCode { get; private set; }

        public Task<ComparableListingsReadResult>
            GetComparableListingsReadOnlyAsync(
                Guid sourceListingId,
                string languageCode,
                int limit,
                CancellationToken cancellationToken)
        {
            LanguageCode = languageCode;
            return Task.FromResult(readResult);
        }

        public Task CreateAsync(
            Listing listing,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
            GetListingsQuery query,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Listing?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<Listing>>
            GetByAgencyIdForDashboardReadOnlyAsync(
                Guid agencyId,
                ListingStatus? status,
                int page,
                int pageSize,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ListingImageUploadProbeReadModel?>
            GetListingImageUploadProbeReadOnlyAsync(
                Guid listingId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IListingImageWriteScope?> BeginListingImageWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Listing?> GetByIdWithImagesForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<Listing>> GetByCreatedByUserIdAsync(
            Guid createdByUserId,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void AddListingImage(ListingImage image) =>
            throw new NotSupportedException();

        public void RemoveListingImage(ListingImage image) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
