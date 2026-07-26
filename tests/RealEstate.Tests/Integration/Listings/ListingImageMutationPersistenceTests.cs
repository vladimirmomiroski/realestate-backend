using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Infrastructure.Storage;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingImageMutationPersistenceTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string PrimaryConstraintName =
        "IX_ListingImages_ListingId";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingImageMutationPersistenceTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task SetPrimaryListingImage_WhenSecondSaveFails_RollsBackPrimaryChange()
    {
        string storageRoot = CreatePersistenceStorageRoot();
        var injectedFailure = new ListingImageSecondSaveException(
            "Injected set-primary second-save failure.");
        var failurePlan = new ListingImageSaveFailurePlan(
            throwOnSaveCall: 2,
            injectedFailure);
        var storageRecorder = new ListingImageDeleteRecorder();
        var localFactory = new ListingImageMutationWebApplicationFactory(
            GetRequiredTestConnectionString(),
            storageRoot,
            failurePlan,
            storageRecorder);
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient client = localFactory.CreateClient();

            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(client);

            List<ListingImage> images =
                await SeedPersistenceImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId);

            ListingImage originalPrimary = images[0];
            ListingImage requestedPrimary = images[1];

            client.AuthorizeAs(owner.AccessToken);

            Func<Task> act = async () =>
            {
                using HttpResponseMessage response =
                    await client.PutAsync(
                        $"/api/listings/{listingId}/images/{requestedPrimary.Id}/primary",
                        null);
            };

            ListingImageSecondSaveException thrownException =
                (await act.Should()
                    .ThrowAsync<ListingImageSecondSaveException>())
                .Which;

            thrownException.Should().BeSameAs(injectedFailure);
            failurePlan.SaveCallCount.Should().Be(2);

            List<ListingImage> savedImages =
                await ReadPersistenceImagesAsync(
                    localFactory,
                    listingId);

            savedImages.Should().HaveCount(2);
            savedImages.Select(image => image.Id)
                .Should().Equal(
                    originalPrimary.Id,
                    requestedPrimary.Id);
            savedImages.Select(image => image.SortOrder)
                .Should().Equal(0, 1);
            savedImages.Count(image => image.IsPrimary)
                .Should().Be(1);
            savedImages.Single(image => image.Id == originalPrimary.Id)
                .IsPrimary.Should().BeTrue();
            savedImages.Single(image => image.Id == requestedPrimary.Id)
                .IsPrimary.Should().BeFalse();
            storageRecorder.GetDeleteCalls().Should().BeEmpty();

            testCompletedSuccessfully = true;
        }
        finally
        {
            try
            {
                localFactory.Dispose();
            }
            catch when (!testCompletedSuccessfully)
            {
                // Preserve the original test failure.
            }
            DeletePersistenceStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task DeleteListingImage_WhenPrimaryPromotionSecondSaveFails_RollsBackDeletionAndPromotion()
    {
        string storageRoot = CreatePersistenceStorageRoot();
        var injectedFailure = new ListingImageSecondSaveException(
            "Injected primary-delete second-save failure.");
        var failurePlan = new ListingImageSaveFailurePlan(
            throwOnSaveCall: 2,
            injectedFailure);
        var storageRecorder = new ListingImageDeleteRecorder();
        var localFactory = new ListingImageMutationWebApplicationFactory(
            GetRequiredTestConnectionString(),
            storageRoot,
            failurePlan,
            storageRecorder);
        bool testCompletedSuccessfully = false;

        try
        {
            using HttpClient client = localFactory.CreateClient();

            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(client);

            List<ListingImage> images =
                await SeedPersistenceImagesAsync(
                    localFactory,
                    storageRoot,
                    listingId);

            ListingImage originalPrimary = images[0];
            ListingImage remainingImage = images[1];

            string originalPrimaryPath = Path.Combine(
                GetPersistenceListingDirectory(storageRoot, listingId),
                originalPrimary.StoredFileName);

            client.AuthorizeAs(owner.AccessToken);

            Func<Task> act = async () =>
            {
                using HttpResponseMessage response =
                    await client.DeleteAsync(
                        $"/api/listings/{listingId}/images/{originalPrimary.Id}");
            };

            ListingImageSecondSaveException thrownException =
                (await act.Should()
                    .ThrowAsync<ListingImageSecondSaveException>())
                .Which;

            thrownException.Should().BeSameAs(injectedFailure);
            failurePlan.SaveCallCount.Should().Be(2);

            List<ListingImage> savedImages =
                await ReadPersistenceImagesAsync(
                    localFactory,
                    listingId);

            savedImages.Should().HaveCount(2);
            savedImages.Select(image => image.Id)
                .Should().Equal(
                    originalPrimary.Id,
                    remainingImage.Id);
            savedImages.Select(image => image.SortOrder)
                .Should().Equal(0, 1);
            savedImages.Count(image => image.IsPrimary)
                .Should().Be(1);
            savedImages.Single(image => image.Id == originalPrimary.Id)
                .IsPrimary.Should().BeTrue();
            savedImages.Single(image => image.Id == remainingImage.Id)
                .IsPrimary.Should().BeFalse();

            storageRecorder.GetDeleteCalls().Should().BeEmpty();
            File.Exists(originalPrimaryPath).Should().BeTrue();
            Directory.GetFiles(
                    GetPersistenceListingDirectory(storageRoot, listingId))
                .Select(Path.GetFileName)
                .Should().BeEquivalentTo(
                    images.Select(image => image.StoredFileName));

            testCompletedSuccessfully = true;
        }
        finally
        {
            try
            {
                localFactory.Dispose();
            }
            catch when (!testCompletedSuccessfully)
            {
                // Preserve the original test failure.
            }
            DeletePersistenceStorageRoot(
                storageRoot,
                testCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task ListingImagePrimaryConstraint_TwoPrimariesForOneListing_RejectsNamedUniqueIndex()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        dbContext.Set<ListingImage>().Add(
            CreatePersistenceImage(
                listingId,
                "constraint-primary-a.jpg",
                sortOrder: 0,
                isPrimary: true));

        await dbContext.SaveChangesAsync();

        dbContext.Set<ListingImage>().Add(
            CreatePersistenceImage(
                listingId,
                "constraint-primary-b.jpg",
                sortOrder: 1,
                isPrimary: true));

        Func<Task> act = async () =>
            await dbContext.SaveChangesAsync();

        DbUpdateException dbUpdateException =
            (await act.Should().ThrowAsync<DbUpdateException>())
            .Which;

        PostgresException postgresException =
            dbUpdateException.InnerException.Should()
                .BeOfType<PostgresException>()
                .Subject;

        postgresException.SqlState.Should()
            .Be(PostgresErrorCodes.UniqueViolation);
        postgresException.ConstraintName.Should()
            .Be(PrimaryConstraintName);

        dbContext.ChangeTracker.Clear();
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

    private static string CreatePersistenceStorageRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "realestate-tests",
            "listing-image-mutation-persistence",
            Guid.NewGuid().ToString("N"));
    }

    private static async Task<List<ListingImage>>
        SeedPersistenceImagesAsync(
            WebApplicationFactory<Program> factory,
            string storageRoot,
            Guid listingId)
    {
        string listingDirectory =
            GetPersistenceListingDirectory(
                storageRoot,
                listingId);

        Directory.CreateDirectory(listingDirectory);

        List<ListingImage> images =
        [
            CreatePersistenceImage(
                listingId,
                "primary.jpg",
                sortOrder: 0,
                isPrimary: true),
            CreatePersistenceImage(
                listingId,
                "secondary.jpg",
                sortOrder: 1,
                isPrimary: false)
        ];

        foreach (ListingImage image in images)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(
                    listingDirectory,
                    image.StoredFileName),
                [0xFF, 0xD8, 0xFF, 0xE0]);
        }

        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        dbContext.Set<ListingImage>().AddRange(images);
        await dbContext.SaveChangesAsync();

        return images;
    }

    private static ListingImage CreatePersistenceImage(
        Guid listingId,
        string storedFileName,
        int sortOrder,
        bool isPrimary)
    {
        return new ListingImage
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            OriginalFileName = storedFileName,
            StoredFileName = storedFileName,
            ContentType = "image/jpeg",
            SizeBytes = 4,
            Url = $"/uploads/listings/{listingId}/{storedFileName}",
            SortOrder = sortOrder,
            IsPrimary = isPrimary
        };
    }

    private static async Task<List<ListingImage>>
        ReadPersistenceImagesAsync(
            WebApplicationFactory<Program> factory,
            Guid listingId)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingImage>()
            .AsNoTracking()
            .Where(image => image.ListingId == listingId)
            .OrderBy(image => image.SortOrder)
            .ToListAsync();
    }

    private static string GetPersistenceListingDirectory(
        string storageRoot,
        Guid listingId)
    {
        return Path.Combine(
            storageRoot,
            "listings",
            listingId.ToString());
    }

    private static void DeletePersistenceStorageRoot(
        string storageRoot,
        bool testCompletedSuccessfully)
    {
        if (!Directory.Exists(storageRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(storageRoot, recursive: true);
        }
        catch when (!testCompletedSuccessfully)
        {
            // Preserve the original test failure.
        }
    }

    private sealed class ListingImageMutationWebApplicationFactory(
        string connectionString,
        string storageRoot,
        ListingImageSaveFailurePlan failurePlan,
        ListingImageDeleteRecorder storageRecorder)
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
                    options => options.UseNpgsql(connectionString));

                services.RemoveAll<IListingRepository>();
                services.AddScoped<ListingRepository>();
                services.AddScoped<IListingRepository>(
                    provider =>
                        new SaveFailingListingRepository(
                            provider.GetRequiredService<ListingRepository>(),
                            failurePlan));

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
                        new RecordingDeleteFileStorage(
                            provider.GetRequiredService<
                                LocalFileStorageService>(),
                            storageRecorder));
            });
        }
    }

    private sealed class SaveFailingListingRepository(
        ListingRepository inner,
        ListingImageSaveFailurePlan failurePlan)
        : IListingRepository
    {
        public Task CreateAsync(
            Listing listing,
            CancellationToken cancellationToken) =>
            inner.CreateAsync(listing, cancellationToken);

        public Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
            GetListingsQuery query,
            CancellationToken cancellationToken) =>
            inner.GetFilteredReadOnlyAsync(query, cancellationToken);

        public Task<ComparableListingsReadResult>
            GetComparableListingsReadOnlyAsync(
                Guid sourceListingId,
                string languageCode,
                int limit,
                CancellationToken cancellationToken) =>
            inner.GetComparableListingsReadOnlyAsync(
                sourceListingId,
                languageCode,
                limit,
                cancellationToken);

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            inner.GetByIdReadOnlyAsync(id, cancellationToken);

        public Task<Listing?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            inner.GetByIdForUpdateAsync(id, cancellationToken);

        public Task<PagedResult<Listing>>
            GetByAgencyIdForDashboardReadOnlyAsync(
                Guid agencyId,
                ListingStatus? status,
                int page,
                int pageSize,
                CancellationToken cancellationToken) =>
            inner.GetByAgencyIdForDashboardReadOnlyAsync(
                agencyId,
                status,
                page,
                pageSize,
                cancellationToken);

        public Task<ListingImageUploadProbeReadModel?>
            GetListingImageUploadProbeReadOnlyAsync(
                Guid listingId,
                CancellationToken cancellationToken) =>
            inner.GetListingImageUploadProbeReadOnlyAsync(
                listingId,
                cancellationToken);

        public Task<IListingImageWriteScope?>
            BeginListingImageWriteAsync(
                Guid listingId,
                CancellationToken cancellationToken) =>
            inner.BeginListingImageWriteAsync(
                listingId,
                cancellationToken);

        public Task<Listing?> GetByIdWithImagesForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            inner.GetByIdWithImagesForUpdateAsync(
                id,
                cancellationToken);

        public Task<PagedResult<Listing>>
            GetByCreatedByUserIdAsync(
                Guid createdByUserId,
                int page,
                int pageSize,
                CancellationToken cancellationToken) =>
            inner.GetByCreatedByUserIdAsync(
                createdByUserId,
                page,
                pageSize,
                cancellationToken);

        public void AddListingImage(ListingImage image)
        {
            inner.AddListingImage(image);
        }

        public void RemoveListingImage(ListingImage image)
        {
            inner.RemoveListingImage(image);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            failurePlan.RecordSaveCall();
            await inner.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class ListingImageSaveFailurePlan(
        int throwOnSaveCall,
        Exception exception)
    {
        private readonly object _sync = new();
        private int _saveCallCount;

        public int SaveCallCount
        {
            get
            {
                lock (_sync)
                {
                    return _saveCallCount;
                }
            }
        }

        public void RecordSaveCall()
        {
            lock (_sync)
            {
                _saveCallCount++;

                if (_saveCallCount == throwOnSaveCall)
                {
                    throw exception;
                }
            }
        }
    }

    private sealed class RecordingDeleteFileStorage(
        LocalFileStorageService inner,
        ListingImageDeleteRecorder recorder)
        : IFileStorageService
    {
        public Task<StoredFileResult> SaveListingImageAsync(
            Guid listingId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            inner.SaveListingImageAsync(
                listingId,
                file,
                cancellationToken);

        public async Task DeleteListingImageAsync(
            Guid listingId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            recorder.Record(
                new ListingImageDeleteCall(
                    listingId,
                    storedFileName,
                    cancellationToken));

            await inner.DeleteListingImageAsync(
                listingId,
                storedFileName,
                cancellationToken);
        }

        public Task<StoredFileResult> SaveUserAvatarAsync(
            Guid userId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            inner.SaveUserAvatarAsync(
                userId,
                file,
                cancellationToken);

        public Task DeleteUserAvatarAsync(
            Guid userId,
            string storedFileName,
            CancellationToken cancellationToken) =>
            inner.DeleteUserAvatarAsync(
                userId,
                storedFileName,
                cancellationToken);

        public Task<StoredFileResult> SaveAgencyLogoAsync(
            Guid agencyId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            inner.SaveAgencyLogoAsync(
                agencyId,
                file,
                cancellationToken);

        public Task DeleteAgencyLogoAsync(
            Guid agencyId,
            string storedFileName,
            CancellationToken cancellationToken) =>
            inner.DeleteAgencyLogoAsync(
                agencyId,
                storedFileName,
                cancellationToken);
    }

    private sealed class ListingImageDeleteRecorder
    {
        private readonly object _sync = new();
        private readonly List<ListingImageDeleteCall> _calls = [];

        public void Record(ListingImageDeleteCall call)
        {
            lock (_sync)
            {
                _calls.Add(call);
            }
        }

        public IReadOnlyList<ListingImageDeleteCall> GetDeleteCalls()
        {
            lock (_sync)
            {
                return _calls.ToArray();
            }
        }
    }

    private sealed record ListingImageDeleteCall(
        Guid ListingId,
        string StoredFileName,
        CancellationToken CancellationToken);

    private sealed class ListingImageSecondSaveException(
        string message)
        : Exception(message);
}
