using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.ClearListingLocation;
using RealEstate.Application.Listings.Commands.ConfirmListingLocation;
using RealEstate.Application.Listings.Commands.PublishListing;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Tests.Integration.Agencies;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingGeocodingConcurrencyTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    private static readonly DateTimeOffset ConfirmationTime =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResolvedGeocodingSnapshot ResolvedSnapshot = new(
        "provider",
        "reference",
        41.9981m,
        21.4254m,
        LocationPrecision.ExactAddress,
        "Resolved address");
    private static readonly ResolvedGeocodingSnapshot ExistingSnapshot = new(
        "existing-provider",
        "existing-reference",
        41.5m,
        21.5m,
        LocationPrecision.City,
        "Existing location");

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingGeocodingConcurrencyTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateBeforeConfirm_RejectsStaleTokenWithoutPersistingSnapshot()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope updateScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext updateDb = updateScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(updateDb);
        int updatePid = await GetBackendProcessIdAsync(updateDb);
        var updateGate = new GatedAuthoringRepository(
            updateScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        UpdateListingHandler updateHandler = CreateUpdateHandler(
            updateGate,
            updateScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            updateScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId);

        var confirmProbe = new TransactionProgressProbe();
        await using RealEstateDbContext confirmDb =
            CreateProbedDbContext(connectionString, confirmProbe);
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(confirmDb));
        var geocoder = new GatedResolutionGeocoder(
            ResolvedSnapshot,
            holdResolution: true);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmRepository,
            new UserRepository(confirmDb),
            new AgencyRepository(confirmDb),
            owner.UserId,
            token,
            geocoder);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingAuthoringResponse>>? updateTask = null;
        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;

        try
        {
            updateTask = updateHandler.HandleAsync(
                listingId,
                CreateLocationChangingRequest("Update wins"),
                cancellation.Token);
            await updateGate.LockAcquired.WaitAsync(TestTimeout);

            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Entered.WaitAsync(TestTimeout);
            confirmProbe.TransactionStarted.IsCompleted.Should().BeFalse(
                "provider resolution must still be outside confirmation's transaction");

            geocoder.Release();
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                confirmPid,
                updatePid,
                confirmTask,
                cancellation.Token);

            updateGate.Release();

            ServiceResult<ListingAuthoringResponse> updateResult =
                await updateTask.WaitAsync(TestTimeout);
            ServiceResult<ListingLocationStateResponse> confirmResult =
                await confirmTask.WaitAsync(TestTimeout);

            updateResult.Status.Should().Be(ServiceResultStatus.Success);
            confirmResult.Status.Should().Be(ServiceResultStatus.Conflict);
            confirmResult.ErrorCode.Should().Be(
                ErrorCodes.ConflictResourceSetChanged);
            confirmRepository.SaveChangesCallCount.Should().Be(0);
            confirmRepository.CommitCallCount.Should().Be(0);
            confirmRepository.DisposeCallCount.Should().Be(1);
            geocoder.CallCount.Should().Be(1);

            Listing persisted = await ReadListingAsync(listingId);
            persisted.Translations.Should().OnlyContain(translation =>
                translation.AddressLine == "Update wins address");
            AssertUnresolved(persisted);
        }
        finally
        {
            geocoder.Release();
            updateGate.Release();
            await DrainStartedTasksAsync(cancellation, updateTask, confirmTask);
        }
    }

    [Fact]
    public async Task ConfirmBeforeUpdate_UpdateWaitsThenInvalidatesSnapshot()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope confirmScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext confirmDb = confirmScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmGate = new GatedAuthoringRepository(
            confirmScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmGate,
            confirmScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            confirmScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId,
            token,
            geocoder);

        var updateProbe = new TransactionProgressProbe();
        await using RealEstateDbContext updateDb =
            CreateProbedDbContext(connectionString, updateProbe);
        await EnsureConnectionOpenAsync(updateDb);
        int updatePid = await GetBackendProcessIdAsync(updateDb);
        UpdateListingHandler updateHandler = CreateUpdateHandler(
            new ListingAuthoringRepository(updateDb),
            new UserRepository(updateDb),
            new AgencyRepository(updateDb),
            owner.UserId);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;
        Task<ServiceResult<ListingAuthoringResponse>>? updateTask = null;

        try
        {
            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmGate.LockAcquired.WaitAsync(TestTimeout);

            updateTask = updateHandler.HandleAsync(
                listingId,
                CreateLocationChangingRequest("Update follows confirm"),
                cancellation.Token);
            await updateProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                updatePid,
                confirmPid,
                updateTask,
                cancellation.Token);

            confirmGate.Release();

            ServiceResult<ListingLocationStateResponse> confirmResult =
                await confirmTask.WaitAsync(TestTimeout);
            ServiceResult<ListingAuthoringResponse> updateResult =
                await updateTask.WaitAsync(TestTimeout);

            confirmResult.Status.Should().Be(ServiceResultStatus.Success);
            updateResult.Status.Should().Be(ServiceResultStatus.Success);
            Listing persisted = await ReadListingAsync(listingId);
            persisted.Translations.Should().OnlyContain(translation =>
                translation.AddressLine == "Update follows confirm address");
            AssertUnresolved(persisted);
        }
        finally
        {
            confirmGate.Release();
            await DrainStartedTasksAsync(cancellation, confirmTask, updateTask);
        }
    }

    [Fact]
    public async Task SubtypeReplacementConcurrency_ConfirmBeforeCommercialUpdatePreservesConfirmedLocation()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        Listing before = await ReadListingAsync(listingId);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope confirmScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext confirmDb = confirmScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmGate = new GatedAuthoringRepository(
            confirmScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmGate,
            confirmScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            confirmScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId,
            token,
            geocoder);

        var updateProbe = new TransactionProgressProbe();
        await using RealEstateDbContext updateDb =
            CreateProbedDbContext(connectionString, updateProbe);
        await EnsureConnectionOpenAsync(updateDb);
        int updatePid = await GetBackendProcessIdAsync(updateDb);
        UpdateListingHandler updateHandler = CreateUpdateHandler(
            new ListingAuthoringRepository(updateDb),
            new UserRepository(updateDb),
            new AgencyRepository(updateDb),
            owner.UserId);
        UpdateListingRequest classificationOnlyRequest =
            CreateCommercialClassificationRequest(before);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;
        Task<ServiceResult<ListingAuthoringResponse>>? updateTask = null;

        try
        {
            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmGate.LockAcquired.WaitAsync(TestTimeout);
            confirmTask.IsCompleted.Should().BeFalse(
                "confirmation is held while owning the Listing parent lock");

            updateTask = updateHandler.HandleAsync(
                listingId,
                classificationOnlyRequest,
                cancellation.Token);
            await updateProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                updatePid,
                confirmPid,
                updateTask,
                cancellation.Token);
            updateTask.IsCompleted.Should().BeFalse(
                "classification replacement must wait for location confirmation");

            confirmGate.Release();

            ServiceResult<ListingLocationStateResponse> confirmResult =
                await confirmTask.WaitAsync(TestTimeout);
            ServiceResult<ListingAuthoringResponse> updateResult =
                await updateTask.WaitAsync(TestTimeout);

            confirmResult.Status.Should().Be(ServiceResultStatus.Success);
            updateResult.Status.Should().Be(ServiceResultStatus.Success);
            updateResult.Value!.PropertyType.Should().Be(PropertyType.Commercial);
            updateResult.Value.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Office);
            updateResult.Value.Latitude.Should().Be(ResolvedSnapshot.Latitude);
            updateResult.Value.Longitude.Should().Be(ResolvedSnapshot.Longitude);

            Listing persisted = await ReadListingAsync(listingId);
            persisted.PropertyType.Should().Be(PropertyType.Commercial);
            persisted.ApartmentDetails.Should().BeNull();
            persisted.HouseDetails.Should().BeNull();
            persisted.CommercialDetails.Should().NotBeNull();
            persisted.CommercialDetails!.CommercialType.Should()
                .Be(CommercialType.Office);
            persisted.LandDetails.Should().BeNull();
            AssertConfirmed(persisted, ResolvedSnapshot);
            await AssertFourSubtypeRowsAsync(
                listingId,
                expectedApartmentRows: 0,
                expectedHouseRows: 0,
                expectedCommercialRows: 1,
                expectedLandRows: 0);
        }
        finally
        {
            confirmGate.Release();
            await DrainStartedTasksAsync(cancellation, confirmTask, updateTask);
        }
    }

    [Fact]
    public async Task PublishBeforeConfirm_ConfirmWaitsThenRejectsNonDraft()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope publishScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext publishDb = publishScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(publishDb);
        int publishPid = await GetBackendProcessIdAsync(publishDb);
        var publishGate = new GatedAuthoringRepository(
            publishScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        PublishListingHandler publishHandler = CreatePublishHandler(
            publishGate,
            publishScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            publishScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId);

        var confirmProbe = new TransactionProgressProbe();
        await using RealEstateDbContext confirmDb =
            CreateProbedDbContext(connectionString, confirmProbe);
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(confirmDb));
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmRepository,
            new UserRepository(confirmDb),
            new AgencyRepository(confirmDb),
            owner.UserId,
            token,
            geocoder);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<PublicListingResponse>>? publishTask = null;
        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;

        try
        {
            publishTask = publishHandler.HandleAsync(
                new PublishListingCommand(listingId, "en"),
                cancellation.Token);
            await publishGate.LockAcquired.WaitAsync(TestTimeout);

            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                confirmPid,
                publishPid,
                confirmTask,
                cancellation.Token);

            publishGate.Release();

            ServiceResult<PublicListingResponse> publishResult =
                await publishTask.WaitAsync(TestTimeout);
            ServiceResult<ListingLocationStateResponse> confirmResult =
                await confirmTask.WaitAsync(TestTimeout);

            publishResult.Status.Should().Be(ServiceResultStatus.Success);
            confirmResult.Status.Should().Be(ServiceResultStatus.Conflict);
            confirmResult.ErrorCode.Should().Be(ErrorCodes.ConflictResourceState);
            confirmRepository.SaveChangesCallCount.Should().Be(0);
            confirmRepository.CommitCallCount.Should().Be(0);
            Listing persisted = await ReadListingAsync(listingId);
            persisted.Status.Should().Be(ListingStatus.Active);
            AssertTrustedFixtureConfirmed(persisted);
        }
        finally
        {
            publishGate.Release();
            await DrainStartedTasksAsync(cancellation, publishTask, confirmTask);
        }
    }

    [Fact]
    public async Task ConfirmBeforePublish_PublishWaitsThenCommitsActiveConfirmedState()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope confirmScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext confirmDb = confirmScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmGate = new GatedAuthoringRepository(
            confirmScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmGate,
            confirmScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            confirmScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId,
            token,
            geocoder);

        var publishProbe = new TransactionProgressProbe();
        await using RealEstateDbContext publishDb =
            CreateProbedDbContext(connectionString, publishProbe);
        await EnsureConnectionOpenAsync(publishDb);
        int publishPid = await GetBackendProcessIdAsync(publishDb);
        PublishListingHandler publishHandler = CreatePublishHandler(
            new ListingAuthoringRepository(publishDb),
            new UserRepository(publishDb),
            new AgencyRepository(publishDb),
            owner.UserId);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;
        Task<ServiceResult<PublicListingResponse>>? publishTask = null;

        try
        {
            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmGate.LockAcquired.WaitAsync(TestTimeout);

            publishTask = publishHandler.HandleAsync(
                new PublishListingCommand(listingId, "en"),
                cancellation.Token);
            await publishProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                publishPid,
                confirmPid,
                publishTask,
                cancellation.Token);

            confirmGate.Release();

            ServiceResult<ListingLocationStateResponse> confirmResult =
                await confirmTask.WaitAsync(TestTimeout);
            ServiceResult<PublicListingResponse> publishResult =
                await publishTask.WaitAsync(TestTimeout);

            confirmResult.Status.Should().Be(ServiceResultStatus.Success);
            publishResult.Status.Should().Be(ServiceResultStatus.Success);
            Listing persisted = await ReadListingAsync(listingId);
            persisted.Status.Should().Be(ListingStatus.Active);
            AssertConfirmed(persisted, ResolvedSnapshot);
        }
        finally
        {
            confirmGate.Release();
            await DrainStartedTasksAsync(cancellation, confirmTask, publishTask);
        }
    }

    [Fact]
    public async Task ConfirmBeforeClear_ClearWaitsThenRemovesSnapshot()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope confirmScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext confirmDb = confirmScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmGate = new GatedAuthoringRepository(
            confirmScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmGate,
            confirmScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            confirmScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId,
            token,
            geocoder);

        var clearProbe = new TransactionProgressProbe();
        await using RealEstateDbContext clearDb =
            CreateProbedDbContext(connectionString, clearProbe);
        await EnsureConnectionOpenAsync(clearDb);
        int clearPid = await GetBackendProcessIdAsync(clearDb);
        ClearListingLocationHandler clearHandler = CreateClearHandler(
            new ListingAuthoringRepository(clearDb),
            new UserRepository(clearDb),
            new AgencyRepository(clearDb),
            owner.UserId);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;
        Task<ServiceResult<ListingLocationStateResponse>>? clearTask = null;

        try
        {
            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmGate.LockAcquired.WaitAsync(TestTimeout);

            clearTask = clearHandler.HandleAsync(
                new ClearListingLocationCommand(listingId),
                cancellation.Token);
            await clearProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                clearPid,
                confirmPid,
                clearTask,
                cancellation.Token);

            confirmGate.Release();

            (await confirmTask.WaitAsync(TestTimeout)).Status
                .Should().Be(ServiceResultStatus.Success);
            (await clearTask.WaitAsync(TestTimeout)).Status
                .Should().Be(ServiceResultStatus.Success);
            AssertUnresolved(await ReadListingAsync(listingId));
        }
        finally
        {
            confirmGate.Release();
            await DrainStartedTasksAsync(cancellation, confirmTask, clearTask);
        }
    }

    [Fact]
    public async Task ClearBeforeConfirm_ConfirmWaitsThenReconfirmsCurrentDraft()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetConfirmedLocationAsync(listingId, ResolvedSnapshot);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope clearScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext clearDb = clearScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(clearDb);
        int clearPid = await GetBackendProcessIdAsync(clearDb);
        var clearGate = new GatedAuthoringRepository(
            clearScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>());
        ClearListingLocationHandler clearHandler = CreateClearHandler(
            clearGate,
            clearScope.ServiceProvider.GetRequiredService<IUserRepository>(),
            clearScope.ServiceProvider.GetRequiredService<IAgencyRepository>(),
            owner.UserId);

        var confirmProbe = new TransactionProgressProbe();
        await using RealEstateDbContext confirmDb =
            CreateProbedDbContext(connectionString, confirmProbe);
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            new ListingAuthoringRepository(confirmDb),
            new UserRepository(confirmDb),
            new AgencyRepository(confirmDb),
            owner.UserId,
            token,
            geocoder);
        using var cancellation = new CancellationTokenSource();

        Task<ServiceResult<ListingLocationStateResponse>>? clearTask = null;
        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;

        try
        {
            clearTask = clearHandler.HandleAsync(
                new ClearListingLocationCommand(listingId),
                cancellation.Token);
            await clearGate.LockAcquired.WaitAsync(TestTimeout);

            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                confirmPid,
                clearPid,
                confirmTask,
                cancellation.Token);

            clearGate.Release();

            (await clearTask.WaitAsync(TestTimeout)).Status
                .Should().Be(ServiceResultStatus.Success);
            (await confirmTask.WaitAsync(TestTimeout)).Status
                .Should().Be(ServiceResultStatus.Success);
            AssertConfirmed(await ReadListingAsync(listingId), ResolvedSnapshot);
        }
        finally
        {
            clearGate.Release();
            await DrainStartedTasksAsync(cancellation, clearTask, confirmTask);
        }
    }

    [Fact]
    public async Task WaitingConfirmation_ReloadsUserAndRejectsDisabledActor()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, owner.UserId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope holderScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext holderDb = holderScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(holderDb);
        int holderPid = await GetBackendProcessIdAsync(holderDb);
        await using IListingAuthoringWriteScope holder =
            await holderScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>()
                .BeginWriteAsync(listingId, CancellationToken.None)
            ?? throw new InvalidOperationException("Listing was not found.");

        var confirmProbe = new TransactionProgressProbe();
        await using RealEstateDbContext confirmDb =
            CreateProbedDbContext(connectionString, confirmProbe);
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(confirmDb));
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmRepository,
            new UserRepository(confirmDb),
            new AgencyRepository(confirmDb),
            owner.UserId,
            token,
            geocoder);
        using var cancellation = new CancellationTokenSource();
        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;

        try
        {
            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                confirmPid,
                holderPid,
                confirmTask,
                cancellation.Token);

            await SetUserStatusAsync(owner.UserId, UserStatus.Disabled);
            await holder.CommitAsync(cancellation.Token);

            ServiceResult<ListingLocationStateResponse> result =
                await confirmTask.WaitAsync(TestTimeout);
            result.Status.Should().Be(ServiceResultStatus.Forbidden);
            result.ErrorCode.Should().Be(
                ErrorCodes.AuthorizationAccountDisabled);
            confirmRepository.SaveChangesCallCount.Should().Be(0);
            confirmRepository.CommitCallCount.Should().Be(0);
            AssertUnresolved(await ReadListingAsync(listingId));
        }
        finally
        {
            await DrainStartedTasksAsync(cancellation, confirmTask);
        }
    }

    [Fact]
    public async Task WaitingClear_UsesCommittedPersonalOwnershipChange()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetConfirmedLocationAsync(listingId, ExistingSnapshot);
        Guid newOwnerId = await CreateDatabaseUserAsync();
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope holderScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext holderDb = holderScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(holderDb);
        int holderPid = await GetBackendProcessIdAsync(holderDb);
        await using IListingAuthoringWriteScope holder =
            await holderScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>()
                .BeginWriteAsync(listingId, CancellationToken.None)
            ?? throw new InvalidOperationException("Listing was not found.");
        holder.Listing.AssignCreator(newOwnerId);
        await holder.SaveChangesAsync(CancellationToken.None);

        var clearProbe = new TransactionProgressProbe();
        await using RealEstateDbContext clearDb =
            CreateProbedDbContext(connectionString, clearProbe);
        await EnsureConnectionOpenAsync(clearDb);
        int clearPid = await GetBackendProcessIdAsync(clearDb);
        var clearRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(clearDb));
        ClearListingLocationHandler clearHandler = CreateClearHandler(
            clearRepository,
            new UserRepository(clearDb),
            new AgencyRepository(clearDb),
            owner.UserId);
        using var cancellation = new CancellationTokenSource();
        Task<ServiceResult<ListingLocationStateResponse>>? clearTask = null;

        try
        {
            clearTask = clearHandler.HandleAsync(
                new ClearListingLocationCommand(listingId),
                cancellation.Token);
            await clearProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                clearPid,
                holderPid,
                clearTask,
                cancellation.Token);

            await holder.CommitAsync(cancellation.Token);

            ServiceResult<ListingLocationStateResponse> result =
                await clearTask.WaitAsync(TestTimeout);
            result.Status.Should().Be(ServiceResultStatus.Forbidden);
            result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
            clearRepository.SaveChangesCallCount.Should().Be(0);
            clearRepository.CommitCallCount.Should().Be(0);
            Listing persisted = await ReadListingAsync(listingId);
            persisted.CreatedByUserId.Should().Be(newOwnerId);
            AssertConfirmed(persisted, ExistingSnapshot);
        }
        finally
        {
            await DrainStartedTasksAsync(cancellation, clearTask);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitingClear_UsesLatestAgencyRoleAndMembershipStatus(
        bool disableMembership)
    {
        (Guid listingId, AuthenticatedTestUser owner, Guid agencyId) =
            await CreateAgencyListingAsync();
        await SetConfirmedLocationAsync(listingId, ExistingSnapshot);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope holderScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext holderDb = holderScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(holderDb);
        int holderPid = await GetBackendProcessIdAsync(holderDb);
        await using IListingAuthoringWriteScope holder =
            await holderScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>()
                .BeginWriteAsync(listingId, CancellationToken.None)
            ?? throw new InvalidOperationException("Listing was not found.");

        var clearProbe = new TransactionProgressProbe();
        await using RealEstateDbContext clearDb =
            CreateProbedDbContext(connectionString, clearProbe);
        await EnsureConnectionOpenAsync(clearDb);
        int clearPid = await GetBackendProcessIdAsync(clearDb);
        var clearRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(clearDb));
        ClearListingLocationHandler clearHandler = CreateClearHandler(
            clearRepository,
            new UserRepository(clearDb),
            new AgencyRepository(clearDb),
            owner.UserId);
        using var cancellation = new CancellationTokenSource();
        Task<ServiceResult<ListingLocationStateResponse>>? clearTask = null;

        try
        {
            clearTask = clearHandler.HandleAsync(
                new ClearListingLocationCommand(listingId),
                cancellation.Token);
            await clearProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                clearPid,
                holderPid,
                clearTask,
                cancellation.Token);

            await RevokeAgencyAccessAsync(
                agencyId,
                owner.UserId,
                disableMembership);
            await holder.CommitAsync(cancellation.Token);

            ServiceResult<ListingLocationStateResponse> result =
                await clearTask.WaitAsync(TestTimeout);
            result.Status.Should().Be(ServiceResultStatus.Forbidden);
            result.ErrorCode.Should().Be(ErrorCodes.AuthorizationForbidden);
            clearRepository.SaveChangesCallCount.Should().Be(0);
            clearRepository.CommitCallCount.Should().Be(0);
            AssertConfirmed(await ReadListingAsync(listingId), ExistingSnapshot);
        }
        finally
        {
            await DrainStartedTasksAsync(cancellation, clearTask);
        }
    }

    [Fact]
    public async Task WaitingConfirmation_RejectsActorDeletedAfterPreflight()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agentId = await CreateDatabaseUserAsync();
        Guid agencyId = await CreateAgencyAsync(owner.UserId, agentId);
        Guid listingId = await ListingTestHelpers.CreateListingAsAsync(
            _httpClient,
            owner,
            agencyId);
        await SetConfirmedLocationAsync(listingId, ExistingSnapshot);
        LocationConfirmationTokenPayload token =
            await CreateTokenAsync(listingId, agentId);
        string connectionString = await GetConnectionStringAsync();

        await using AsyncServiceScope holderScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext holderDb = holderScope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await EnsureConnectionOpenAsync(holderDb);
        int holderPid = await GetBackendProcessIdAsync(holderDb);
        await using IListingAuthoringWriteScope holder =
            await holderScope.ServiceProvider
                .GetRequiredService<IListingAuthoringRepository>()
                .BeginWriteAsync(listingId, CancellationToken.None)
            ?? throw new InvalidOperationException("Listing was not found.");

        var confirmProbe = new TransactionProgressProbe();
        await using RealEstateDbContext confirmDb =
            CreateProbedDbContext(connectionString, confirmProbe);
        await EnsureConnectionOpenAsync(confirmDb);
        int confirmPid = await GetBackendProcessIdAsync(confirmDb);
        var confirmRepository = new RecordingAuthoringRepository(
            new ListingAuthoringRepository(confirmDb));
        var geocoder = new GatedResolutionGeocoder(ResolvedSnapshot);
        ConfirmListingLocationHandler confirmHandler = CreateConfirmHandler(
            confirmRepository,
            new UserRepository(confirmDb),
            new AgencyRepository(confirmDb),
            agentId,
            token,
            geocoder);
        using var cancellation = new CancellationTokenSource();
        Task<ServiceResult<ListingLocationStateResponse>>? confirmTask = null;

        try
        {
            confirmTask = confirmHandler.HandleAsync(
                new ConfirmListingLocationCommand(listingId, "token"),
                cancellation.Token);
            await geocoder.Completed.WaitAsync(TestTimeout);
            await confirmProbe.TransactionStarted.WaitAsync(TestTimeout);
            await WaitForBlockedParentLockAsync(
                confirmPid,
                holderPid,
                confirmTask,
                cancellation.Token);

            await DeleteAgencyMemberAndUserAsync(agencyId, agentId);
            await holder.CommitAsync(cancellation.Token);

            ServiceResult<ListingLocationStateResponse> result =
                await confirmTask.WaitAsync(TestTimeout);
            result.Status.Should().Be(ServiceResultStatus.Unauthorized);
            result.ErrorCode.Should().Be(
                ErrorCodes.AuthenticationInvalidPrincipal);
            confirmRepository.SaveChangesCallCount.Should().Be(0);
            confirmRepository.CommitCallCount.Should().Be(0);
            AssertConfirmed(await ReadListingAsync(listingId), ExistingSnapshot);
        }
        finally
        {
            await DrainStartedTasksAsync(cancellation, confirmTask);
        }
    }

    private ConfirmListingLocationHandler CreateConfirmHandler(
        IListingAuthoringRepository repository,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        Guid userId,
        LocationConfirmationTokenPayload token,
        IListingGeocoder geocoder)
    {
        return new ConfirmListingLocationHandler(
            repository,
            userRepository,
            new AgencyListingAccessChecker(agencyRepository),
            new FixedCurrentUserService(userId),
            geocoder,
            new FixedTokenProtector(token),
            new FixedTimeProvider(ConfirmationTime));
    }

    private static ClearListingLocationHandler CreateClearHandler(
        IListingAuthoringRepository repository,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        Guid userId)
    {
        return new ClearListingLocationHandler(
            repository,
            userRepository,
            new AgencyListingAccessChecker(agencyRepository),
            new FixedCurrentUserService(userId));
    }

    private static UpdateListingHandler CreateUpdateHandler(
        IListingAuthoringRepository repository,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        Guid userId)
    {
        return new UpdateListingHandler(
            repository,
            userRepository,
            new AgencyListingAccessChecker(agencyRepository),
            new FixedCurrentUserService(userId),
            new UpdateListingValidator(),
            new ListingDraftReplacementEngine());
    }

    private static PublishListingHandler CreatePublishHandler(
        IListingAuthoringRepository repository,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        Guid userId)
    {
        return new PublishListingHandler(
            repository,
            userRepository,
            new AgencyListingAccessChecker(agencyRepository),
            new FixedCurrentUserService(userId));
    }

    private async Task<LocationConfirmationTokenPayload> CreateTokenAsync(
        Guid listingId,
        Guid actorUserId)
    {
        Listing listing = await ReadListingAsync(listingId);
        CanonicalListingLocation canonicalLocation =
            CanonicalListingLocation.From(listing.Translations.Select(
                translation => new CanonicalListingLocationInput(
                    translation.LanguageCode,
                    translation.City,
                    translation.Municipality,
                    translation.AddressLine,
                    translation.Neighborhood)));
        LocationConfirmationTokenClaims claims =
            LocationConfirmationTokenClaims.Create(
                listingId,
                actorUserId,
                ResolvedSnapshot.ProviderKey,
                ResolvedSnapshot.ResultReference,
                "en",
                ListingLocationFingerprint.Compute(canonicalLocation));

        return LocationConfirmationTokenPayload.CreateCurrent(
            claims,
            ConfirmationTime.AddMinutes(-1),
            ConfirmationTime.AddMinutes(9));
    }

    private static UpdateListingRequest CreateCommercialClassificationRequest(
        Listing listing)
    {
        return new UpdateListingRequest
        {
            ListingType = listing.ListingType,
            PropertyType = PropertyType.Commercial,
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
            ApartmentDetails = null,
            HouseDetails = null,
            CommercialDetails = new UpdateListingCommercialDetailsRequest
            {
                CommercialType = CommercialType.Office
            },
            LandDetails = null,
            Translations = listing.Translations
                .OrderBy(translation => translation.LanguageCode)
                .Select(translation => new UpdateListingTranslationRequest
                {
                    LanguageCode = translation.LanguageCode,
                    Title = translation.Title,
                    Description = translation.Description,
                    AddressLine = translation.AddressLine,
                    City = translation.City,
                    Municipality = translation.Municipality,
                    Neighborhood = translation.Neighborhood
                })
                .ToList()
        };
    }

    private static UpdateListingRequest CreateLocationChangingRequest(
        string title)
    {
        return new UpdateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            Price = 125_000m,
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
            Translations =
            [
                new UpdateListingTranslationRequest
                {
                    LanguageCode = "en",
                    Title = title,
                    Description = $"{title} description",
                    AddressLine = $"{title} address",
                    City = "Skopje",
                    Municipality = "Centar",
                    Neighborhood = "Center"
                },
                new UpdateListingTranslationRequest
                {
                    LanguageCode = "mk",
                    Title = $"{title} mk",
                    Description = $"{title} mk description",
                    AddressLine = $"{title} address",
                    City = "Скопје",
                    Municipality = "Центар",
                    Neighborhood = "Центар"
                }
            ]
        };
    }

    private static RealEstateDbContext CreateProbedDbContext(
        string connectionString,
        TransactionProgressProbe probe)
    {
        DbContextOptions<RealEstateDbContext> options =
            new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(new TransactionStartedProbe(probe))
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

        return Convert.ToInt32(await command.ExecuteScalarAsync());
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
                AddParameter(command, "waitingBackendPid", waitingBackendPid);
                AddParameter(command, "blockingBackendPid", blockingBackendPid);

                if (await command.ExecuteScalarAsync(
                        observationTimeout.Token) is true)
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

    private async Task AssertFourSubtypeRowsAsync(
        Guid listingId,
        int expectedApartmentRows,
        int expectedHouseRows,
        int expectedCommercialRows,
        int expectedLandRows)
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
    }

    private async Task SetConfirmedLocationAsync(
        Guid listingId,
        ResolvedGeocodingSnapshot snapshot)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings.SingleAsync(
            current => current.Id == listingId);
        listing.ConfirmLocation(
            snapshot.Latitude,
            snapshot.Longitude,
            snapshot.Precision,
            snapshot.ProviderKey,
            snapshot.ResultReference,
            snapshot.DisplayName,
            ConfirmationTime.UtcDateTime);
        await dbContext.SaveChangesAsync();
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

    private async Task<Guid> CreateDatabaseUserAsync()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        var user = new User(
            $"concurrency-{Guid.NewGuid():N}@example.com",
            "password-hash",
            "Concurrency",
            "Actor",
            null,
            status: UserStatus.Active);
        dbContext.Set<User>().Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(Guid ListingId, AuthenticatedTestUser Owner, Guid AgencyId)>
        CreateAgencyListingAsync()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsync(owner.UserId);
        Guid listingId = await ListingTestHelpers.CreateListingAsAsync(
            _httpClient,
            owner,
            agencyId);
        return (listingId, owner, agencyId);
    }

    private async Task<Guid> CreateAgencyAsync(
        Guid ownerUserId,
        Guid? agentUserId = null)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Agency agency = AgencyTestHelpers.CreateAgency();
        agency.AddMember(ownerUserId, AgencyMemberRole.Owner);
        if (agentUserId.HasValue)
        {
            agency.AddMember(agentUserId.Value, AgencyMemberRole.Agent);
        }
        dbContext.Agencies.Add(agency);
        await dbContext.SaveChangesAsync();
        return agency.Id;
    }

    private async Task RevokeAgencyAccessAsync(
        Guid agencyId,
        Guid userId,
        bool disableMembership)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        AgencyMember member = await dbContext.Set<AgencyMember>().SingleAsync(
            current =>
                current.AgencyId == agencyId &&
                current.UserId == userId);
        if (disableMembership)
        {
            member.Disable();
        }
        else
        {
            member.ChangeRole(AgencyMemberRole.Manager);
        }
        await dbContext.SaveChangesAsync();
    }

    private async Task DeleteAgencyMemberAndUserAsync(
        Guid agencyId,
        Guid userId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        int membersDeleted = await dbContext.Set<AgencyMember>()
            .Where(member =>
                member.AgencyId == agencyId &&
                member.UserId == userId)
            .ExecuteDeleteAsync();
        int usersDeleted = await dbContext.Set<User>()
            .Where(user => user.Id == userId)
            .ExecuteDeleteAsync();
        membersDeleted.Should().Be(1);
        usersDeleted.Should().Be(1);
    }

    private static void AssertUnresolved(Listing listing)
    {
        listing.Latitude.Should().BeNull();
        listing.Longitude.Should().BeNull();
        listing.LocationPrecision.Should().BeNull();
        listing.GeocodingProviderKey.Should().BeNull();
        listing.GeocodingResultReference.Should().BeNull();
        listing.GeocodedDisplayName.Should().BeNull();
        listing.LocationConfirmedAtUtc.Should().BeNull();
    }

    private static void AssertConfirmed(
        Listing listing,
        ResolvedGeocodingSnapshot snapshot)
    {
        listing.Latitude.Should().Be(snapshot.Latitude);
        listing.Longitude.Should().Be(snapshot.Longitude);
        listing.LocationPrecision.Should().Be(snapshot.Precision);
        listing.GeocodingProviderKey.Should().Be(snapshot.ProviderKey);
        listing.GeocodingResultReference.Should().Be(snapshot.ResultReference);
        listing.GeocodedDisplayName.Should().Be(snapshot.DisplayName);
        listing.LocationConfirmedAtUtc.Should().Be(
            ConfirmationTime.UtcDateTime);
    }

    private static void AssertTrustedFixtureConfirmed(Listing listing)
    {
        listing.Latitude.Should().Be(
            StrongLocationListingTestFixtures.ConfirmedLatitude);
        listing.Longitude.Should().Be(
            StrongLocationListingTestFixtures.ConfirmedLongitude);
        listing.LocationPrecision.Should().Be(LocationPrecision.ExactAddress);
        listing.GeocodingProviderKey.Should().Be(
            StrongLocationListingTestFixtures.TestProviderKey);
        listing.GeocodingResultReference.Should().Be(
            StrongLocationListingTestFixtures.TestResultReference);
        listing.GeocodedDisplayName.Should().Be(
            StrongLocationListingTestFixtures.TestDisplayName);
        listing.LocationConfirmedAtUtc.Should().Be(
            StrongLocationListingTestFixtures.ConfirmedAtUtc);
    }

    private static async Task DrainStartedTasksAsync(
        CancellationTokenSource cancellation,
        params Task?[] tasks)
    {
        Task[] started = tasks
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();
        if (started.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(started).WaitAsync(TestTimeout);
        }
        catch
        {
            await cancellation.CancelAsync();
            try
            {
                await Task.WhenAll(started).WaitAsync(TestTimeout);
            }
            catch
            {
                // Every started task is observed below after cancellation.
            }
            foreach (Task task in started)
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FixedTokenProtector(
        LocationConfirmationTokenPayload payload)
        : ILocationConfirmationTokenProtector
    {
        public string Protect(LocationConfirmationTokenClaims claims) =>
            throw new InvalidOperationException("Protect was not expected.");

        public LocationConfirmationTokenUnprotectResult Unprotect(
            string protectedToken) =>
            LocationConfirmationTokenUnprotectResult.Success(payload);
    }

    private sealed class GatedResolutionGeocoder(
        ResolvedGeocodingSnapshot snapshot,
        bool holdResolution = false)
        : IListingGeocoder
    {
        private readonly TaskCompletionSource<bool> _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _completed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public Task Completed => _completed.Task;
        public int CallCount { get; private set; }

        public Task<GeocodingSearchResult> SearchAsync(
            GeocodingSearchInput input,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Search was not expected.");

        public async Task<GeocodingResolutionResult> ResolveAsync(
            GeocodingReference reference,
            CancellationToken cancellationToken)
        {
            CallCount++;
            reference.Should().Be(new GeocodingReference(
                snapshot.ProviderKey,
                snapshot.ResultReference));
            _entered.TrySetResult(true);
            if (holdResolution)
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            _completed.TrySetResult(true);
            return GeocodingResolutionResult.Success(snapshot);
        }

        public void Release()
        {
            _release.TrySetResult(true);
        }
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
            CancellationToken cancellationToken) =>
            inner.GetByIdReadOnlyAsync(listingId, cancellationToken);

        public async Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            IListingAuthoringWriteScope? scope =
                await inner.BeginWriteAsync(listingId, cancellationToken);
            if (scope is null)
            {
                return null;
            }
            _lockAcquired.TrySetResult(true);
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return scope;
            }
            catch
            {
                await scope.DisposeAsync();
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
            CancellationToken cancellationToken) =>
            inner.GetByIdReadOnlyAsync(listingId, cancellationToken);

        public async Task<IListingAuthoringWriteScope?> BeginWriteAsync(
            Guid listingId,
            CancellationToken cancellationToken)
        {
            IListingAuthoringWriteScope? scope =
                await inner.BeginWriteAsync(listingId, cancellationToken);
            return scope is null
                ? null
                : new RecordingScope(this, scope);
        }

        private sealed class RecordingScope(
            RecordingAuthoringRepository owner,
            IListingAuthoringWriteScope inner)
            : IListingAuthoringWriteScope
        {
            public Listing Listing => inner.Listing;

            public void AddTranslation(ListingTranslation translation) =>
                inner.AddTranslation(translation);

            public void RemoveTranslation(ListingTranslation translation) =>
                inner.RemoveTranslation(translation);

            public void MarkListingModified() => inner.MarkListingModified();

            public Task SaveChangesAsync(CancellationToken cancellationToken)
            {
                owner.SaveChangesCallCount++;
                return inner.SaveChangesAsync(cancellationToken);
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.CommitCallCount++;
                return inner.CommitAsync(cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                owner.DisposeCallCount++;
                await inner.DisposeAsync();
            }
        }
    }

    private sealed class TransactionProgressProbe
    {
        private readonly TaskCompletionSource<bool> _transactionStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TransactionStarted => _transactionStarted.Task;

        public void MarkTransactionStarted()
        {
            _transactionStarted.TrySetResult(true);
        }
    }

    private sealed class TransactionStartedProbe(
        TransactionProgressProbe probe)
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
}
