using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Unit.Application.Listings;

public sealed class UploadListingImageHandlerTests
{
    [Fact]
    public async Task Handle_WhenPersistenceSaveThrows_DeletesExactlyNewFileAndRethrowsOriginal()
    {
        // Arrange
        using var context = new TestContext();

        var existingImage = CreateExistingImage(
            context.Listing.Id,
            "existing-image.jpg",
            sortOrder: 0,
            isPrimary: true);

        context.Listing.Images.Add(existingImage);

        var persistenceFailure =
            new InvalidOperationException(
                "Injected persistence failure.");

        context.ListingRepository.SaveChangesException =
            persistenceFailure;

        // Act
        Func<Task> act = async () =>
        {
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);
        };

        // Assert
        var thrown =
            await act.Should()
                .ThrowExactlyAsync<InvalidOperationException>();

        thrown.Which.Should()
            .BeSameAs(persistenceFailure);

        thrown.Which.ToString().Should()
            .Contain(
                nameof(FakeListingRepository.SaveChangesAsync));

        context.FileStorage.DeleteCalls.Should()
            .ContainSingle();

        DeleteListingImageCall deleteCall =
            context.FileStorage.DeleteCalls.Single();

        deleteCall.ListingId.Should()
            .Be(context.Listing.Id);

        deleteCall.StoredFileName.Should()
            .Be(context.StoredFile.StoredFileName);

        deleteCall.StoredFileName.Should()
            .NotBe(existingImage.StoredFileName);

        deleteCall.CancellationToken.Should()
            .Be(CancellationToken.None);

        context.Calls.Should()
            .Equal(
                "listing.get",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.add",
                "listing.save",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenPersistenceSaveIsCancelled_UsesNonCancelledCleanupToken()
    {
        // Arrange
        using var context = new TestContext();
        using var requestCancellationSource =
            new CancellationTokenSource();

        var persistenceCancellation =
            new OperationCanceledException(
                "Injected persistence cancellation.",
                requestCancellationSource.Token);

        context.ListingRepository.BeforeSaveChanges =
            _ => requestCancellationSource.Cancel();

        context.ListingRepository.SaveChangesException =
            persistenceCancellation;

        // Act
        Func<Task> act = async () =>
        {
            await context.Handler.Handle(
                context.Command,
                requestCancellationSource.Token);
        };

        // Assert
        var thrown =
            await act.Should()
                .ThrowExactlyAsync<OperationCanceledException>();

        thrown.Which.Should()
            .BeSameAs(persistenceCancellation);

        requestCancellationSource
            .IsCancellationRequested
            .Should()
            .BeTrue();

        context.FileStorage.DeleteCalls.Should()
            .ContainSingle();

        DeleteListingImageCall deleteCall =
            context.FileStorage.DeleteCalls.Single();

        deleteCall.ListingId.Should()
            .Be(context.Listing.Id);

        deleteCall.StoredFileName.Should()
            .Be(context.StoredFile.StoredFileName);

        deleteCall.CancellationToken.Should()
            .Be(CancellationToken.None);

        deleteCall.CancellationToken
            .CanBeCanceled
            .Should()
            .BeFalse();

        deleteCall.CancellationToken
            .IsCancellationRequested
            .Should()
            .BeFalse();

        context.Calls.Should()
            .Equal(
                "listing.get",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.add",
                "listing.save",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenPersistenceAndCleanupBothFail_ThrowsOrderedAggregate()
    {
        // Arrange
        using var context = new TestContext();

        var persistenceFailure =
            new InvalidOperationException(
                "Injected persistence failure.");

        var cleanupFailure =
            new IOException(
                "Injected cleanup failure.");

        context.ListingRepository.SaveChangesException =
            persistenceFailure;

        context.FileStorage.DeleteException =
            cleanupFailure;

        // Act
        Func<Task> act = async () =>
        {
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);
        };

        // Assert
        var thrown =
            await act.Should()
                .ThrowExactlyAsync<AggregateException>();

        thrown.Which.InnerExceptions.Should()
            .HaveCount(2);

        thrown.Which.InnerExceptions[0].Should()
            .BeSameAs(persistenceFailure);

        thrown.Which.InnerExceptions[1].Should()
            .BeSameAs(cleanupFailure);

        context.FileStorage.DeleteCalls.Should()
            .ContainSingle();

        DeleteListingImageCall deleteCall =
            context.FileStorage.DeleteCalls.Single();

        deleteCall.ListingId.Should()
            .Be(context.Listing.Id);

        deleteCall.StoredFileName.Should()
            .Be(context.StoredFile.StoredFileName);

        deleteCall.CancellationToken.Should()
            .Be(CancellationToken.None);

        context.Calls.Should()
            .Equal(
                "listing.get",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.add",
                "listing.save",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenPersistenceSucceeds_DoesNotDeleteStoredFile()
    {
        // Arrange
        using var context = new TestContext();
        using var requestCancellationSource =
            new CancellationTokenSource();

        // Act
        UploadListingImageResult result =
            await context.Handler.Handle(
                context.Command,
                requestCancellationSource.Token);

        // Assert
        result.Succeeded.Should()
            .BeTrue();

        result.Error.Should()
            .Be(UploadListingImageError.None);

        result.Image.Should()
            .NotBeNull();

        context.ListingRepository.GetListingCalls.Should()
            .ContainSingle();

        GetListingCall getListingCall =
            context.ListingRepository.GetListingCalls.Single();

        getListingCall.ListingId.Should()
            .Be(context.Command.ListingId);

        getListingCall.CancellationToken.Should()
            .Be(requestCancellationSource.Token);

        context.UserRepository.GetUserCalls.Should()
            .ContainSingle();

        GetUserCall getUserCall =
            context.UserRepository.GetUserCalls.Single();

        getUserCall.UserId.Should()
            .Be(context.Actor.Id);

        getUserCall.CancellationToken.Should()
            .Be(requestCancellationSource.Token);

        context.ListingRepository.AddedImages.Should()
            .ContainSingle();

        ListingImage addedImage =
            context.ListingRepository.AddedImages.Single();

        addedImage.ListingId.Should()
            .Be(context.Listing.Id);

        addedImage.OriginalFileName.Should()
            .Be(context.StoredFile.OriginalFileName);

        addedImage.StoredFileName.Should()
            .Be(context.StoredFile.StoredFileName);

        addedImage.ContentType.Should()
            .Be(context.StoredFile.ContentType);

        addedImage.SizeBytes.Should()
            .Be(context.StoredFile.SizeBytes);

        addedImage.Url.Should()
            .Be(context.StoredFile.Url);

        addedImage.SortOrder.Should()
            .Be(0);

        addedImage.IsPrimary.Should()
            .BeTrue();

        result.Image!.Id.Should()
            .Be(addedImage.Id);

        result.Image.Url.Should()
            .Be(addedImage.Url);

        result.Image.ContentType.Should()
            .Be(addedImage.ContentType);

        result.Image.SizeBytes.Should()
            .Be(addedImage.SizeBytes);

        result.Image.SortOrder.Should()
            .Be(addedImage.SortOrder);

        result.Image.IsPrimary.Should()
            .Be(addedImage.IsPrimary);

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(1);

        context.ListingRepository.SaveChangesTokens.Should()
            .ContainSingle()
            .Which.Should()
            .Be(requestCancellationSource.Token);

        context.FileStorage.SaveCalls.Should()
            .ContainSingle();

        SaveListingImageCall saveCall =
            context.FileStorage.SaveCalls.Single();

        saveCall.ListingId.Should()
            .Be(context.Listing.Id);

        saveCall.File.Should()
            .BeSameAs(context.Command.File);

        saveCall.CancellationToken.Should()
            .Be(requestCancellationSource.Token);

        context.FileStorage.DeleteCalls.Should()
            .BeEmpty();

        context.Calls.Should()
            .Equal(
                "listing.get",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.add",
                "listing.save");
    }

    [Fact]
    public async Task Handle_WhenStorageSaveThrows_DoesNotAttemptCleanup()
    {
        // Arrange
        using var context = new TestContext();

        var storageFailure =
            new IOException(
                "Injected storage failure.");

        context.FileStorage.SaveException =
            storageFailure;

        // Act
        Func<Task> act = async () =>
        {
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);
        };

        // Assert
        var thrown =
            await act.Should()
                .ThrowExactlyAsync<IOException>();

        thrown.Which.Should()
            .BeSameAs(storageFailure);

        context.FileStorage.SaveCalls.Should()
            .ContainSingle();

        context.FileStorage.DeleteCalls.Should()
            .BeEmpty();

        context.ListingRepository.AddedImages.Should()
            .BeEmpty();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(0);

        context.Calls.Should()
            .Equal(
                "listing.get",
                "current-user.id",
                "user.get",
                "storage.save");
    }

    [Fact]
    public async Task Handle_WhenAddListingImageThrows_DeletesExactlyNewFileAndRethrowsOriginal()
    {
        // Arrange
        using var context = new TestContext();

        var addFailure =
            new InvalidOperationException(
                "Injected AddListingImage failure.");

        context.ListingRepository.AddListingImageException =
            addFailure;

        // Act
        Func<Task> act = async () =>
        {
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);
        };

        // Assert
        var thrown =
            await act.Should()
                .ThrowExactlyAsync<InvalidOperationException>();

        thrown.Which.Should()
            .BeSameAs(addFailure);

        context.ListingRepository.AddedImages.Should()
            .ContainSingle();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(0);

        context.FileStorage.DeleteCalls.Should()
            .ContainSingle();

        DeleteListingImageCall deleteCall =
            context.FileStorage.DeleteCalls.Single();

        deleteCall.ListingId.Should()
            .Be(context.Listing.Id);

        deleteCall.StoredFileName.Should()
            .Be(context.StoredFile.StoredFileName);

        deleteCall.CancellationToken.Should()
            .Be(CancellationToken.None);

        context.Calls.Should()
            .Equal(
                "listing.get",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.add",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenPersistenceFails_DeletesNoExistingImage()
    {
        // Arrange
        using var context = new TestContext();

        ListingImage firstExistingImage =
            CreateExistingImage(
                context.Listing.Id,
                "existing-first.jpg",
                sortOrder: 0,
                isPrimary: true);

        ListingImage secondExistingImage =
            CreateExistingImage(
                context.Listing.Id,
                "existing-second.jpg",
                sortOrder: 1,
                isPrimary: false);

        context.Listing.Images.Add(firstExistingImage);
        context.Listing.Images.Add(secondExistingImage);

        context.ListingRepository.SaveChangesException =
            new InvalidOperationException(
                "Injected persistence failure.");

        // Act
        Func<Task> act = async () =>
        {
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);
        };

        // Assert
        await act.Should()
            .ThrowExactlyAsync<InvalidOperationException>();

        context.FileStorage.DeleteCalls.Should()
            .ContainSingle();

        context.FileStorage.DeleteCalls
            .Select(call => call.StoredFileName)
            .Should()
            .Equal(context.StoredFile.StoredFileName);

        context.FileStorage.DeleteCalls
            .Select(call => call.StoredFileName)
            .Should()
            .NotContain(
                firstExistingImage.StoredFileName,
                secondExistingImage.StoredFileName);

        ListingImage addedImage =
            context.ListingRepository.AddedImages.Single();

        addedImage.SortOrder.Should()
            .Be(2);

        addedImage.IsPrimary.Should()
            .BeFalse();
    }

    private static ListingImage CreateExistingImage(
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
            SizeBytes = 100,
            Url = $"/uploads/listings/{listingId}/{storedFileName}",
            SortOrder = sortOrder,
            IsPrimary = isPrimary
        };
    }

    private static InvalidOperationException UnexpectedCall(
        string memberName)
    {
        return new InvalidOperationException(
            $"Unexpected call to {memberName}.");
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext()
        {
            Calls = [];

            Actor = new User(
                "owner@example.com",
                "password-hash",
                "Test",
                "Owner",
                phoneNumber: null,
                status: UserStatus.Active);

            Listing = new Listing
            {
                Id = Guid.NewGuid()
            };

            Listing.AssignCreator(Actor.Id);

            FileContent =
                new MemoryStream(
                    [10, 20, 30, 40]);

            var uploadedFile = new UploadedFile(
                FileContent,
                "photo.JPG",
                "image/jpeg",
                FileContent.Length);

            Command = new UploadListingImageCommand(
                Listing.Id,
                uploadedFile);

            StoredFile = new StoredFileResult(
                "photo.JPG",
                "new-listing-image.jpg",
                "image/jpeg",
                FileContent.Length,
                $"/uploads/listings/" +
                $"{Listing.Id}/new-listing-image.jpg");

            ListingRepository =
                new FakeListingRepository(Calls)
                {
                    ListingResult = Listing
                };

            FileStorage =
                new FakeFileStorageService(Calls)
                {
                    SaveResult = StoredFile
                };

            UserRepository =
                new FakeUserRepository(Calls)
                {
                    UserResult = Actor
                };

            CurrentUser =
                new FakeCurrentUserService(Calls)
                {
                    ConfiguredUserId = Actor.Id
                };

            Handler = new UploadListingImageHandler(
                ListingRepository,
                FileStorage,
                CurrentUser,
                UserRepository);
        }

        public List<string> Calls { get; }

        public User Actor { get; }

        public Listing Listing { get; }

        public MemoryStream FileContent { get; }

        public UploadListingImageCommand Command { get; }

        public StoredFileResult StoredFile { get; }

        public FakeListingRepository ListingRepository { get; }

        public FakeFileStorageService FileStorage { get; }

        public FakeUserRepository UserRepository { get; }

        public FakeCurrentUserService CurrentUser { get; }

        public UploadListingImageHandler Handler { get; }

        public void Dispose()
        {
            FileContent.Dispose();
        }
    }

    private sealed class FakeListingRepository
        : IListingRepository
    {
        private readonly List<string> _calls;

        public FakeListingRepository(
            List<string> calls)
        {
            _calls = calls;
        }

        public Listing? ListingResult { get; set; }

        public List<GetListingCall> GetListingCalls { get; } = [];

        public Exception? AddListingImageException { get; set; }

        public Exception? SaveChangesException { get; set; }

        public Action<CancellationToken>? BeforeSaveChanges { get; set; }

        public List<ListingImage> AddedImages { get; } = [];

        public List<CancellationToken> SaveChangesTokens { get; } = [];

        public int SaveChangesCallCount { get; private set; }

        public Task<Listing?> GetByIdWithImagesForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            _calls.Add("listing.get");

            GetListingCalls.Add(
                new GetListingCall(
                    id,
                    cancellationToken));

            return Task.FromResult(ListingResult);
        }

        public void AddListingImage(
            ListingImage image)
        {
            _calls.Add("listing.add");
            AddedImages.Add(image);

            if (AddListingImageException is not null)
            {
                throw AddListingImageException;
            }
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            _calls.Add("listing.save");

            SaveChangesCallCount++;
            SaveChangesTokens.Add(cancellationToken);

            BeforeSaveChanges?.Invoke(cancellationToken);

            if (SaveChangesException is not null)
            {
                throw SaveChangesException;
            }

            return Task.CompletedTask;
        }

        public Task CreateAsync(
            Listing listing,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(nameof(CreateAsync));
        }

        public Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
            GetListingsQuery query,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetFilteredReadOnlyAsync));
        }

        public Task<ComparableListingsReadResult>
            GetComparableListingsReadOnlyAsync(
                Guid sourceListingId,
                string languageCode,
                int limit,
                CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetComparableListingsReadOnlyAsync));
        }

        public Task<Listing?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByIdReadOnlyAsync));
        }

        public Task<Listing?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByIdForUpdateAsync));
        }

        public Task<PagedResult<Listing>>
            GetByAgencyIdForDashboardReadOnlyAsync(
                Guid agencyId,
                ListingStatus? status,
                int page,
                int pageSize,
                CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByAgencyIdForDashboardReadOnlyAsync));
        }

        public Task<PagedResult<Listing>>
            GetByCreatedByUserIdAsync(
                Guid createdByUserId,
                int page,
                int pageSize,
                CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByCreatedByUserIdAsync));
        }

        public void RemoveListingImage(
            ListingImage image)
        {
            throw UnexpectedCall(
                nameof(RemoveListingImage));
        }
    }

    private sealed class FakeFileStorageService
        : IFileStorageService
    {
        private readonly List<string> _calls;

        public FakeFileStorageService(
            List<string> calls)
        {
            _calls = calls;
        }

        public StoredFileResult? SaveResult { get; set; }

        public Exception? SaveException { get; set; }

        public Exception? DeleteException { get; set; }

        public List<SaveListingImageCall> SaveCalls { get; } = [];

        public List<DeleteListingImageCall> DeleteCalls { get; } = [];

        public Task<StoredFileResult> SaveListingImageAsync(
            Guid listingId,
            UploadedFile file,
            CancellationToken cancellationToken)
        {
            _calls.Add("storage.save");

            SaveCalls.Add(
                new SaveListingImageCall(
                    listingId,
                    file,
                    cancellationToken));

            if (SaveException is not null)
            {
                throw SaveException;
            }

            return Task.FromResult(
                SaveResult
                ?? throw new InvalidOperationException(
                    "A stored-file result was not configured."));
        }

        public Task DeleteListingImageAsync(
            Guid listingId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            _calls.Add("storage.delete");

            DeleteCalls.Add(
                new DeleteListingImageCall(
                    listingId,
                    storedFileName,
                    cancellationToken));

            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            return Task.CompletedTask;
        }

        public Task<StoredFileResult> SaveUserAvatarAsync(
            Guid userId,
            UploadedFile file,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(SaveUserAvatarAsync));
        }

        public Task DeleteUserAvatarAsync(
            Guid userId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(DeleteUserAvatarAsync));
        }

        public Task<StoredFileResult> SaveAgencyLogoAsync(
            Guid agencyId,
            UploadedFile file,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(SaveAgencyLogoAsync));
        }

        public Task DeleteAgencyLogoAsync(
            Guid agencyId,
            string storedFileName,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(DeleteAgencyLogoAsync));
        }
    }

    private sealed class FakeUserRepository
        : IUserRepository
    {
        private readonly List<string> _calls;

        public FakeUserRepository(
            List<string> calls)
        {
            _calls = calls;
        }

        public User? UserResult { get; set; }

        public List<GetUserCall> GetUserCalls { get; } = [];

        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            _calls.Add("user.get");

            GetUserCalls.Add(
                new GetUserCall(
                    id,
                    cancellationToken));

            return Task.FromResult(UserResult);
        }

        public Task<bool> ExistsByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(ExistsByNormalizedEmailAsync));
        }

        public Task<User?> GetByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByNormalizedEmailAsync));
        }

        public Task<User?> GetByNormalizedEmailReadOnlyAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByNormalizedEmailReadOnlyAsync));
        }

        public Task<User?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByIdForUpdateAsync));
        }

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(AddAsync));
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(SaveChangesAsync));
        }
    }

    private sealed class FakeCurrentUserService
        : ICurrentUserService
    {
        private readonly List<string> _calls;

        public FakeCurrentUserService(
            List<string> calls)
        {
            _calls = calls;
        }

        public Guid? ConfiguredUserId { get; set; }

        public Guid? UserId
        {
            get
            {
                _calls.Add("current-user.id");
                return ConfiguredUserId;
            }
        }

        public bool IsAuthenticated =>
            throw UnexpectedCall(
                nameof(IsAuthenticated));
    }

    private sealed record SaveListingImageCall(
        Guid ListingId,
        UploadedFile File,
        CancellationToken CancellationToken);

    private sealed record GetListingCall(
        Guid ListingId,
        CancellationToken CancellationToken);

    private sealed record GetUserCall(
        Guid UserId,
        CancellationToken CancellationToken);

    private sealed record DeleteListingImageCall(
        Guid ListingId,
        string StoredFileName,
        CancellationToken CancellationToken);
}
