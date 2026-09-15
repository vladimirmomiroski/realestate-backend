using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.PublishListing;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Mappings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingUpdateConcurrencyTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingUpdateConcurrencyTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task SubtypeReplacementConcurrency_OpposingApartmentCommercialUpdatesSerializeAndSecondUsesPostCommitAggregate()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        ListingAuthoringResponse initial =
            await ReadAuthoringResponseAsync(listingId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope firstDependencyScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondDependencyScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext firstAuthoringDbContext =
            firstDependencyScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(firstAuthoringDbContext);
        int firstBackendPid = await GetBackendProcessIdAsync(
            firstAuthoringDbContext);
        var firstGate = new GatedAuthoringRepository(
            firstDependencyScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var secondProbe = new AuthoringProgressProbe();
        await using RealEstateDbContext secondAuthoringDbContext =
            CreateProbedAuthoringDbContext(connectionString, secondProbe);
        await EnsureConnectionOpenAsync(secondAuthoringDbContext);
        int secondBackendPid = await GetBackendProcessIdAsync(
            secondAuthoringDbContext);
        var secondRepository = new ListingAuthoringRepository(
            secondAuthoringDbContext);

        UpdateListingHandler firstHandler = CreateUpdateHandler(
            firstGate,
            firstDependencyScope.ServiceProvider,
            owner.UserId);
        UpdateListingHandler secondHandler = CreateUpdateHandler(
            secondRepository,
            secondDependencyScope.ServiceProvider,
            owner.UserId);

        UpdateListingRequest firstRequest = CreateCommercialRequest(
            price: 111_000m,
            CommercialType.Office,
            ("en", "Writer one English"),
            ("de", "Writer one German"));
        UpdateListingRequest secondRequest = CreateApartmentRequest(
            price: 222_000m,
            ("de", "Writer two German"),
            ("sq", "Writer two Albanian"));
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingAuthoringResponse>>? firstTask = null;
        Task<ServiceResult<ListingAuthoringResponse>>? secondTask = null;

        try
        {
            firstTask = firstHandler.HandleAsync(
                listingId,
                firstRequest,
                cancellation.Token);

            await firstGate.LockAcquired.WaitAsync(TestTimeout);
            firstTask.IsCompleted.Should().BeFalse(
                "writer one is held by the deterministic gate while owning the parent lock");

            secondTask = secondHandler.HandleAsync(
                listingId,
                secondRequest,
                cancellation.Token);

            await secondProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                waitingBackendPid: secondBackendPid,
                blockingBackendPid: firstBackendPid,
                competingTask: secondTask,
                cancellation.Token);
            secondProbe.AggregateLoadStarted.IsCompleted.Should().BeFalse(
                "writer two cannot load a tracked aggregate before acquiring the parent lock");
            secondTask.IsCompleted.Should().BeFalse();

            firstGate.Release();

            ServiceResult<ListingAuthoringResponse> firstResult =
                await firstTask.WaitAsync(TestTimeout);
            await secondProbe.AggregateLoadStarted.WaitAsync(TestTimeout);
            ServiceResult<ListingAuthoringResponse> secondResult =
                await secondTask.WaitAsync(TestTimeout);

            firstResult.Status.Should().Be(ServiceResultStatus.Success);
            secondResult.Status.Should().Be(ServiceResultStatus.Success);
            firstResult.Value!.PropertyType.Should().Be(PropertyType.Commercial);
            firstResult.Value.ApartmentDetails.Should().BeNull();
            firstResult.Value.HouseDetails.Should().BeNull();
            firstResult.Value.CommercialDetails.Should().NotBeNull();
            firstResult.Value.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Office);
            firstResult.Value.LandDetails.Should().BeNull();
            secondResult.Value!.PropertyType.Should().Be(PropertyType.Apartment);
            secondResult.Value.ApartmentDetails.Should().NotBeNull();
            secondResult.Value.HouseDetails.Should().BeNull();
            secondResult.Value.CommercialDetails.Should().BeNull();
            secondResult.Value.LandDetails.Should().BeNull();

            Guid writerOneGermanId = firstResult.Value.Translations
                .Single(translation => translation.LanguageCode == "de")
                .Id;
            ListingAuthoringResponse writerTwoResponse = secondResult.Value!;
            writerTwoResponse.Translations
                .Single(translation => translation.LanguageCode == "de")
                .Id.Should().Be(
                    writerOneGermanId,
                    "writer two must reconcile against writer one's committed de row");

            ListingAuthoringResponse persisted =
                await ReadAuthoringResponseAsync(listingId);

            persisted.Price.Should().Be(222_000m);
            persisted.Translations.Select(translation => translation.LanguageCode)
                .Should().Equal("de", "sq");
            persisted.Translations.Single(translation =>
                    translation.LanguageCode == "de")
                .Id.Should().Be(writerOneGermanId);
            persisted.Translations.Single(translation =>
                    translation.LanguageCode == "de")
                .Title.Should().Be("Writer two German");
            persisted.Translations.Select(translation => translation.Id)
                .Should().OnlyHaveUniqueItems();
            persisted.Translations.Select(translation => translation.LanguageCode)
                .Should().OnlyHaveUniqueItems();
            persisted.PropertyType.Should().Be(PropertyType.Apartment);
            persisted.ApartmentDetails.Should().NotBeNull();
            persisted.HouseDetails.Should().BeNull();
            persisted.CommercialDetails.Should().BeNull();
            persisted.LandDetails.Should().BeNull();
            persisted.ModifiedAtUtc.Should().NotBeNull();
            writerTwoResponse.ModifiedAtUtc.Should().NotBeNull();
            (persisted.ModifiedAtUtc!.Value -
                writerTwoResponse.ModifiedAtUtc!.Value)
                .Duration()
                .Should().BeLessThan(TimeSpan.FromMilliseconds(1));
            persisted.ModifiedAtUtc.Should().BeAfter(initial.CreatedAtUtc);

            await AssertSubtypeRowsAsync(
                listingId,
                expectedApartmentRows: 1,
                expectedHouseRows: 0,
                expectedCommercialRows: 0,
                expectedLandRows: 0);
        }
        finally
        {
            firstGate.Release();
            await DrainStartedTasksAsync(
                cancellation,
                firstTask,
                secondTask);
        }
    }

    [Fact]
    public async Task SubtypeReplacementConcurrency_SameCommercialRootSerializesAndLastSubtypeWins()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope firstDependencyScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondDependencyScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext firstAuthoringDbContext =
            firstDependencyScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(firstAuthoringDbContext);
        int firstBackendPid = await GetBackendProcessIdAsync(
            firstAuthoringDbContext);
        var firstGate = new GatedAuthoringRepository(
            firstDependencyScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var secondProbe = new AuthoringProgressProbe();
        await using RealEstateDbContext secondAuthoringDbContext =
            CreateProbedAuthoringDbContext(connectionString, secondProbe);
        await EnsureConnectionOpenAsync(secondAuthoringDbContext);
        int secondBackendPid = await GetBackendProcessIdAsync(
            secondAuthoringDbContext);
        var secondRepository = new ListingAuthoringRepository(
            secondAuthoringDbContext);

        UpdateListingHandler firstHandler = CreateUpdateHandler(
            firstGate,
            firstDependencyScope.ServiceProvider,
            owner.UserId);
        UpdateListingHandler secondHandler = CreateUpdateHandler(
            secondRepository,
            secondDependencyScope.ServiceProvider,
            owner.UserId);
        UpdateListingRequest firstRequest = CreateCommercialRequest(
            181_000m,
            CommercialType.Office,
            ("en", "Office writer"),
            ("de", "Office writer German"));
        UpdateListingRequest secondRequest = CreateCommercialRequest(
            282_000m,
            CommercialType.Shop,
            ("en", "Shop writer"),
            ("sq", "Shop writer Albanian"));
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingAuthoringResponse>>? firstTask = null;
        Task<ServiceResult<ListingAuthoringResponse>>? secondTask = null;

        try
        {
            firstTask = firstHandler.HandleAsync(
                listingId,
                firstRequest,
                cancellation.Token);
            await firstGate.LockAcquired.WaitAsync(TestTimeout);
            firstTask.IsCompleted.Should().BeFalse(
                "the Office writer is held while owning the parent lock");

            secondTask = secondHandler.HandleAsync(
                listingId,
                secondRequest,
                cancellation.Token);
            await secondProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                waitingBackendPid: secondBackendPid,
                blockingBackendPid: firstBackendPid,
                competingTask: secondTask,
                cancellation.Token);
            secondProbe.AggregateLoadStarted.IsCompleted.Should().BeFalse(
                "the Shop writer cannot load stale child state before the parent lock");
            secondTask.IsCompleted.Should().BeFalse();

            firstGate.Release();

            ServiceResult<ListingAuthoringResponse> firstResult =
                await firstTask.WaitAsync(TestTimeout);
            await secondProbe.AggregateLoadStarted.WaitAsync(TestTimeout);
            ServiceResult<ListingAuthoringResponse> secondResult =
                await secondTask.WaitAsync(TestTimeout);

            firstResult.Status.Should().Be(ServiceResultStatus.Success);
            firstResult.Value!.PropertyType.Should().Be(PropertyType.Commercial);
            firstResult.Value.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Office);
            secondResult.Status.Should().Be(ServiceResultStatus.Success);
            secondResult.Value!.PropertyType.Should().Be(PropertyType.Commercial);
            secondResult.Value.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Shop);

            ListingAuthoringResponse persisted =
                await ReadAuthoringResponseAsync(listingId);
            persisted.PropertyType.Should().Be(PropertyType.Commercial);
            persisted.Price.Should().Be(282_000m);
            persisted.ApartmentDetails.Should().BeNull();
            persisted.HouseDetails.Should().BeNull();
            persisted.CommercialDetails.Should().NotBeNull();
            persisted.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Shop);
            persisted.LandDetails.Should().BeNull();
            persisted.Translations.Select(translation => translation.LanguageCode)
                .Should().Equal("en", "sq");
            persisted.Translations.Single(translation =>
                    translation.LanguageCode == "en")
                .Title.Should().Be("Shop writer");

            await AssertSubtypeRowsAsync(
                listingId,
                expectedApartmentRows: 0,
                expectedHouseRows: 0,
                expectedCommercialRows: 1,
                expectedLandRows: 0,
                expectedCommercialType: CommercialType.Shop);
        }
        finally
        {
            firstGate.Release();
            await DrainStartedTasksAsync(
                cancellation,
                firstTask,
                secondTask);
        }
    }

    [Fact]
    public async Task SubtypeReplacementConcurrency_CommercialUpdateFirst_NonOwnerPublishWaitsThenAuthorizationPrecedesReadiness()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        AuthenticatedTestUser nonowner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(nonowner.UserId, UserStatus.Active);
        ListingAuthoringResponse initial =
            await ReadAuthoringResponseAsync(listingId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope updateDependencyScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope publishDependencyScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext updateAuthoringDbContext =
            updateDependencyScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(updateAuthoringDbContext);
        int updateBackendPid = await GetBackendProcessIdAsync(
            updateAuthoringDbContext);
        var updateGate = new GatedAuthoringRepository(
            updateDependencyScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var publishProbe = new AuthoringProgressProbe();
        await using RealEstateDbContext publishAuthoringDbContext =
            CreateProbedAuthoringDbContext(connectionString, publishProbe);
        await EnsureConnectionOpenAsync(publishAuthoringDbContext);
        int publishBackendPid = await GetBackendProcessIdAsync(
            publishAuthoringDbContext);
        var publishRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(publishAuthoringDbContext));

        UpdateListingHandler updateHandler = CreateUpdateHandler(
            updateGate,
            updateDependencyScope.ServiceProvider,
            owner.UserId);
        PublishListingHandler publishHandler = CreatePublishHandler(
            publishRepository,
            publishDependencyScope.ServiceProvider,
            nonowner.UserId);
        UpdateListingRequest incompleteRequest = CreateCommercialRequest(
            price: 345_000m,
            CommercialType.Shop,
            ("en", "Committed incomplete English"),
            ("de", "Committed incomplete German"));
        incompleteRequest.Translations
            .Single(translation => translation.LanguageCode == "de")
            .City = null;
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingAuthoringResponse>>? updateTask = null;
        Task<ServiceResult<PublicListingResponse>>? publishTask = null;

        try
        {
            updateTask = updateHandler.HandleAsync(
                listingId,
                incompleteRequest,
                cancellation.Token);

            await updateGate.LockAcquired.WaitAsync(TestTimeout);

            publishTask = publishHandler.HandleAsync(
                new PublishListingCommand(listingId, "de"),
                cancellation.Token);

            await publishProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                waitingBackendPid: publishBackendPid,
                blockingBackendPid: updateBackendPid,
                competingTask: publishTask,
                cancellation.Token);
            publishProbe.AggregateLoadStarted.IsCompleted.Should().BeFalse(
                "publish cannot load the aggregate before acquiring the parent lock");
            publishTask.IsCompleted.Should().BeFalse();

            updateGate.Release();

            ServiceResult<ListingAuthoringResponse> updateResult =
                await updateTask.WaitAsync(TestTimeout);
            await publishProbe.AggregateLoadStarted.WaitAsync(TestTimeout);
            ServiceResult<PublicListingResponse> publishResult =
                await publishTask.WaitAsync(TestTimeout);

            updateResult.Status.Should().Be(ServiceResultStatus.Success);
            publishResult.Status.Should().Be(ServiceResultStatus.Forbidden);
            publishResult.ErrorCode.Should().Be(
                ErrorCodes.AuthorizationForbidden,
                "post-lock ownership authorization must run before readiness");
            publishRepository.SaveChangesCallCount.Should().Be(0);
            publishRepository.CommitCallCount.Should().Be(0);
            publishRepository.DisposeCallCount.Should().Be(1);

            ListingAuthoringResponse updated = updateResult.Value!;
            Guid committedGermanId = updated.Translations
                .Single(translation => translation.LanguageCode == "de")
                .Id;
            committedGermanId.Should().NotBeEmpty();
            initial.Translations.Select(translation => translation.Id)
                .Should().NotContain(committedGermanId);

            ListingAuthoringResponse persisted =
                await ReadAuthoringResponseAsync(listingId);
            persisted.Status.Should().Be(ListingStatus.Draft);
            persisted.PropertyType.Should().Be(PropertyType.Commercial);
            persisted.Price.Should().Be(345_000m);
            persisted.Translations.Select(translation => translation.LanguageCode)
                .Should().Equal("de", "en");
            ListingAuthoringTranslationResponse german = persisted.Translations
                .Single(translation => translation.LanguageCode == "de");
            german.Id.Should().Be(committedGermanId);
            german.Title.Should().Be("Committed incomplete German");
            german.City.Should().BeNull();
            german.Description.Should().Be(
                "Committed incomplete German description");
            persisted.Translations.Should().NotContain(translation =>
                translation.LanguageCode == "mk");
            persisted.ApartmentDetails.Should().BeNull();
            persisted.HouseDetails.Should().BeNull();
            persisted.CommercialDetails.Should().NotBeNull();
            persisted.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Shop);
            persisted.LandDetails.Should().BeNull();
            persisted.ModifiedAtUtc.Should().NotBeNull();
            persisted.ModifiedAtUtc.Should().BeAfter(initial.CreatedAtUtc);

            await AssertSubtypeRowsAsync(
                listingId,
                expectedApartmentRows: 0,
                expectedHouseRows: 0,
                expectedCommercialRows: 1,
                expectedLandRows: 0);
        }
        finally
        {
            updateGate.Release();
            await DrainStartedTasksAsync(
                cancellation,
                updateTask,
                publishTask);
        }
    }

    [Fact]
    public async Task UpdateFirst_PublishWaitsThenReturnsListingNotReadyForClearedLocation()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope updateDependencyScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope publishDependencyScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext updateAuthoringDbContext =
            updateDependencyScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(updateAuthoringDbContext);
        int updateBackendPid = await GetBackendProcessIdAsync(
            updateAuthoringDbContext);
        var updateGate = new GatedAuthoringRepository(
            updateDependencyScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var publishProbe = new AuthoringProgressProbe();
        await using RealEstateDbContext publishAuthoringDbContext =
            CreateProbedAuthoringDbContext(connectionString, publishProbe);
        await EnsureConnectionOpenAsync(publishAuthoringDbContext);
        int publishBackendPid = await GetBackendProcessIdAsync(
            publishAuthoringDbContext);
        var publishRepository = new ListingAuthoringRepository(
            publishAuthoringDbContext);

        UpdateListingHandler updateHandler = CreateUpdateHandler(
            updateGate,
            updateDependencyScope.ServiceProvider,
            owner.UserId);
        PublishListingHandler publishHandler = CreatePublishHandler(
            publishRepository,
            publishDependencyScope.ServiceProvider,
            owner.UserId);
        UpdateListingRequest updateRequest = CreateApartmentRequest(
            price: 333_000m,
            ("en", "Committed before publish"),
            ("de", "Vor Veröffentlichung gespeichert"));
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingAuthoringResponse>>? updateTask = null;
        Task<ServiceResult<PublicListingResponse>>? publishTask = null;

        try
        {
            updateTask = updateHandler.HandleAsync(
                listingId,
                updateRequest,
                cancellation.Token);

            await updateGate.LockAcquired.WaitAsync(TestTimeout);

            publishTask = publishHandler.HandleAsync(
                new PublishListingCommand(listingId, "de"),
                cancellation.Token);

            await publishProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                waitingBackendPid: publishBackendPid,
                blockingBackendPid: updateBackendPid,
                competingTask: publishTask,
                cancellation.Token);
            publishProbe.AggregateLoadStarted.IsCompleted.Should().BeFalse();
            publishTask.IsCompleted.Should().BeFalse(
                "publish must wait for the update's parent lock");

            updateGate.Release();

            ServiceResult<ListingAuthoringResponse> updateResult =
                await updateTask.WaitAsync(TestTimeout);
            await publishProbe.AggregateLoadStarted.WaitAsync(TestTimeout);
            ServiceResult<PublicListingResponse> publishResult =
                await publishTask.WaitAsync(TestTimeout);

            updateResult.Status.Should().Be(ServiceResultStatus.Success);
            publishResult.Status.Should().Be(ServiceResultStatus.Conflict);
            publishResult.ErrorCode.Should().Be(
                ErrorCodes.ConflictListingNotReady);

            ListingAuthoringResponse persisted =
                await ReadAuthoringResponseAsync(listingId);
            persisted.Status.Should().Be(ListingStatus.Draft);
            persisted.Price.Should().Be(333_000m);
            persisted.Translations.Select(translation => translation.LanguageCode)
                .Should().Equal("de", "en");
            persisted.Translations.Select(translation => translation.Id)
                .Should().OnlyHaveUniqueItems();
            persisted.ApartmentDetails.Should().NotBeNull();
            persisted.HouseDetails.Should().BeNull();

            await AssertSubtypeRowsAsync(
                listingId,
                expectedApartmentRows: 1,
                expectedHouseRows: 0);
        }
        finally
        {
            updateGate.Release();
            await DrainStartedTasksAsync(
                cancellation,
                updateTask,
                publishTask);
        }
    }

    [Fact]
    public async Task PublishFirst_UpdateWaitsThenReturnsResourceStateConflictWithoutMutation()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);
        Guid imageId = await AddImageAsync(listingId);
        ListingAuthoringResponse before =
            await ReadAuthoringResponseAsync(listingId);
        before.Translations.Should().OnlyContain(translation =>
            !string.IsNullOrWhiteSpace(translation.City) &&
            !string.IsNullOrWhiteSpace(translation.Description));
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope publishDependencyScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope updateDependencyScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext publishAuthoringDbContext =
            publishDependencyScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(publishAuthoringDbContext);
        int publishBackendPid = await GetBackendProcessIdAsync(
            publishAuthoringDbContext);
        var publishGate = new GatedAuthoringRepository(
            publishDependencyScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var updateProbe = new AuthoringProgressProbe();
        await using RealEstateDbContext updateAuthoringDbContext =
            CreateProbedAuthoringDbContext(connectionString, updateProbe);
        await EnsureConnectionOpenAsync(updateAuthoringDbContext);
        int updateBackendPid = await GetBackendProcessIdAsync(
            updateAuthoringDbContext);
        var updateRepository = new ListingAuthoringRepository(
            updateAuthoringDbContext);

        PublishListingHandler publishHandler = CreatePublishHandler(
            publishGate,
            publishDependencyScope.ServiceProvider,
            owner.UserId);
        UpdateListingHandler updateHandler = CreateUpdateHandler(
            updateRepository,
            updateDependencyScope.ServiceProvider,
            owner.UserId);
        UpdateListingRequest rejectedRequest = CreateApartmentRequest(
            price: 444_000m,
            ("en", "Must not be persisted"),
            ("de", "Darf nicht gespeichert werden"));
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<PublicListingResponse>>? publishTask = null;
        Task<ServiceResult<ListingAuthoringResponse>>? updateTask = null;

        try
        {
            publishTask = publishHandler.HandleAsync(
                new PublishListingCommand(listingId, "en"),
                cancellation.Token);

            await publishGate.LockAcquired.WaitAsync(TestTimeout);

            updateTask = updateHandler.HandleAsync(
                listingId,
                rejectedRequest,
                cancellation.Token);

            await updateProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                waitingBackendPid: updateBackendPid,
                blockingBackendPid: publishBackendPid,
                competingTask: updateTask,
                cancellation.Token);
            updateProbe.AggregateLoadStarted.IsCompleted.Should().BeFalse();
            updateTask.IsCompleted.Should().BeFalse(
                "update must wait for publish's parent lock");

            publishGate.Release();

            ServiceResult<PublicListingResponse> publishResult =
                await publishTask.WaitAsync(TestTimeout);
            await updateProbe.AggregateLoadStarted.WaitAsync(TestTimeout);
            ServiceResult<ListingAuthoringResponse> updateResult =
                await updateTask.WaitAsync(TestTimeout);

            publishResult.Status.Should().Be(ServiceResultStatus.Success);
            publishResult.Value!.Status.Should().Be(ListingStatus.Active);
            updateResult.Status.Should().Be(ServiceResultStatus.Conflict);
            updateResult.ErrorCode.Should().Be(ErrorCodes.ConflictResourceState);

            ListingAuthoringResponse persisted =
                await ReadAuthoringResponseAsync(listingId);
            persisted.Status.Should().Be(ListingStatus.Active);
            persisted.Should().BeEquivalentTo(
                before,
                options => options
                    .Excluding(response => response.Status)
                    .Excluding(response => response.ModifiedAtUtc));
            persisted.ModifiedAtUtc.Should().BeAfter(before.CreatedAtUtc);
            persisted.Images.Should().ContainSingle();
            persisted.Images.Single().Id.Should().Be(imageId);
            persisted.Translations.Should().NotContain(translation =>
                translation.Title == "Must not be persisted");
            persisted.ApartmentDetails.Should().NotBeNull();
            persisted.HouseDetails.Should().BeNull();

            await AssertSubtypeRowsAsync(
                listingId,
                expectedApartmentRows: 1,
                expectedHouseRows: 0);
        }
        finally
        {
            publishGate.Release();
            await DrainStartedTasksAsync(
                cancellation,
                publishTask,
                updateTask);
        }
    }

    private UpdateListingHandler CreateUpdateHandler(
        IListingAuthoringRepository repository,
        IServiceProvider serviceProvider,
        Guid userId)
    {
        return new UpdateListingHandler(
            repository,
            serviceProvider.GetRequiredService<IUserRepository>(),
            serviceProvider.GetRequiredService<AgencyListingAccessChecker>(),
            new FixedCurrentUserService(userId),
            new UpdateListingValidator(),
            new ListingDraftReplacementEngine());
    }

    private PublishListingHandler CreatePublishHandler(
        IListingAuthoringRepository repository,
        IServiceProvider serviceProvider,
        Guid userId)
    {
        return new PublishListingHandler(
            repository,
            serviceProvider.GetRequiredService<IUserRepository>(),
            serviceProvider.GetRequiredService<AgencyListingAccessChecker>(),
            new FixedCurrentUserService(userId));
    }

    private static RealEstateDbContext CreateProbedAuthoringDbContext(
        string connectionString,
        AuthoringProgressProbe probe)
    {
        DbContextOptions<RealEstateDbContext> options =
            new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(
                    new TransactionStartedProbe(probe),
                    new AggregateLoadStartedProbe(probe))
                .Options;

        return new RealEstateDbContext(options);
    }

    private async Task<string> GetConnectionStringAsync()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The integration-test connection string is unavailable.");
    }

    private static async Task EnsureConnectionOpenAsync(
        RealEstateDbContext dbContext)
    {
        if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync();
        }
    }

    private static async Task<int> GetBackendProcessIdAsync(
        RealEstateDbContext dbContext)
    {
        await using DbCommand command =
            dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_backend_pid();";

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    private async Task WaitForBlockedParentLockAsync(
        int waitingBackendPid,
        int blockingBackendPid,
        Task competingTask,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope observerScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext observerDbContext = observerScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(observerDbContext);
        DbConnection observerConnection =
            observerDbContext.Database.GetDbConnection();

        using var observationTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        observationTimeout.CancelAfter(TestTimeout);

        try
        {
            while (true)
            {
                if (competingTask.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "The competing writer completed before PostgreSQL " +
                        "reported parent-row lock contention.");
                }

                await using DbCommand command =
                    observerConnection.CreateCommand();
                command.CommandTimeout = 2;
                command.CommandText =
                    """
                    SELECT EXISTS (
                        SELECT 1
                        FROM pg_stat_activity AS activity
                        WHERE activity.datname = current_database()
                          AND activity.pid = @waitingBackendPid
                          AND activity.state = 'active'
                          AND activity.wait_event_type = 'Lock'
                          AND activity.query ILIKE '%FROM "Listings"%'
                          AND activity.query ILIKE '%FOR UPDATE%'
                          AND @blockingBackendPid =
                              ANY(pg_blocking_pids(activity.pid))
                    );
                    """;

                AddParameter(
                    command,
                    "waitingBackendPid",
                    waitingBackendPid);
                AddParameter(
                    command,
                    "blockingBackendPid",
                    blockingBackendPid);

                object? result = await command.ExecuteScalarAsync(
                    observationTimeout.Token);

                if (result is true)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException exception)
            when (observationTimeout.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "PostgreSQL did not report the competing writer blocked " +
                "on the expected parent-row lock.",
                exception);
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        int value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Int32;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<ListingAuthoringResponse> ReadAuthoringResponseAsync(
        Guid listingId)
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

    private async Task SetUserStatusAsync(
        Guid userId,
        UserStatus status)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        int updated = await dbContext.Set<User>()
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                user => user.Status,
                status));

        updated.Should().Be(1);
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
            OriginalFileName = "concurrency-proof.jpg",
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
        int expectedHouseRows,
        int expectedCommercialRows = 0,
        int expectedLandRows = 0,
        CommercialType? expectedCommercialType = null)
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
        int commercialRows = await dbContext.Set<ListingCommercialDetails>()
            .AsNoTracking()
            .CountAsync(details => details.ListingId == listingId);
        int landRows = await dbContext.Set<ListingLandDetails>()
            .AsNoTracking()
            .CountAsync(details => details.ListingId == listingId);

        apartmentRows.Should().Be(expectedApartmentRows);
        houseRows.Should().Be(expectedHouseRows);
        commercialRows.Should().Be(expectedCommercialRows);
        landRows.Should().Be(expectedLandRows);
        (apartmentRows + houseRows + commercialRows + landRows)
            .Should().Be(1);

        if (expectedCommercialType.HasValue)
        {
            ListingCommercialDetails details = await dbContext
                .Set<ListingCommercialDetails>()
                .AsNoTracking()
                .SingleAsync(current => current.ListingId == listingId);
            details.CommercialType.Should().Be(expectedCommercialType.Value);
        }
    }

    private static UpdateListingRequest CreateCommercialRequest(
        decimal price,
        CommercialType commercialType,
        params (string LanguageCode, string Title)[] translations)
    {
        return new UpdateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Commercial,
            Price = price,
            Currency = "EUR",
            AreaSquareMeters = 88m,
            Rooms = 3m,
            Bathrooms = 2m,
            BalconyCount = 1,
            ParkingSpaces = 1,
            HasBasement = true,
            IsExchangePossible = false,
            HeatingType = HeatingType.Central,
            FurnishingStatus = FurnishingStatus.Furnished,
            Condition = PropertyCondition.Good,
            YearRenovated = 2021,
            Orientation = Orientation.South,
            YearBuilt = 2010,
            ApartmentDetails = null,
            HouseDetails = null,
            CommercialDetails = new UpdateListingCommercialDetailsRequest
            {
                CommercialType = commercialType
            },
            LandDetails = null,
            Translations = translations
                .Select(translation => new UpdateListingTranslationRequest
                {
                    LanguageCode = translation.LanguageCode,
                    Title = translation.Title,
                    Description = $"{translation.Title} description",
                    AddressLine = "Concurrency address",
                    City = "Skopje",
                    Municipality = "Centar",
                    Neighborhood = "Center"
                })
                .ToList()
        };
    }

    private static UpdateListingRequest CreateApartmentRequest(
        decimal price,
        params (string LanguageCode, string Title)[] translations)
    {
        return new UpdateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = price,
            Currency = "EUR",
            AreaSquareMeters = 88m,
            Rooms = 3m,
            Bathrooms = 2m,
            BalconyCount = 1,
            ParkingSpaces = 1,
            HasBasement = true,
            IsExchangePossible = false,
            HeatingType = HeatingType.Central,
            FurnishingStatus = FurnishingStatus.Furnished,
            Condition = PropertyCondition.Good,
            YearRenovated = 2021,
            Orientation = Orientation.South,
            YearBuilt = 2010,
            ApartmentDetails = new UpdateListingApartmentDetailsRequest
            {
                ApartmentType = ApartmentType.Standard,
                Floor = 4,
                TotalFloors = 8,
                HasElevator = true
            },
            HouseDetails = null,
            Translations = translations
                .Select(translation => new UpdateListingTranslationRequest
                {
                    LanguageCode = translation.LanguageCode,
                    Title = translation.Title,
                    Description = $"{translation.Title} description",
                    AddressLine = "Concurrency address",
                    City = "Skopje",
                    Municipality = "Centar",
                    Neighborhood = "Center"
                })
                .ToList()
        };
    }

    private static async Task DrainStartedTasksAsync(
        CancellationTokenSource cancellation,
        params Task?[] tasks)
    {
        Task[] startedTasks = tasks
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

        if (startedTasks.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(startedTasks).WaitAsync(TestTimeout);
        }
        catch
        {
            await cancellation.CancelAsync();

            try
            {
                await Task.WhenAll(startedTasks).WaitAsync(TestTimeout);
            }
            catch
            {
                // Every task is observed below after cancellation.
            }

            foreach (Task task in startedTasks)
            {
                _ = task.Exception;
            }
        }
    }

    private sealed class FixedCurrentUserService(Guid userId)
        : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => true;
    }

    private sealed class GatedAuthoringRepository(
        IListingAuthoringRepository inner)
        : IListingAuthoringRepository
    {
        private readonly TaskCompletionSource<bool> _lockAcquired = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task LockAcquired => _lockAcquired.Task;

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            return inner.GetByIdReadOnlyAsync(listingId, cancellationToken);
        }

        public async Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            IListingAuthoringWriteScope? writeScope =
                await inner.BeginWriteAsync(listingId, cancellationToken);

            if (writeScope is null)
            {
                return null;
            }

            _lockAcquired.TrySetResult(true);

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return writeScope;
            }
            catch
            {
                await writeScope.DisposeAsync();
                throw;
            }
        }

        public void Release()
        {
            _release.TrySetResult(true);
        }
    }

    private sealed class RecordingAuthoringRepository(
        IListingAuthoringRepository inner)
        : IListingAuthoringRepository
    {
        public int SaveChangesCallCount { get; private set; }
        public int CommitCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            return inner.GetByIdReadOnlyAsync(listingId, cancellationToken);
        }

        public async Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            IListingAuthoringWriteScope? scope =
                await inner.BeginWriteAsync(listingId, cancellationToken);

            return scope is null
                ? null
                : new RecordingAuthoringWriteScope(this, scope);
        }

        private sealed class RecordingAuthoringWriteScope(
            RecordingAuthoringRepository owner,
            IListingAuthoringWriteScope innerScope)
            : IListingAuthoringWriteScope
        {
            public Listing Listing => innerScope.Listing;

            public void AddTranslation(ListingTranslation translation)
            {
                innerScope.AddTranslation(translation);
            }

            public void RemoveTranslation(ListingTranslation translation)
            {
                innerScope.RemoveTranslation(translation);
            }

            public void MarkListingModified()
            {
                innerScope.MarkListingModified();
            }

            public Task SaveChangesAsync(CancellationToken cancellationToken)
            {
                owner.SaveChangesCallCount++;
                return innerScope.SaveChangesAsync(cancellationToken);
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.CommitCallCount++;
                return innerScope.CommitAsync(cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                owner.DisposeCallCount++;
                await innerScope.DisposeAsync();
            }
        }
    }

    private sealed class AuthoringProgressProbe
    {
        private readonly TaskCompletionSource<bool> _transactionStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _aggregateLoadStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TransactionStarted => _transactionStarted.Task;

        public Task AggregateLoadStarted => _aggregateLoadStarted.Task;

        public void MarkTransactionStarted()
        {
            _transactionStarted.TrySetResult(true);
        }

        public void MarkAggregateLoadStarted()
        {
            _aggregateLoadStarted.TrySetResult(true);
        }
    }

    private sealed class TransactionStartedProbe(
        AuthoringProgressProbe probe)
        : DbTransactionInterceptor
    {
        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            probe.MarkTransactionStarted();

            return ValueTask.FromResult(result);
        }
    }

    private sealed class AggregateLoadStartedProbe(
        AuthoringProgressProbe probe)
        : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            probe.MarkAggregateLoadStarted();

            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
