using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Storage;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests
{
    private static readonly TimeSpan ListingImageConcurrencyTimeout =
        TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ListingImageUploadConcurrency_TwoUploadsFromNineteenImages_OneWinsAndCapacityLoserIsCompensated()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        string connectionString = GetRequiredTestConnectionString();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            connectionString,
            storageRoot,
            coordinator);

        using var timeoutSource =
            new CancellationTokenSource(
                ListingImageConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeoutSource.Token;

        HttpClient? firstClient = null;
        HttpClient? secondClient = null;
        MultipartFormDataContent? firstContent = null;
        MultipartFormDataContent? secondContent = null;
        Task<HttpResponseMessage>? firstRequestTask = null;
        Task<HttpResponseMessage>? secondRequestTask = null;
        HttpResponseMessage? firstResponse = null;
        HttpResponseMessage? secondResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient =
                localFactory.CreateClient();

            (
                Guid listingId,
                AuthenticatedTestUser owner
            ) = await ListingTestHelpers
                .CreateListingWithOwnerAsync(
                    setupClient);

            List<ListingImage> seedImages =
                await SeedListingImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId,
                    count: 19,
                    cancellationToken);

            List<ListingImage> initialImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            initialImages.Should().HaveCount(19);
            initialImages.Count(image => image.IsPrimary)
                .Should().Be(1);

            string listingDirectory =
                GetConcurrencyListingDirectory(
                    storageRoot,
                    listingId);

            GetFinalFiles(listingDirectory)
                .Should().HaveCount(19);

            AssertNoTemporaryFiles(storageRoot);

            gateScope =
                localFactory.Services.CreateScope();

            observerScope =
                localFactory.Services.CreateScope();

            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            gateTransaction =
                await gateDbContext.Database
                    .BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);

            int gateBackendPid =
                await GetListingGateBackendPidAsync(
                    gateDbContext,
                    cancellationToken);

            firstClient = localFactory.CreateClient();
            secondClient = localFactory.CreateClient();

            firstClient.AuthorizeAs(owner.AccessToken);
            secondClient.AuthorizeAs(owner.AccessToken);

            firstContent =
                CreateConcurrencyUploadContent(
                    "capacity-first.jpg");

            secondContent =
                CreateConcurrencyUploadContent(
                    "capacity-second.jpg");

            firstRequestTask = firstClient.PostAsync(
                $"/api/listings/{listingId}/images",
                firstContent,
                cancellationToken);

            secondRequestTask = secondClient.PostAsync(
                $"/api/listings/{listingId}/images",
                secondContent,
                cancellationToken);

            await coordinator.WaitForTwoSavesAsync(
                cancellationToken);

            IReadOnlyList<ListingImageSaveRecord>
                gatedSaveRecords = coordinator.GetSaveRecords();

            gatedSaveRecords.Should().HaveCount(2);

            int firstRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: null,
                    cancellationToken);

            int secondRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid:
                        firstRequestBackendPid,
                    cancellationToken);

            secondRequestBackendPid.Should()
                .NotBe(firstRequestBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            gatedImages.Should().HaveCount(19);

            string[] gatedFiles =
                GetFinalFiles(listingDirectory);

            gatedFiles.Should().HaveCount(21);

            foreach (ListingImageSaveRecord saveRecord
                     in gatedSaveRecords)
            {
                gatedFiles.Select(Path.GetFileName)
                    .Should()
                    .Contain(
                        saveRecord.StoredFile
                            .StoredFileName);
            }

            AssertNoTemporaryFiles(storageRoot);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            firstResponse =
                await firstRequestTask.WaitAsync(
                    cancellationToken);

            secondResponse =
                await secondRequestTask.WaitAsync(
                    cancellationToken);

            HttpResponseMessage[] responses =
            [
                firstResponse,
                secondResponse
            ];

            responses.Count(response =>
                    response.StatusCode ==
                    HttpStatusCode.Created)
                .Should().Be(1);

            responses.Count(response =>
                    response.StatusCode ==
                    HttpStatusCode.BadRequest)
                .Should().Be(1);

            HttpResponseMessage successfulResponse =
                responses.Single(response =>
                    response.StatusCode ==
                    HttpStatusCode.Created);

            HttpResponseMessage losingResponse =
                responses.Single(response =>
                    response.StatusCode ==
                    HttpStatusCode.BadRequest);

            string losingResponseBody =
                await losingResponse.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            losingResponseBody.Should().Contain(
                "Listing cannot have more than 20 images.");

            ListingImageResponse? successfulImage =
                await successfulResponse.Content
                    .ReadFromJsonAsync<ListingImageResponse>(
                        cancellationToken);

            successfulImage.Should().NotBeNull();
            successfulImage!.Id.Should().NotBeEmpty();
            successfulImage.SortOrder.Should().Be(19);
            successfulImage.IsPrimary.Should().BeFalse();

            await coordinator.WaitForOneDeleteAsync(
                cancellationToken);

            IReadOnlyList<ListingImageSaveRecord> saveRecords =
                coordinator.GetSaveRecords();

            ListingImageSaveRecord winningSave =
                saveRecords.Single(record =>
                    record.StoredFile.Url ==
                    successfulImage.Url);

            ListingImageSaveRecord losingSave =
                saveRecords.Single(record =>
                    record.StoredFile.StoredFileName !=
                    winningSave.StoredFile.StoredFileName);

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            finalImages.Should().HaveCount(20);

            finalImages.Select(image => image.SortOrder)
                .OrderBy(sortOrder => sortOrder)
                .Should().Equal(
                    Enumerable.Range(0, 20));

            finalImages.Select(image => image.SortOrder)
                .Should().OnlyHaveUniqueItems();

            finalImages.Count(image => image.IsPrimary)
                .Should().Be(1);

            foreach (ListingImage seedImage in seedImages)
            {
                finalImages.Should().ContainSingle(image =>
                    image.Id == seedImage.Id &&
                    image.StoredFileName ==
                    seedImage.StoredFileName);
            }

            finalImages.Should().ContainSingle(image =>
                image.Id == successfulImage.Id);

            finalImages.Count(image =>
                    saveRecords.Any(record =>
                        record.StoredFile.StoredFileName ==
                        image.StoredFileName))
                .Should().Be(1);

            finalImages.Should().ContainSingle(image =>
                image.StoredFileName ==
                winningSave.StoredFile.StoredFileName);

            finalImages.Should().NotContain(image =>
                image.StoredFileName ==
                losingSave.StoredFile.StoredFileName);

            string[] finalFiles =
                GetFinalFiles(listingDirectory);

            finalFiles.Should().HaveCount(20);

            string[] finalFileNames =
                finalFiles.Select(Path.GetFileName)
                    .ToArray()!;

            foreach (ListingImage seedImage in seedImages)
            {
                finalFileNames.Should().Contain(
                    seedImage.StoredFileName);
            }

            finalFileNames.Should().Contain(
                winningSave.StoredFile.StoredFileName);

            finalFileNames.Should().NotContain(
                losingSave.StoredFile.StoredFileName);

            IReadOnlyList<ListingImageDeleteRecord>
                deleteRecords = coordinator.GetDeleteRecords();

            coordinator.GetSaveRecords().Should()
                .HaveCount(2);

            deleteRecords.Should().ContainSingle();

            ListingImageDeleteRecord deleteRecord =
                deleteRecords.Single();

            deleteRecord.ListingId.Should()
                .Be(listingId);

            deleteRecord.StoredFileName.Should()
                .Be(losingSave.StoredFile.StoredFileName);

            seedImages.Select(image => image.StoredFileName)
                .Should().NotContain(
                    deleteRecord.StoredFileName);

            deleteRecord.CancellationToken.Should()
                .Be(CancellationToken.None);

            deleteRecord.CancellationToken.CanBeCanceled
                .Should().BeFalse();

            AssertNoTemporaryFiles(storageRoot);

            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((
                    firstRequestTask is not null &&
                    !firstRequestTask.IsCompleted
                ) ||
                (
                    secondRequestTask is not null &&
                    !secondRequestTask.IsCompleted
                ))
            {
                timeoutSource.Cancel();
                firstClient?.CancelPendingRequests();
                secondClient?.CancelPendingRequests();
            }

            if (!gateReleased &&
                gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (firstResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(
                    firstRequestTask);
            }
            else
            {
                firstResponse.Dispose();
            }

            if (secondResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(
                    secondRequestTask);
            }
            else
            {
                secondResponse.Dispose();
            }

            firstContent?.Dispose();
            secondContent?.Dispose();
            firstClient?.Dispose();
            secondClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();

            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageUploadConcurrency_TwoFirstUploads_CreateOrdersZeroAndOneWithOnePrimary()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        string connectionString = GetRequiredTestConnectionString();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            connectionString,
            storageRoot,
            coordinator);

        using var timeoutSource =
            new CancellationTokenSource(
                ListingImageConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeoutSource.Token;

        HttpClient? firstClient = null;
        HttpClient? secondClient = null;
        MultipartFormDataContent? firstContent = null;
        MultipartFormDataContent? secondContent = null;
        Task<HttpResponseMessage>? firstRequestTask = null;
        Task<HttpResponseMessage>? secondRequestTask = null;
        HttpResponseMessage? firstResponse = null;
        HttpResponseMessage? secondResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient =
                localFactory.CreateClient();

            (
                Guid listingId,
                AuthenticatedTestUser owner
            ) = await ListingTestHelpers
                .CreateListingWithOwnerAsync(
                    setupClient);

            List<ListingImage> initialImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            initialImages.Should().BeEmpty();

            string listingDirectory =
                GetConcurrencyListingDirectory(
                    storageRoot,
                    listingId);

            GetFinalFiles(listingDirectory)
                .Should().BeEmpty();

            AssertNoTemporaryFiles(storageRoot);

            gateScope =
                localFactory.Services.CreateScope();

            observerScope =
                localFactory.Services.CreateScope();

            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            gateTransaction =
                await gateDbContext.Database
                    .BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);

            int gateBackendPid =
                await GetListingGateBackendPidAsync(
                    gateDbContext,
                    cancellationToken);

            firstClient = localFactory.CreateClient();
            secondClient = localFactory.CreateClient();

            firstClient.AuthorizeAs(owner.AccessToken);
            secondClient.AuthorizeAs(owner.AccessToken);

            firstContent =
                CreateConcurrencyUploadContent(
                    "first-primary-a.jpg");

            secondContent =
                CreateConcurrencyUploadContent(
                    "first-primary-b.jpg");

            firstRequestTask = firstClient.PostAsync(
                $"/api/listings/{listingId}/images",
                firstContent,
                cancellationToken);

            secondRequestTask = secondClient.PostAsync(
                $"/api/listings/{listingId}/images",
                secondContent,
                cancellationToken);

            await coordinator.WaitForTwoSavesAsync(
                cancellationToken);

            IReadOnlyList<ListingImageSaveRecord>
                gatedSaveRecords = coordinator.GetSaveRecords();

            gatedSaveRecords.Should().HaveCount(2);

            int firstRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: null,
                    cancellationToken);

            int secondRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid:
                        firstRequestBackendPid,
                    cancellationToken);

            secondRequestBackendPid.Should()
                .NotBe(firstRequestBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            gatedImages.Should().BeEmpty();

            string[] gatedFiles =
                GetFinalFiles(listingDirectory);

            gatedFiles.Should().HaveCount(2);

            foreach (ListingImageSaveRecord saveRecord
                     in gatedSaveRecords)
            {
                gatedFiles.Select(Path.GetFileName)
                    .Should()
                    .Contain(
                        saveRecord.StoredFile
                            .StoredFileName);
            }

            AssertNoTemporaryFiles(storageRoot);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            firstResponse =
                await firstRequestTask.WaitAsync(
                    cancellationToken);

            secondResponse =
                await secondRequestTask.WaitAsync(
                    cancellationToken);

            firstResponse.StatusCode.Should()
                .Be(HttpStatusCode.Created);

            secondResponse.StatusCode.Should()
                .Be(HttpStatusCode.Created);

            ListingImageResponse? firstImage =
                await firstResponse.Content
                    .ReadFromJsonAsync<ListingImageResponse>(
                        cancellationToken);

            ListingImageResponse? secondImage =
                await secondResponse.Content
                    .ReadFromJsonAsync<ListingImageResponse>(
                        cancellationToken);

            firstImage.Should().NotBeNull();
            secondImage.Should().NotBeNull();

            ListingImageResponse[] responseImages =
            [
                firstImage!,
                secondImage!
            ];

            responseImages.Select(image => image.Id)
                .Should().NotContain(Guid.Empty);

            responseImages.Select(image => image.Id)
                .Should().OnlyHaveUniqueItems();

            responseImages.Select(image => image.SortOrder)
                .OrderBy(sortOrder => sortOrder)
                .Should().Equal(0, 1);

            responseImages.Count(image => image.IsPrimary)
                .Should().Be(1);

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            finalImages.Should().HaveCount(2);

            finalImages.Select(image => image.Id)
                .OrderBy(id => id)
                .Should().Equal(
                    responseImages.Select(image => image.Id)
                        .OrderBy(id => id));

            finalImages.Select(image => image.SortOrder)
                .OrderBy(sortOrder => sortOrder)
                .Should().Equal(0, 1);

            finalImages.Select(image => image.SortOrder)
                .Should().OnlyHaveUniqueItems();

            finalImages.Count(image => image.IsPrimary)
                .Should().Be(1);

            finalImages.Single(image =>
                    image.SortOrder == 0)
                .IsPrimary.Should().BeTrue();

            finalImages.Single(image =>
                    image.SortOrder == 1)
                .IsPrimary.Should().BeFalse();

            IReadOnlyList<ListingImageSaveRecord> saveRecords =
                coordinator.GetSaveRecords();

            saveRecords.Should().HaveCount(2);

            foreach (ListingImageSaveRecord saveRecord
                     in saveRecords)
            {
                finalImages.Should().ContainSingle(image =>
                    image.StoredFileName ==
                    saveRecord.StoredFile.StoredFileName);
            }

            string[] finalFiles =
                GetFinalFiles(listingDirectory);

            finalFiles.Should().HaveCount(2);

            string[] finalFileNames =
                finalFiles.Select(Path.GetFileName)
                    .ToArray()!;

            foreach (ListingImageSaveRecord saveRecord
                     in saveRecords)
            {
                finalFileNames.Should().Contain(
                    saveRecord.StoredFile.StoredFileName);
            }

            coordinator.GetDeleteRecords()
                .Should().BeEmpty();

            AssertNoTemporaryFiles(storageRoot);

            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((
                    firstRequestTask is not null &&
                    !firstRequestTask.IsCompleted
                ) ||
                (
                    secondRequestTask is not null &&
                    !secondRequestTask.IsCompleted
                ))
            {
                timeoutSource.Cancel();
                firstClient?.CancelPendingRequests();
                secondClient?.CancelPendingRequests();
            }

            if (!gateReleased &&
                gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (firstResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(
                    firstRequestTask);
            }
            else
            {
                firstResponse.Dispose();
            }

            if (secondResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(
                    secondRequestTask);
            }
            else
            {
                secondResponse.Dispose();
            }

            firstContent?.Dispose();
            secondContent?.Dispose();
            firstClient?.Dispose();
            secondClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();

            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageUploadConcurrency_TwoAppendsFromExistingImages_CreateDistinctSequentialOrders()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        string connectionString = GetRequiredTestConnectionString();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            connectionString,
            storageRoot,
            coordinator);

        using var timeoutSource =
            new CancellationTokenSource(
                ListingImageConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeoutSource.Token;

        HttpClient? firstClient = null;
        HttpClient? secondClient = null;
        MultipartFormDataContent? firstContent = null;
        MultipartFormDataContent? secondContent = null;
        Task<HttpResponseMessage>? firstRequestTask = null;
        Task<HttpResponseMessage>? secondRequestTask = null;
        HttpResponseMessage? firstResponse = null;
        HttpResponseMessage? secondResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient =
                localFactory.CreateClient();

            (
                Guid listingId,
                AuthenticatedTestUser owner
            ) = await ListingTestHelpers
                .CreateListingWithOwnerAsync(
                    setupClient);

            List<ListingImage> seedImages =
                await SeedListingImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId,
                    count: 3,
                    cancellationToken);

            List<ListingImage> initialImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            initialImages.Should().HaveCount(3);

            initialImages.Select(image => image.SortOrder)
                .Should().Equal(0, 1, 2);

            initialImages.Count(image => image.IsPrimary)
                .Should().Be(1);

            string listingDirectory =
                GetConcurrencyListingDirectory(
                    storageRoot,
                    listingId);

            GetFinalFiles(listingDirectory)
                .Should().HaveCount(3);

            AssertNoTemporaryFiles(storageRoot);

            gateScope =
                localFactory.Services.CreateScope();

            observerScope =
                localFactory.Services.CreateScope();

            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            gateTransaction =
                await gateDbContext.Database
                    .BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);

            int gateBackendPid =
                await GetListingGateBackendPidAsync(
                    gateDbContext,
                    cancellationToken);

            firstClient = localFactory.CreateClient();
            secondClient = localFactory.CreateClient();

            firstClient.AuthorizeAs(owner.AccessToken);
            secondClient.AuthorizeAs(owner.AccessToken);

            firstContent =
                CreateConcurrencyUploadContent(
                    "append-first.jpg");

            secondContent =
                CreateConcurrencyUploadContent(
                    "append-second.jpg");

            firstRequestTask = firstClient.PostAsync(
                $"/api/listings/{listingId}/images",
                firstContent,
                cancellationToken);

            secondRequestTask = secondClient.PostAsync(
                $"/api/listings/{listingId}/images",
                secondContent,
                cancellationToken);

            await coordinator.WaitForTwoSavesAsync(
                cancellationToken);

            IReadOnlyList<ListingImageSaveRecord>
                gatedSaveRecords = coordinator.GetSaveRecords();

            gatedSaveRecords.Should().HaveCount(2);

            int firstRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: null,
                    cancellationToken);

            int secondRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid:
                        firstRequestBackendPid,
                    cancellationToken);

            secondRequestBackendPid.Should()
                .NotBe(firstRequestBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            gatedImages.Should().HaveCount(3);

            foreach (ListingImage seedImage in seedImages)
            {
                gatedImages.Should().ContainSingle(image =>
                    image.Id == seedImage.Id &&
                    image.StoredFileName ==
                        seedImage.StoredFileName &&
                    image.SortOrder == seedImage.SortOrder &&
                    image.IsPrimary == seedImage.IsPrimary);
            }

            string[] gatedFiles =
                GetFinalFiles(listingDirectory);

            gatedFiles.Should().HaveCount(5);

            string[] gatedFileNames =
                gatedFiles.Select(Path.GetFileName)
                    .ToArray()!;

            foreach (ListingImageSaveRecord saveRecord
                     in gatedSaveRecords)
            {
                gatedFileNames.Should().Contain(
                    saveRecord.StoredFile.StoredFileName);
            }

            AssertNoTemporaryFiles(storageRoot);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            firstResponse =
                await firstRequestTask.WaitAsync(
                    cancellationToken);

            secondResponse =
                await secondRequestTask.WaitAsync(
                    cancellationToken);

            firstResponse.StatusCode.Should()
                .Be(HttpStatusCode.Created);

            secondResponse.StatusCode.Should()
                .Be(HttpStatusCode.Created);

            ListingImageResponse? firstImage =
                await firstResponse.Content
                    .ReadFromJsonAsync<ListingImageResponse>(
                        cancellationToken);

            ListingImageResponse? secondImage =
                await secondResponse.Content
                    .ReadFromJsonAsync<ListingImageResponse>(
                        cancellationToken);

            firstImage.Should().NotBeNull();
            secondImage.Should().NotBeNull();

            ListingImageResponse[] responseImages =
            [
                firstImage!,
                secondImage!
            ];

            responseImages.Select(image => image.Id)
                .Should().NotContain(Guid.Empty);

            responseImages.Select(image => image.Id)
                .Should().OnlyHaveUniqueItems();

            responseImages.Select(image => image.SortOrder)
                .OrderBy(sortOrder => sortOrder)
                .Should().Equal(3, 4);

            responseImages.Should().OnlyContain(image =>
                !image.IsPrimary);

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            finalImages.Should().HaveCount(5);

            finalImages.Select(image => image.SortOrder)
                .OrderBy(sortOrder => sortOrder)
                .Should().Equal(0, 1, 2, 3, 4);

            finalImages.Select(image => image.SortOrder)
                .Should().OnlyHaveUniqueItems();

            finalImages.Count(image => image.IsPrimary)
                .Should().Be(1);

            finalImages.Single(image =>
                    image.SortOrder == 0)
                .IsPrimary.Should().BeTrue();

            foreach (ListingImage seedImage in seedImages)
            {
                finalImages.Should().ContainSingle(image =>
                    image.Id == seedImage.Id &&
                    image.StoredFileName ==
                        seedImage.StoredFileName &&
                    image.SortOrder == seedImage.SortOrder &&
                    image.IsPrimary == seedImage.IsPrimary);
            }

            foreach (ListingImageResponse responseImage
                     in responseImages)
            {
                finalImages.Should().ContainSingle(image =>
                    image.Id == responseImage.Id &&
                    image.SortOrder ==
                        responseImage.SortOrder &&
                    !image.IsPrimary);
            }

            IReadOnlyList<ListingImageSaveRecord> saveRecords =
                coordinator.GetSaveRecords();

            saveRecords.Should().HaveCount(2);

            foreach (ListingImageSaveRecord saveRecord
                     in saveRecords)
            {
                finalImages.Should().ContainSingle(image =>
                    image.StoredFileName ==
                    saveRecord.StoredFile.StoredFileName);
            }

            string[] finalFiles =
                GetFinalFiles(listingDirectory);

            finalFiles.Should().HaveCount(5);

            string[] finalFileNames =
                finalFiles.Select(Path.GetFileName)
                    .ToArray()!;

            foreach (ListingImage seedImage in seedImages)
            {
                finalFileNames.Should().Contain(
                    seedImage.StoredFileName);
            }

            foreach (ListingImageSaveRecord saveRecord
                     in saveRecords)
            {
                finalFileNames.Should().Contain(
                    saveRecord.StoredFile.StoredFileName);
            }

            coordinator.GetDeleteRecords()
                .Should().BeEmpty();

            AssertNoTemporaryFiles(storageRoot);

            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((
                    firstRequestTask is not null &&
                    !firstRequestTask.IsCompleted
                ) ||
                (
                    secondRequestTask is not null &&
                    !secondRequestTask.IsCompleted
                ))
            {
                timeoutSource.Cancel();
                firstClient?.CancelPendingRequests();
                secondClient?.CancelPendingRequests();
            }

            if (!gateReleased &&
                gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (firstResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(
                    firstRequestTask);
            }
            else
            {
                firstResponse.Dispose();
            }

            if (secondResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(
                    secondRequestTask);
            }
            else
            {
                secondResponse.Dispose();
            }

            firstContent?.Dispose();
            secondContent?.Dispose();
            firstClient?.Dispose();
            secondClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();

            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageUploadConcurrency_CancelledWhileWaitingForListingLock_CompensatesStoredFile()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        string connectionString = GetRequiredTestConnectionString();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            connectionString,
            storageRoot,
            coordinator);

        using var timeoutSource =
            new CancellationTokenSource(
                ListingImageConcurrencyTimeout);

        CancellationToken cancellationToken =
            timeoutSource.Token;

        using var requestCancellationSource =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        HttpClient? client = null;
        MultipartFormDataContent? content = null;
        Task<HttpResponseMessage>? requestTask = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient =
                localFactory.CreateClient();

            (
                Guid listingId,
                AuthenticatedTestUser owner
            ) = await ListingTestHelpers
                .CreateListingWithOwnerAsync(
                    setupClient);

            List<ListingImage> initialImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            initialImages.Should().BeEmpty();

            string listingDirectory =
                GetConcurrencyListingDirectory(
                    storageRoot,
                    listingId);

            GetFinalFiles(listingDirectory)
                .Should().BeEmpty();

            AssertNoTemporaryFiles(storageRoot);

            gateScope =
                localFactory.Services.CreateScope();

            observerScope =
                localFactory.Services.CreateScope();

            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            gateTransaction =
                await gateDbContext.Database
                    .BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);

            int gateBackendPid =
                await GetListingGateBackendPidAsync(
                    gateDbContext,
                    cancellationToken);

            client = localFactory.CreateClient();
            client.AuthorizeAs(owner.AccessToken);

            content =
                CreateConcurrencyUploadContent(
                    "cancelled-while-waiting.jpg");

            requestTask = client.PostAsync(
                $"/api/listings/{listingId}/images",
                content,
                requestCancellationSource.Token);

            await coordinator.WaitForOneSaveAsync(
                cancellationToken);

            IReadOnlyList<ListingImageSaveRecord> saveRecords =
                coordinator.GetSaveRecords();

            saveRecords.Should().ContainSingle();

            ListingImageSaveRecord saveRecord =
                saveRecords.Single();

            int requestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: null,
                    cancellationToken);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            gatedImages.Should().BeEmpty();

            string[] gatedFiles =
                GetFinalFiles(listingDirectory);

            gatedFiles.Should().ContainSingle();

            Path.GetFileName(gatedFiles.Single())
                .Should().Be(
                    saveRecord.StoredFile.StoredFileName);

            AssertNoTemporaryFiles(storageRoot);

            requestCancellationSource.Cancel();

            await coordinator.WaitForOneDeleteAsync(
                cancellationToken);

            Func<Task> observeRequestCancellation =
                async () =>
                {
                    using HttpResponseMessage
                        unexpectedResponse =
                            await requestTask.WaitAsync(
                                cancellationToken);
                };

            await observeRequestCancellation.Should()
                .ThrowAsync<OperationCanceledException>();

            await WaitForListingBackendToStopWaitingAsync(
                observerDbContext,
                requestBackendPid,
                cancellationToken);

            await gateTransaction.RollbackAsync(
                CancellationToken.None);

            gateReleased = true;

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);

            finalImages.Should().BeEmpty();

            GetFinalFiles(listingDirectory)
                .Should().BeEmpty();

            coordinator.GetSaveRecords()
                .Should().ContainSingle();

            IReadOnlyList<ListingImageDeleteRecord>
                deleteRecords = coordinator.GetDeleteRecords();

            deleteRecords.Should().ContainSingle();

            ListingImageDeleteRecord deleteRecord =
                deleteRecords.Single();

            deleteRecord.ListingId.Should()
                .Be(listingId);

            deleteRecord.StoredFileName.Should()
                .Be(saveRecord.StoredFile.StoredFileName);

            deleteRecord.CancellationToken.Should()
                .Be(CancellationToken.None);

            deleteRecord.CancellationToken.CanBeCanceled
                .Should().BeFalse();

            deleteRecord.CancellationToken.IsCancellationRequested
                .Should().BeFalse();

            AssertNoTemporaryFiles(storageRoot);

            testCompletedSuccessfully = true;
        }
        finally
        {
            if (requestTask is not null &&
                !requestTask.IsCompleted)
            {
                requestCancellationSource.Cancel();
                timeoutSource.Cancel();
                client?.CancelPendingRequests();
            }

            if (!gateReleased &&
                gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            await DrainAndDisposeUploadTaskAsync(
                requestTask);

            content?.Dispose();
            client?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();

            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageMutationConcurrency_SetPrimaryVersusSetPrimary_SerializesToOnePrimary()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            GetRequiredTestConnectionString(),
            storageRoot,
            coordinator);
        using var timeoutSource = new CancellationTokenSource(
            ListingImageConcurrencyTimeout);
        CancellationToken cancellationToken = timeoutSource.Token;
        HttpClient? firstClient = null;
        HttpClient? secondClient = null;
        Task<HttpResponseMessage>? firstRequestTask = null;
        Task<HttpResponseMessage>? secondRequestTask = null;
        HttpResponseMessage? firstResponse = null;
        HttpResponseMessage? secondResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient = localFactory.CreateClient();
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(
                    setupClient);
            List<ListingImage> seedImages =
                await SeedListingImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId,
                    count: 3,
                    cancellationToken);
            ListingImage imageA = seedImages[0];
            ListingImage imageB = seedImages[1];
            ListingImage imageC = seedImages[2];
            string listingDirectory = GetConcurrencyListingDirectory(
                storageRoot,
                listingId);

            gateScope = localFactory.Services.CreateScope();
            observerScope = localFactory.Services.CreateScope();
            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            gateTransaction = await gateDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);
            int gateBackendPid = await GetListingGateBackendPidAsync(
                gateDbContext,
                cancellationToken);

            firstClient = localFactory.CreateClient();
            secondClient = localFactory.CreateClient();
            firstClient.AuthorizeAs(owner.AccessToken);
            secondClient.AuthorizeAs(owner.AccessToken);

            firstRequestTask = firstClient.PutAsync(
                $"/api/listings/{listingId}/images/{imageB.Id}/primary",
                null,
                cancellationToken);
            int firstRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: null,
                    cancellationToken);

            secondRequestTask = secondClient.PutAsync(
                $"/api/listings/{listingId}/images/{imageC.Id}/primary",
                null,
                cancellationToken);
            int secondRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: firstRequestBackendPid,
                    cancellationToken);
            secondRequestBackendPid.Should().NotBe(firstRequestBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            gatedImages.Select(image => image.Id)
                .Should().Equal(seedImages.Select(image => image.Id));
            gatedImages.Single(image => image.Id == imageA.Id)
                .IsPrimary.Should().BeTrue();
            GetFinalFiles(listingDirectory).Should().HaveCount(3);
            coordinator.GetSaveRecords().Should().BeEmpty();
            coordinator.GetDeleteRecords().Should().BeEmpty();

            await gateTransaction.RollbackAsync(CancellationToken.None);
            gateReleased = true;

            firstResponse = await firstRequestTask.WaitAsync(cancellationToken);
            secondResponse = await secondRequestTask.WaitAsync(cancellationToken);
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingImageResponse? firstResult =
                await firstResponse.Content.ReadFromJsonAsync<ListingImageResponse>(
                    cancellationToken);
            ListingImageResponse? secondResult =
                await secondResponse.Content.ReadFromJsonAsync<ListingImageResponse>(
                    cancellationToken);
            firstResult.Should().NotBeNull();
            secondResult.Should().NotBeNull();
            firstResult!.Id.Should().Be(imageB.Id);
            firstResult.IsPrimary.Should().BeTrue();
            secondResult!.Id.Should().Be(imageC.Id);
            secondResult.IsPrimary.Should().BeTrue();

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            finalImages.Should().HaveCount(3);
            finalImages.Select(image => image.Id)
                .Should().Equal(imageA.Id, imageB.Id, imageC.Id);
            finalImages.Select(image => image.SortOrder)
                .Should().Equal(0, 1, 2);
            finalImages.Count(image => image.IsPrimary).Should().Be(1);
            finalImages.Single(image => image.Id == imageA.Id)
                .IsPrimary.Should().BeFalse();
            finalImages.Single(image => image.Id == imageB.Id)
                .IsPrimary.Should().BeFalse();
            finalImages.Single(image => image.Id == imageC.Id)
                .IsPrimary.Should().BeTrue();
            GetFinalFiles(listingDirectory).Select(Path.GetFileName)
                .Should().BeEquivalentTo(
                    seedImages.Select(image => image.StoredFileName));
            coordinator.GetSaveRecords().Should().BeEmpty();
            coordinator.GetDeleteRecords().Should().BeEmpty();
            AssertNoTemporaryFiles(storageRoot);
            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((firstRequestTask is not null && !firstRequestTask.IsCompleted) ||
                (secondRequestTask is not null && !secondRequestTask.IsCompleted))
            {
                timeoutSource.Cancel();
                firstClient?.CancelPendingRequests();
                secondClient?.CancelPendingRequests();
            }

            if (!gateReleased && gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (firstResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(firstRequestTask);
            }
            else
            {
                firstResponse.Dispose();
            }

            if (secondResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(secondRequestTask);
            }
            else
            {
                secondResponse.Dispose();
            }

            firstClient?.Dispose();
            secondClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch when (!testCompletedSuccessfully)
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();
            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageMutationConcurrency_SetPrimaryVersusDeleteTarget_LeavesOneValidPrimary()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            GetRequiredTestConnectionString(),
            storageRoot,
            coordinator);
        using var timeoutSource = new CancellationTokenSource(
            ListingImageConcurrencyTimeout);
        CancellationToken cancellationToken = timeoutSource.Token;
        HttpClient? firstClient = null;
        HttpClient? secondClient = null;
        Task<HttpResponseMessage>? firstRequestTask = null;
        Task<HttpResponseMessage>? secondRequestTask = null;
        HttpResponseMessage? firstResponse = null;
        HttpResponseMessage? secondResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient = localFactory.CreateClient();
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(
                    setupClient);
            List<ListingImage> seedImages =
                await SeedListingImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId,
                    count: 3,
                    cancellationToken);
            ListingImage imageA = seedImages[0];
            ListingImage imageB = seedImages[1];
            ListingImage imageC = seedImages[2];
            string listingDirectory = GetConcurrencyListingDirectory(
                storageRoot,
                listingId);

            gateScope = localFactory.Services.CreateScope();
            observerScope = localFactory.Services.CreateScope();
            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            gateTransaction = await gateDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);
            int gateBackendPid = await GetListingGateBackendPidAsync(
                gateDbContext,
                cancellationToken);

            firstClient = localFactory.CreateClient();
            secondClient = localFactory.CreateClient();
            firstClient.AuthorizeAs(owner.AccessToken);
            secondClient.AuthorizeAs(owner.AccessToken);

            firstRequestTask = firstClient.PutAsync(
                $"/api/listings/{listingId}/images/{imageB.Id}/primary",
                null,
                cancellationToken);
            int firstRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: null,
                    cancellationToken);

            secondRequestTask = secondClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageB.Id}",
                cancellationToken);
            int secondRequestBackendPid =
                await WaitForBlockedListingBackendAsync(
                    observerDbContext,
                    gateBackendPid,
                    excludedBackendPid: firstRequestBackendPid,
                    cancellationToken);
            secondRequestBackendPid.Should().NotBe(firstRequestBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            gatedImages.Should().HaveCount(3);
            gatedImages.Single(image => image.Id == imageA.Id)
                .IsPrimary.Should().BeTrue();
            GetFinalFiles(listingDirectory).Should().HaveCount(3);
            coordinator.GetDeleteRecords().Should().BeEmpty();

            await gateTransaction.RollbackAsync(CancellationToken.None);
            gateReleased = true;

            firstResponse = await firstRequestTask.WaitAsync(cancellationToken);
            secondResponse = await secondRequestTask.WaitAsync(cancellationToken);
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            ListingImageResponse? setPrimaryResult =
                await firstResponse.Content.ReadFromJsonAsync<ListingImageResponse>(
                    cancellationToken);
            setPrimaryResult.Should().NotBeNull();
            setPrimaryResult!.Id.Should().Be(imageB.Id);
            setPrimaryResult.IsPrimary.Should().BeTrue();

            await coordinator.WaitForOneDeleteAsync(cancellationToken);

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            finalImages.Should().HaveCount(2);
            finalImages.Select(image => image.Id)
                .Should().Equal(imageA.Id, imageC.Id);
            finalImages.Select(image => image.SortOrder)
                .Should().Equal(0, 2);
            finalImages.Count(image => image.IsPrimary).Should().Be(1);
            finalImages.Single(image => image.Id == imageA.Id)
                .IsPrimary.Should().BeTrue();
            finalImages.Single(image => image.Id == imageC.Id)
                .IsPrimary.Should().BeFalse();

            IReadOnlyList<ListingImageDeleteRecord> deleteRecords =
                coordinator.GetDeleteRecords();
            deleteRecords.Should().ContainSingle();
            deleteRecords[0].ListingId.Should().Be(listingId);
            deleteRecords[0].StoredFileName.Should()
                .Be(imageB.StoredFileName);
            deleteRecords[0].StoredFileName.Should()
                .NotBe(imageA.StoredFileName)
                .And.NotBe(imageC.StoredFileName);

            string[] finalFileNames = GetFinalFiles(listingDirectory)
                .Select(Path.GetFileName)
                .ToArray()!;
            finalFileNames.Should().BeEquivalentTo(
                imageA.StoredFileName,
                imageC.StoredFileName);
            coordinator.GetSaveRecords().Should().BeEmpty();
            AssertNoTemporaryFiles(storageRoot);
            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((firstRequestTask is not null && !firstRequestTask.IsCompleted) ||
                (secondRequestTask is not null && !secondRequestTask.IsCompleted))
            {
                timeoutSource.Cancel();
                firstClient?.CancelPendingRequests();
                secondClient?.CancelPendingRequests();
            }

            if (!gateReleased && gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (firstResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(firstRequestTask);
            }
            else
            {
                firstResponse.Dispose();
            }

            if (secondResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(secondRequestTask);
            }
            else
            {
                secondResponse.Dispose();
            }

            firstClient?.Dispose();
            secondClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch when (!testCompletedSuccessfully)
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();
            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageMutationConcurrency_DeleteVersusUpload_PreservesCapacityMembershipAndAppendOrder()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            GetRequiredTestConnectionString(),
            storageRoot,
            coordinator);
        using var timeoutSource = new CancellationTokenSource(
            ListingImageConcurrencyTimeout);
        CancellationToken cancellationToken = timeoutSource.Token;
        HttpClient? deleteClient = null;
        HttpClient? uploadClient = null;
        MultipartFormDataContent? uploadContent = null;
        Task<HttpResponseMessage>? deleteRequestTask = null;
        Task<HttpResponseMessage>? uploadRequestTask = null;
        HttpResponseMessage? deleteResponse = null;
        HttpResponseMessage? uploadResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient = localFactory.CreateClient();
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(
                    setupClient);
            List<ListingImage> seedImages =
                await SeedListingImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId,
                    count: 19,
                    cancellationToken);
            ListingImage deletedImage = seedImages.Single(
                image => image.SortOrder == 18);
            string listingDirectory = GetConcurrencyListingDirectory(
                storageRoot,
                listingId);

            gateScope = localFactory.Services.CreateScope();
            observerScope = localFactory.Services.CreateScope();
            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            gateTransaction = await gateDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);
            int gateBackendPid = await GetListingGateBackendPidAsync(
                gateDbContext,
                cancellationToken);

            deleteClient = localFactory.CreateClient();
            uploadClient = localFactory.CreateClient();
            deleteClient.AuthorizeAs(owner.AccessToken);
            uploadClient.AuthorizeAs(owner.AccessToken);

            deleteRequestTask = deleteClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{deletedImage.Id}",
                cancellationToken);
            int deleteBackendPid = await WaitForBlockedListingBackendAsync(
                observerDbContext,
                gateBackendPid,
                excludedBackendPid: null,
                cancellationToken);

            uploadContent = CreateConcurrencyUploadContent(
                "delete-versus-upload.jpg");
            uploadRequestTask = uploadClient.PostAsync(
                $"/api/listings/{listingId}/images",
                uploadContent,
                cancellationToken);
            await coordinator.WaitForOneSaveAsync(cancellationToken);
            ListingImageSaveRecord savedUpload =
                coordinator.GetSaveRecords().Single();
            int uploadBackendPid = await WaitForBlockedListingBackendAsync(
                observerDbContext,
                gateBackendPid,
                excludedBackendPid: deleteBackendPid,
                cancellationToken);
            uploadBackendPid.Should().NotBe(deleteBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            gatedImages.Should().HaveCount(19);
            gatedImages.Should().Contain(image => image.Id == deletedImage.Id);
            string[] gatedFileNames = GetFinalFiles(listingDirectory)
                .Select(Path.GetFileName)
                .ToArray()!;
            gatedFileNames.Should().HaveCount(20);
            gatedFileNames.Should().Contain(deletedImage.StoredFileName);
            gatedFileNames.Should().Contain(
                savedUpload.StoredFile.StoredFileName);
            coordinator.GetDeleteRecords().Should().BeEmpty();
            AssertNoTemporaryFiles(storageRoot);

            await gateTransaction.RollbackAsync(CancellationToken.None);
            gateReleased = true;

            deleteResponse = await deleteRequestTask.WaitAsync(cancellationToken);
            uploadResponse = await uploadRequestTask.WaitAsync(cancellationToken);
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
            uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            ListingImageResponse? uploadResult =
                await uploadResponse.Content.ReadFromJsonAsync<ListingImageResponse>(
                    cancellationToken);
            uploadResult.Should().NotBeNull();
            uploadResult!.Id.Should().NotBeEmpty();
            uploadResult.SortOrder.Should().Be(18);
            uploadResult.IsPrimary.Should().BeFalse();

            await coordinator.WaitForOneDeleteAsync(cancellationToken);

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            finalImages.Should().HaveCount(19);
            finalImages.Should().NotContain(image => image.Id == deletedImage.Id);
            foreach (ListingImage seedImage in seedImages
                         .Where(image => image.Id != deletedImage.Id))
            {
                finalImages.Should().ContainSingle(image =>
                    image.Id == seedImage.Id &&
                    image.StoredFileName == seedImage.StoredFileName);
            }

            finalImages.Should().ContainSingle(image =>
                image.Id == uploadResult.Id &&
                image.StoredFileName == savedUpload.StoredFile.StoredFileName &&
                image.SortOrder == 18 &&
                !image.IsPrimary);
            finalImages.Select(image => image.SortOrder)
                .Should().Equal(Enumerable.Range(0, 19));
            finalImages.Select(image => image.SortOrder)
                .Should().OnlyHaveUniqueItems();
            finalImages.Count(image => image.IsPrimary).Should().Be(1);
            finalImages.Single(image => image.SortOrder == 0)
                .IsPrimary.Should().BeTrue();

            IReadOnlyList<ListingImageDeleteRecord> deleteRecords =
                coordinator.GetDeleteRecords();
            deleteRecords.Should().ContainSingle();
            deleteRecords[0].ListingId.Should().Be(listingId);
            deleteRecords[0].StoredFileName.Should()
                .Be(deletedImage.StoredFileName);
            deleteRecords[0].StoredFileName.Should()
                .NotBe(savedUpload.StoredFile.StoredFileName);

            string[] finalFileNames = GetFinalFiles(listingDirectory)
                .Select(Path.GetFileName)
                .ToArray()!;
            finalFileNames.Should().HaveCount(19);
            finalFileNames.Should().NotContain(deletedImage.StoredFileName);
            finalFileNames.Should().Contain(
                savedUpload.StoredFile.StoredFileName);
            foreach (ListingImage seedImage in seedImages
                         .Where(image => image.Id != deletedImage.Id))
            {
                finalFileNames.Should().Contain(seedImage.StoredFileName);
            }

            coordinator.GetSaveRecords().Should().ContainSingle();
            AssertNoTemporaryFiles(storageRoot);
            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((deleteRequestTask is not null && !deleteRequestTask.IsCompleted) ||
                (uploadRequestTask is not null && !uploadRequestTask.IsCompleted))
            {
                timeoutSource.Cancel();
                deleteClient?.CancelPendingRequests();
                uploadClient?.CancelPendingRequests();
            }

            if (!gateReleased && gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (deleteResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(deleteRequestTask);
            }
            else
            {
                deleteResponse.Dispose();
            }

            if (uploadResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(uploadRequestTask);
            }
            else
            {
                uploadResponse.Dispose();
            }

            uploadContent?.Dispose();
            deleteClient?.Dispose();
            uploadClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch when (!testCompletedSuccessfully)
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();
            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImageMutationConcurrency_ReorderVersusUpload_PreservesCompleteSetAndAppendOrder()
    {
        string storageRoot = CreateConcurrencyStorageRoot();
        var coordinator = new ListingImageStorageCoordinator();
        var localFactory = new ListingImageConcurrencyWebApplicationFactory(
            GetRequiredTestConnectionString(),
            storageRoot,
            coordinator);
        using var timeoutSource = new CancellationTokenSource(
            ListingImageConcurrencyTimeout);
        CancellationToken cancellationToken = timeoutSource.Token;
        HttpClient? reorderClient = null;
        HttpClient? uploadClient = null;
        MultipartFormDataContent? uploadContent = null;
        Task<HttpResponseMessage>? reorderRequestTask = null;
        Task<HttpResponseMessage>? uploadRequestTask = null;
        HttpResponseMessage? reorderResponse = null;
        HttpResponseMessage? uploadResponse = null;
        IServiceScope? gateScope = null;
        IServiceScope? observerScope = null;
        IDbContextTransaction? gateTransaction = null;
        bool gateReleased = false;
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient setupClient = localFactory.CreateClient();
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(
                    setupClient);
            List<ListingImage> seedImages =
                await SeedListingImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId,
                    count: 3,
                    cancellationToken);
            ListingImage imageA = seedImages[0];
            ListingImage imageB = seedImages[1];
            ListingImage imageC = seedImages[2];
            Guid originalPrimaryId = imageA.Id;
            Guid[] requestedOrder = [imageC.Id, imageA.Id, imageB.Id];
            string listingDirectory = GetConcurrencyListingDirectory(
                storageRoot,
                listingId);

            gateScope = localFactory.Services.CreateScope();
            observerScope = localFactory.Services.CreateScope();
            RealEstateDbContext gateDbContext =
                gateScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            gateTransaction = await gateDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await LockListingAsync(
                gateDbContext,
                listingId,
                cancellationToken);
            int gateBackendPid = await GetListingGateBackendPidAsync(
                gateDbContext,
                cancellationToken);

            reorderClient = localFactory.CreateClient();
            uploadClient = localFactory.CreateClient();
            reorderClient.AuthorizeAs(owner.AccessToken);
            uploadClient.AuthorizeAs(owner.AccessToken);

            reorderRequestTask = reorderClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                new { imageIds = requestedOrder },
                cancellationToken);
            int reorderBackendPid = await WaitForBlockedListingBackendAsync(
                observerDbContext,
                gateBackendPid,
                excludedBackendPid: null,
                cancellationToken);

            uploadContent = CreateConcurrencyUploadContent(
                "reorder-versus-upload.jpg");
            uploadRequestTask = uploadClient.PostAsync(
                $"/api/listings/{listingId}/images",
                uploadContent,
                cancellationToken);
            await coordinator.WaitForOneSaveAsync(cancellationToken);
            ListingImageSaveRecord savedUpload =
                coordinator.GetSaveRecords().Single();
            int uploadBackendPid = await WaitForBlockedListingBackendAsync(
                observerDbContext,
                gateBackendPid,
                excludedBackendPid: reorderBackendPid,
                cancellationToken);
            uploadBackendPid.Should().NotBe(reorderBackendPid);

            List<ListingImage> gatedImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            gatedImages.Select(image => image.Id)
                .Should().Equal(imageA.Id, imageB.Id, imageC.Id);
            gatedImages.Select(image => image.SortOrder)
                .Should().Equal(0, 1, 2);
            string[] gatedFileNames = GetFinalFiles(listingDirectory)
                .Select(Path.GetFileName)
                .ToArray()!;
            gatedFileNames.Should().HaveCount(4);
            gatedFileNames.Should().Contain(
                savedUpload.StoredFile.StoredFileName);
            coordinator.GetDeleteRecords().Should().BeEmpty();
            AssertNoTemporaryFiles(storageRoot);

            await gateTransaction.RollbackAsync(CancellationToken.None);
            gateReleased = true;

            reorderResponse = await reorderRequestTask.WaitAsync(cancellationToken);
            uploadResponse = await uploadRequestTask.WaitAsync(cancellationToken);
            reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            List<ListingImageResponse>? reorderResult =
                await reorderResponse.Content.ReadFromJsonAsync<
                    List<ListingImageResponse>>(cancellationToken);
            ListingImageResponse? uploadResult =
                await uploadResponse.Content.ReadFromJsonAsync<ListingImageResponse>(
                    cancellationToken);
            reorderResult.Should().NotBeNull();
            reorderResult!.Select(image => image.Id)
                .Should().Equal(requestedOrder);
            reorderResult.Select(image => image.SortOrder)
                .Should().Equal(0, 1, 2);
            uploadResult.Should().NotBeNull();
            uploadResult!.SortOrder.Should().Be(3);
            uploadResult.IsPrimary.Should().BeFalse();

            List<ListingImage> finalImages =
                await ReadCommittedListingImagesAsync(
                    localFactory,
                    listingId,
                    cancellationToken);
            finalImages.Should().HaveCount(4);
            finalImages.Select(image => image.Id)
                .Should().BeEquivalentTo(
                [
                    imageA.Id,
                    imageB.Id,
                    imageC.Id,
                    uploadResult.Id
                ]);
            finalImages.Select(image => image.SortOrder)
                .Should().Equal(0, 1, 2, 3);
            finalImages.Select(image => image.SortOrder)
                .Should().OnlyHaveUniqueItems();
            finalImages.Single(image => image.Id == imageC.Id)
                .SortOrder.Should().Be(0);
            finalImages.Single(image => image.Id == imageA.Id)
                .SortOrder.Should().Be(1);
            finalImages.Single(image => image.Id == imageB.Id)
                .SortOrder.Should().Be(2);
            finalImages.Single(image => image.Id == uploadResult.Id)
                .SortOrder.Should().Be(3);
            finalImages.Single(image => image.Id == uploadResult.Id)
                .StoredFileName.Should()
                .Be(savedUpload.StoredFile.StoredFileName);
            finalImages.Count(image => image.IsPrimary).Should().Be(1);
            finalImages.Single(image => image.IsPrimary).Id
                .Should().Be(originalPrimaryId);
            finalImages.Single(image => image.Id == uploadResult.Id)
                .IsPrimary.Should().BeFalse();

            string[] finalFileNames = GetFinalFiles(listingDirectory)
                .Select(Path.GetFileName)
                .ToArray()!;
            finalFileNames.Should().HaveCount(4);
            foreach (ListingImage seedImage in seedImages)
            {
                finalFileNames.Should().Contain(seedImage.StoredFileName);
            }
            finalFileNames.Should().Contain(
                savedUpload.StoredFile.StoredFileName);
            coordinator.GetSaveRecords().Should().ContainSingle();
            coordinator.GetDeleteRecords().Should().BeEmpty();
            AssertNoTemporaryFiles(storageRoot);
            testCompletedSuccessfully = true;
        }
        finally
        {
            if ((reorderRequestTask is not null && !reorderRequestTask.IsCompleted) ||
                (uploadRequestTask is not null && !uploadRequestTask.IsCompleted))
            {
                timeoutSource.Cancel();
                reorderClient?.CancelPendingRequests();
                uploadClient?.CancelPendingRequests();
            }

            if (!gateReleased && gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (reorderResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(reorderRequestTask);
            }
            else
            {
                reorderResponse.Dispose();
            }

            if (uploadResponse is null)
            {
                await DrainAndDisposeUploadTaskAsync(uploadRequestTask);
            }
            else
            {
                uploadResponse.Dispose();
            }

            uploadContent?.Dispose();
            reorderClient?.Dispose();
            uploadClient?.Dispose();

            if (gateTransaction is not null)
            {
                try
                {
                    await gateTransaction.DisposeAsync();
                }
                catch when (!testCompletedSuccessfully)
                {
                    // Preserve the original test failure.
                }
            }

            observerScope?.Dispose();
            gateScope?.Dispose();
            localFactory.Dispose();
            DeleteConcurrencyStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    private static string CreateConcurrencyStorageRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "realestate-tests",
            "listing-image-upload-concurrency",
            Guid.NewGuid().ToString("N"));
    }

    private string GetRequiredTestConnectionString()
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        string? connectionString =
            dbContext.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The initialized PostgreSQL test connection string is unavailable.");
        }

        return connectionString;
    }

    private static MultipartFormDataContent
        CreateConcurrencyUploadContent(
            string fileName)
    {
        var content = new MultipartFormDataContent();

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0,
            0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01
        ];

        var fileContent =
            new ByteArrayContent(imageBytes);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                "image/jpeg");

        content.Add(
            fileContent,
            "file",
            fileName);

        return content;
    }

    private static async Task<List<ListingImage>>
        SeedListingImagesAsync(
            WebApplicationFactory<Program> factory,
            string storageRoot,
            Guid listingId,
            int count,
            CancellationToken cancellationToken)
    {
        string listingDirectory =
            GetConcurrencyListingDirectory(
                storageRoot,
                listingId);

        Directory.CreateDirectory(
            listingDirectory);

        byte[] seedBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0
        ];

        var images = new List<ListingImage>(count);

        for (int sortOrder = 0;
             sortOrder < count;
             sortOrder++)
        {
            string storedFileName =
                $"seed-{sortOrder:D2}.jpg";

            var image = new ListingImage
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                OriginalFileName = storedFileName,
                StoredFileName = storedFileName,
                ContentType = "image/jpeg",
                SizeBytes = seedBytes.LongLength,
                Url =
                    $"/uploads/listings/{listingId}/" +
                    storedFileName,
                SortOrder = sortOrder,
                IsPrimary = sortOrder == 0
            };

            images.Add(image);

            await File.WriteAllBytesAsync(
                Path.Combine(
                    listingDirectory,
                    storedFileName),
                seedBytes,
                cancellationToken);
        }

        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        dbContext.Set<ListingImage>()
            .AddRange(images);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return images;
    }

    private static async Task<List<ListingImage>>
        ReadCommittedListingImagesAsync(
            WebApplicationFactory<Program> factory,
            Guid listingId,
            CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingImage>()
            .AsNoTracking()
            .Where(image =>
                image.ListingId == listingId)
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .ToListAsync(cancellationToken);
    }

    private static string GetConcurrencyListingDirectory(
        string storageRoot,
        Guid listingId)
    {
        return Path.Combine(
            storageRoot,
            "listings",
            listingId.ToString());
    }

    private static string[] GetFinalFiles(
        string listingDirectory)
    {
        if (!Directory.Exists(listingDirectory))
        {
            return [];
        }

        return Directory.GetFiles(
                listingDirectory,
                "*",
                SearchOption.TopDirectoryOnly)
            .OrderBy(
                path => path,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertNoTemporaryFiles(
        string storageRoot)
    {
        string[] temporaryFiles =
            Directory.Exists(storageRoot)
                ? Directory.GetFiles(
                    storageRoot,
                    "*.tmp",
                    SearchOption.AllDirectories)
                : [];

        temporaryFiles.Should().BeEmpty();
    }

    private static async Task LockListingAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        await EnsureListingConnectionOpenAsync(
            dbContext,
            cancellationToken);

        DbConnection connection =
            dbContext.Database.GetDbConnection();

        await using DbCommand command =
            connection.CreateCommand();

        command.Transaction =
            dbContext.Database.CurrentTransaction?
                .GetDbTransaction();

        command.CommandTimeout = 2;
        command.CommandText =
            """
            SELECT "Id"
            FROM "Listings"
            WHERE "Id" = @listingId
            FOR UPDATE;
            """;

        AddListingConcurrencyParameter(
            command,
            "listingId",
            DbType.Guid,
            listingId);

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        result.Should().NotBeNull();
        ((Guid)result!).Should().Be(listingId);
    }

    private static async Task<int>
        GetListingGateBackendPidAsync(
            RealEstateDbContext dbContext,
            CancellationToken cancellationToken)
    {
        await EnsureListingConnectionOpenAsync(
            dbContext,
            cancellationToken);

        DbConnection connection =
            dbContext.Database.GetDbConnection();

        await using DbCommand command =
            connection.CreateCommand();

        command.Transaction =
            dbContext.Database.CurrentTransaction?
                .GetDbTransaction();

        command.CommandTimeout = 2;
        command.CommandText =
            "SELECT pg_backend_pid();";

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (result is null || result is DBNull)
        {
            throw new InvalidOperationException(
                "The listing-image gate backend PID could not be resolved.");
        }

        return Convert.ToInt32(result);
    }

    private static async Task<int>
        WaitForBlockedListingBackendAsync(
            RealEstateDbContext observerDbContext,
            int blockingBackendPid,
            int? excludedBackendPid,
            CancellationToken cancellationToken)
    {
        await EnsureListingConnectionOpenAsync(
            observerDbContext,
            cancellationToken);

        DbConnection connection =
            observerDbContext.Database
                .GetDbConnection();

        while (true)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await using DbCommand command =
                connection.CreateCommand();

            command.CommandTimeout = 2;
            command.CommandText =
                """
                WITH RECURSIVE request_activity AS (
                    SELECT
                        activity.pid,
                        activity.query_start
                    FROM pg_stat_activity AS activity
                    WHERE activity.datname = current_database()
                      AND activity.pid <> pg_backend_pid()
                      AND activity.state = 'active'
                      AND activity.wait_event_type = 'Lock'
                      AND activity.query ILIKE '%FROM "Listings"%'
                      AND activity.query ILIKE '%FOR UPDATE%'
                      AND (
                          @excludedBackendPid IS NULL
                          OR activity.pid <> @excludedBackendPid
                      )
                ),
                blocker_chain AS (
                    SELECT
                        request_activity.pid AS request_pid,
                        blockers.blocker_pid
                    FROM request_activity
                    CROSS JOIN LATERAL unnest(
                        pg_blocking_pids(
                            request_activity.pid))
                        AS blockers(blocker_pid)

                    UNION

                    SELECT
                        blocker_chain.request_pid,
                        blockers.blocker_pid
                    FROM blocker_chain
                    CROSS JOIN LATERAL unnest(
                        pg_blocking_pids(
                            blocker_chain.blocker_pid))
                        AS blockers(blocker_pid)
                )
                SELECT request_activity.pid
                FROM request_activity
                WHERE EXISTS (
                    SELECT 1
                    FROM blocker_chain
                    WHERE blocker_chain.request_pid =
                              request_activity.pid
                      AND blocker_chain.blocker_pid =
                              @blockingBackendPid
                )
                ORDER BY request_activity.query_start
                LIMIT 1;
                """;

            object excludedBackendPidValue =
                excludedBackendPid.HasValue
                    ? excludedBackendPid.Value
                    : DBNull.Value;

            AddListingConcurrencyParameter(
                command,
                "excludedBackendPid",
                DbType.Int32,
                excludedBackendPidValue);

            AddListingConcurrencyParameter(
                command,
                "blockingBackendPid",
                DbType.Int32,
                blockingBackendPid);

            object? result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (result is not null &&
                result is not DBNull)
            {
                return Convert.ToInt32(result);
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                cancellationToken);
        }
    }

    private static async Task
        WaitForListingBackendToStopWaitingAsync(
            RealEstateDbContext observerDbContext,
            int requestBackendPid,
            CancellationToken cancellationToken)
    {
        await EnsureListingConnectionOpenAsync(
            observerDbContext,
            cancellationToken);

        DbConnection connection =
            observerDbContext.Database
                .GetDbConnection();

        while (true)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await using DbCommand command =
                connection.CreateCommand();

            command.CommandTimeout = 2;
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity AS activity
                    WHERE activity.datname = current_database()
                      AND activity.pid = @requestBackendPid
                      AND activity.state = 'active'
                      AND activity.wait_event_type = 'Lock'
                      AND activity.query ILIKE '%FROM "Listings"%'
                      AND activity.query ILIKE '%FOR UPDATE%'
                );
                """;

            AddListingConcurrencyParameter(
                command,
                "requestBackendPid",
                DbType.Int32,
                requestBackendPid);

            object? result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            bool isStillWaiting =
                result is not null &&
                result is not DBNull &&
                Convert.ToBoolean(result);

            if (!isStillWaiting)
            {
                return;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                cancellationToken);
        }
    }

    private static async Task EnsureListingConnectionOpenAsync(
        RealEstateDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.GetDbConnection().State !=
            ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(
                cancellationToken);
        }
    }

    private static void AddListingConcurrencyParameter(
        DbCommand command,
        string parameterName,
        DbType dbType,
        object value)
    {
        DbParameter parameter =
            command.CreateParameter();

        parameter.ParameterName = parameterName;
        parameter.DbType = dbType;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }

    private static async Task DrainAndDisposeUploadTaskAsync(
        Task<HttpResponseMessage>? responseTask)
    {
        if (responseTask is null)
        {
            return;
        }

        try
        {
            HttpResponseMessage response =
                await responseTask.WaitAsync(
                    TimeSpan.FromSeconds(5));

            response.Dispose();
        }
        catch (TimeoutException timeoutException)
            when (!responseTask.IsCompleted)
        {
            throw new InvalidOperationException(
                "A listing-image upload request did not terminate after cancellation.",
                timeoutException);
        }
        catch (OperationCanceledException)
        {
            // The intentionally cancelled request has terminated.
        }
        catch when (responseTask.IsCompleted)
        {
            // Observe an already-completed request fault without
            // replacing the original orchestration failure.
        }
    }

    private static void DeleteConcurrencyStorageRoot(
        string storageRoot,
        bool testCompletedSuccessfully)
    {
        if (!Directory.Exists(storageRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(
                storageRoot,
                recursive: true);
        }
        catch when (!testCompletedSuccessfully)
        {
            // Failure-path cleanup must not replace
            // the original test failure.
        }
    }

    private sealed class ListingImageConcurrencyWebApplicationFactory(
        string connectionString,
        string storageRoot,
        ListingImageStorageCoordinator coordinator)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                connectionString);

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] =
                                connectionString
                        });
                });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<
                    DbContextOptions<RealEstateDbContext>>();

                services.RemoveAll<RealEstateDbContext>();

                services.AddDbContext<RealEstateDbContext>(
                    options =>
                        options.UseNpgsql(
                            connectionString));

                services.PostConfigure<LocalFileStorageOptions>(
                    options =>
                    {
                        options.RootPath = storageRoot;
                        options.PublicBasePath = "/uploads";
                    });

                services.RemoveAll<IFileStorageService>();

                services.AddScoped<LocalFileStorageService>();

                services.AddScoped<IFileStorageService>(
                    provider =>
                        new RecordingListingImageStorage(
                            provider.GetRequiredService<
                                LocalFileStorageService>(),
                            coordinator));
            });
        }
    }

    private sealed class RecordingListingImageStorage(
        LocalFileStorageService inner,
        ListingImageStorageCoordinator coordinator)
        : IFileStorageService
    {
        public async Task<StoredFileResult>
            SaveListingImageAsync(
                Guid listingId,
                UploadedFile file,
                CancellationToken cancellationToken)
        {
            StoredFileResult storedFile =
                await inner.SaveListingImageAsync(
                    listingId,
                    file,
                    cancellationToken);

            coordinator.RecordSave(
                new ListingImageSaveRecord(
                    listingId,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    storedFile));

            return storedFile;
        }

        public async Task DeleteListingImageAsync(
            Guid listingId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            coordinator.RecordDeleteStarted(
                new ListingImageDeleteRecord(
                    listingId,
                    storedFileName,
                    cancellationToken));

            await inner.DeleteListingImageAsync(
                listingId,
                storedFileName,
                cancellationToken);

            coordinator.RecordDeleteCompleted();
        }

        public Task<StoredFileResult> SaveUserAvatarAsync(
            Guid userId,
            UploadedFile file,
            CancellationToken cancellationToken)
        {
            return inner.SaveUserAvatarAsync(
                userId,
                file,
                cancellationToken);
        }

        public Task DeleteUserAvatarAsync(
            Guid userId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            return inner.DeleteUserAvatarAsync(
                userId,
                storedFileName,
                cancellationToken);
        }

        public Task<StoredFileResult> SaveAgencyLogoAsync(
            Guid agencyId,
            UploadedFile file,
            CancellationToken cancellationToken)
        {
            return inner.SaveAgencyLogoAsync(
                agencyId,
                file,
                cancellationToken);
        }

        public Task DeleteAgencyLogoAsync(
            Guid agencyId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            return inner.DeleteAgencyLogoAsync(
                agencyId,
                storedFileName,
                cancellationToken);
        }
    }

    private sealed class ListingImageStorageCoordinator
    {
        private readonly object _sync = new();

        private readonly List<ListingImageSaveRecord>
            _saveRecords = [];

        private readonly List<ListingImageDeleteRecord>
            _deleteRecords = [];

        private readonly TaskCompletionSource<bool>
            _oneSaveCompleted = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _twoSavesCompleted = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _oneDeleteCompleted = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        public void RecordSave(
            ListingImageSaveRecord record)
        {
            lock (_sync)
            {
                if (_saveRecords.Count >= 2)
                {
                    throw new InvalidOperationException(
                        "Unexpected third listing-image save.");
                }

                _saveRecords.Add(record);

                if (_saveRecords.Count == 1)
                {
                    _oneSaveCompleted.TrySetResult(true);
                }

                if (_saveRecords.Count == 2)
                {
                    _twoSavesCompleted.TrySetResult(true);
                }
            }
        }

        public void RecordDeleteStarted(
            ListingImageDeleteRecord record)
        {
            lock (_sync)
            {
                if (_deleteRecords.Count >= 1)
                {
                    throw new InvalidOperationException(
                        "Unexpected second listing-image delete.");
                }

                _deleteRecords.Add(record);
            }
        }

        public void RecordDeleteCompleted()
        {
            lock (_sync)
            {
                if (_deleteRecords.Count != 1)
                {
                    throw new InvalidOperationException(
                        "A listing-image delete completed without its start record.");
                }

                _oneDeleteCompleted.TrySetResult(true);
            }
        }

        public async Task WaitForOneSaveAsync(
            CancellationToken cancellationToken)
        {
            await _oneSaveCompleted.Task.WaitAsync(
                cancellationToken);
        }

        public async Task WaitForTwoSavesAsync(
            CancellationToken cancellationToken)
        {
            await _twoSavesCompleted.Task.WaitAsync(
                cancellationToken);
        }

        public async Task WaitForOneDeleteAsync(
            CancellationToken cancellationToken)
        {
            await _oneDeleteCompleted.Task.WaitAsync(
                cancellationToken);
        }

        public IReadOnlyList<ListingImageSaveRecord>
            GetSaveRecords()
        {
            lock (_sync)
            {
                return _saveRecords.ToArray();
            }
        }

        public IReadOnlyList<ListingImageDeleteRecord>
            GetDeleteRecords()
        {
            lock (_sync)
            {
                return _deleteRecords.ToArray();
            }
        }
    }

    private sealed record ListingImageSaveRecord(
        Guid ListingId,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        StoredFileResult StoredFile);

    private sealed record ListingImageDeleteRecord(
        Guid ListingId,
        string StoredFileName,
        CancellationToken CancellationToken);
}
