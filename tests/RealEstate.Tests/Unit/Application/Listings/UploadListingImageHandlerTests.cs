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

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.dispose",
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

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.dispose",
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

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.dispose",
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

        context.ListingRepository.UploadProbeCalls.Should()
            .ContainSingle();

        GetListingCall uploadProbeCall =
            context.ListingRepository.UploadProbeCalls.Single();

        uploadProbeCall.ListingId.Should()
            .Be(context.Command.ListingId);

        uploadProbeCall.CancellationToken.Should()
            .Be(requestCancellationSource.Token);

        context.ListingRepository.BeginWriteScopeCalls.Should()
            .ContainSingle();

        GetListingCall beginWriteScopeCall =
            context.ListingRepository.BeginWriteScopeCalls.Single();

        beginWriteScopeCall.ListingId.Should()
            .Be(context.Command.ListingId);

        beginWriteScopeCall.CancellationToken.Should()
            .Be(requestCancellationSource.Token);

        context.UserRepository.GetUserCalls.Should()
            .HaveCount(2);

        context.UserRepository.GetUserCalls.Should()
            .OnlyContain(call =>
                call.UserId == context.Actor.Id &&
                call.CancellationToken ==
                    requestCancellationSource.Token);

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

        context.WriteScope.CommitCallCount.Should()
            .Be(1);

        context.WriteScope.CommitTokens.Should()
            .ContainSingle()
            .Which.Should()
            .Be(requestCancellationSource.Token);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.commit",
                "listing.scope.dispose");
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

        context.ListingRepository.BeginWriteScopeCalls.Should()
            .BeEmpty();

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(0);

        context.Calls.Should()
            .Equal(
                "listing.probe",
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

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.scope.dispose",
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

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

        context.Calls.Should()
            .Equal(
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.dispose",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenProtectedCapacityIsReached_DisposesScopeThenDeletesFileAndReturnsLimit()
    {
        // Arrange
        using var context = new TestContext();

        context.ListingRepository.UploadProbeResult =
            new ListingImageUploadProbeReadModel(
                context.Listing.Id,
                context.Actor.Id,
                ImageCount: 19);

        AddExistingImages(
            context.Listing,
            count: 20);

        // Act
        UploadListingImageResult result =
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);

        // Assert
        result.Succeeded.Should()
            .BeFalse();

        result.Error.Should()
            .Be(UploadListingImageError.ImageLimitReached);

        result.Image.Should()
            .BeNull();

        context.ListingRepository.AddedImages.Should()
            .BeEmpty();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(0);

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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

        context.UserRepository.GetUserCalls.Should()
            .HaveCount(2);

        context.Calls.Should()
            .Equal(
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.scope.dispose",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenProtectedListingOwnerChanges_DisposesScopeThenDeletesFileAndReturnsNotOwner()
    {
        // Arrange
        using var context = new TestContext();

        context.ListingRepository.UploadProbeResult =
            new ListingImageUploadProbeReadModel(
                context.Listing.Id,
                context.Actor.Id,
                ImageCount: 0);

        context.Listing.AssignCreator(
            Guid.NewGuid());

        // Act
        UploadListingImageResult result =
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);

        // Assert
        result.Succeeded.Should()
            .BeFalse();

        result.Error.Should()
            .Be(UploadListingImageError.NotListingOwner);

        result.Image.Should()
            .BeNull();

        context.ListingRepository.AddedImages.Should()
            .BeEmpty();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(0);

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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

        context.UserRepository.GetUserCalls.Should()
            .HaveCount(2);

        context.Calls.Should()
            .Equal(
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.scope.dispose",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenWriteScopeReturnsNull_DeletesStoredFileAndReturnsListingNotFound()
    {
        // Arrange
        using var context = new TestContext();

        context.ListingRepository.WriteScopeResult = null;

        // Act
        UploadListingImageResult result =
            await context.Handler.Handle(
                context.Command,
                CancellationToken.None);

        // Assert
        result.Succeeded.Should()
            .BeFalse();

        result.Error.Should()
            .Be(UploadListingImageError.ListingNotFound);

        result.Image.Should()
            .BeNull();

        context.ListingRepository.AddedImages.Should()
            .BeEmpty();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(0);

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
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

        context.UserRepository.GetUserCalls.Should()
            .ContainSingle();

        context.Calls.Should()
            .Equal(
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenCommitThrows_DisposesScopeDeletesFileAndRethrowsOriginal()
    {
        // Arrange
        using var context = new TestContext();

        var commitFailure =
            new InvalidOperationException(
                "Injected commit failure.");

        context.WriteScope.CommitException =
            commitFailure;

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
            .BeSameAs(commitFailure);

        context.ListingRepository.AddedImages.Should()
            .ContainSingle();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(1);

        context.WriteScope.CommitCallCount.Should()
            .Be(1);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.commit",
                "listing.scope.dispose",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenCommitIsCancelled_UsesNonCancelledCleanupToken()
    {
        // Arrange
        using var context = new TestContext();
        using var requestCancellationSource =
            new CancellationTokenSource();

        var commitCancellation =
            new OperationCanceledException(
                "Injected commit cancellation.",
                requestCancellationSource.Token);

        context.WriteScope.BeforeCommit =
            _ => requestCancellationSource.Cancel();

        context.WriteScope.CommitException =
            commitCancellation;

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
            .BeSameAs(commitCancellation);

        requestCancellationSource
            .IsCancellationRequested
            .Should()
            .BeTrue();

        context.WriteScope.CommitCallCount.Should()
            .Be(1);

        context.WriteScope.CommitTokens.Should()
            .ContainSingle()
            .Which.Should()
            .Be(requestCancellationSource.Token);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.commit",
                "listing.scope.dispose",
                "storage.delete");
    }

    [Fact]
    public async Task Handle_WhenCommitSucceedsButScopeDisposalThrows_DoesNotDeleteCommittedFile()
    {
        // Arrange
        using var context = new TestContext();

        var disposalFailure =
            new InvalidOperationException(
                "Injected scope disposal failure.");

        context.WriteScope.DisposeException =
            disposalFailure;

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
            .BeSameAs(disposalFailure);

        context.ListingRepository.AddedImages.Should()
            .ContainSingle();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(1);

        context.WriteScope.CommitCallCount.Should()
            .Be(1);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

        context.FileStorage.DeleteCalls.Should()
            .BeEmpty();

        context.Calls.Should()
            .Equal(
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.add",
                "listing.save",
                "listing.scope.commit",
                "listing.scope.dispose");
    }

    [Fact]
    public async Task Handle_WhenCapacityCleanupThrows_PropagatesCleanupFailure()
    {
        // Arrange
        using var context = new TestContext();

        context.ListingRepository.UploadProbeResult =
            new ListingImageUploadProbeReadModel(
                context.Listing.Id,
                context.Actor.Id,
                ImageCount: 19);

        AddExistingImages(
            context.Listing,
            count: 20);

        var cleanupFailure =
            new IOException(
                "Injected capacity cleanup failure.");

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
                .ThrowExactlyAsync<IOException>();

        thrown.Which.Should()
            .BeSameAs(cleanupFailure);

        context.ListingRepository.AddedImages.Should()
            .BeEmpty();

        context.ListingRepository.SaveChangesCallCount.Should()
            .Be(0);

        context.WriteScope.CommitCallCount.Should()
            .Be(0);

        context.WriteScope.DisposeCallCount.Should()
            .Be(1);

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
                "listing.probe",
                "current-user.id",
                "user.get",
                "storage.save",
                "listing.scope.begin",
                "user.get",
                "listing.scope.dispose",
                "storage.delete");
    }

    private static void AddExistingImages(
        Listing listing,
        int count)
    {
        for (int sortOrder = 0;
             sortOrder < count;
             sortOrder++)
        {
            listing.Images.Add(
                CreateExistingImage(
                    listing.Id,
                    $"existing-{sortOrder}.jpg",
                    sortOrder,
                    isPrimary: sortOrder == 0));
        }
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

            UploadProbe =
                new ListingImageUploadProbeReadModel(
                    Listing.Id,
                    Listing.CreatedByUserId,
                    Listing.Images.Count);

            WriteScope =
                new FakeListingImageWriteScope(
                    Listing,
                    Calls);

            ListingRepository =
                new FakeListingRepository(Calls)
                {
                    UploadProbeResult = UploadProbe,
                    WriteScopeResult = WriteScope
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

        public ListingImageUploadProbeReadModel UploadProbe { get; }

        public FakeListingImageWriteScope WriteScope { get; }

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

        public ListingImageUploadProbeReadModel?
            UploadProbeResult
            { get; set; }

        public IListingImageWriteScope?
            WriteScopeResult
            { get; set; }

        public List<GetListingCall> UploadProbeCalls { get; } = [];

        public List<GetListingCall> BeginWriteScopeCalls { get; } = [];

        public Exception? AddListingImageException { get; set; }

        public Exception? SaveChangesException { get; set; }

        public Action<CancellationToken>? BeforeSaveChanges { get; set; }

        public List<ListingImage> AddedImages { get; } = [];

        public List<CancellationToken> SaveChangesTokens { get; } = [];

        public int SaveChangesCallCount { get; private set; }

        public Task<ListingImageUploadProbeReadModel?>
            GetListingImageUploadProbeReadOnlyAsync(
                Guid listingId,
                CancellationToken cancellationToken)
        {
            _calls.Add("listing.probe");

            UploadProbeCalls.Add(
                new GetListingCall(
                    listingId,
                    cancellationToken));

            return Task.FromResult(
                UploadProbeResult);
        }

        public Task<IListingImageWriteScope?>
            BeginListingImageWriteAsync(
                Guid listingId,
                CancellationToken cancellationToken)
        {
            _calls.Add("listing.scope.begin");

            BeginWriteScopeCalls.Add(
                new GetListingCall(
                    listingId,
                    cancellationToken));

            return Task.FromResult(
                WriteScopeResult);
        }

        public Task<Listing?> GetByIdWithImagesForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(GetByIdWithImagesForUpdateAsync));
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

    private sealed class FakeListingImageWriteScope
        : IListingImageWriteScope
    {
        private readonly List<string> _calls;

        public FakeListingImageWriteScope(
            Listing listing,
            List<string> calls)
        {
            Listing = listing;
            _calls = calls;
        }

        public Listing Listing { get; }

        public Exception? CommitException { get; set; }

        public Exception? DisposeException { get; set; }

        public Action<CancellationToken>? BeforeCommit { get; set; }

        public int CommitCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public List<CancellationToken> CommitTokens { get; } = [];

        public Task CommitAsync(
            CancellationToken cancellationToken)
        {
            _calls.Add("listing.scope.commit");

            CommitCallCount++;
            CommitTokens.Add(cancellationToken);

            BeforeCommit?.Invoke(cancellationToken);

            if (CommitException is not null)
            {
                throw CommitException;
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _calls.Add("listing.scope.dispose");

            DisposeCallCount++;

            if (DisposeException is not null)
            {
                return ValueTask.FromException(
                    DisposeException);
            }

            return ValueTask.CompletedTask;
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

        public Task<UserRegistrationPersistenceResult>
            PersistRegistrationAsync(
                User user,
                CancellationToken cancellationToken)
        {
            throw UnexpectedCall(
                nameof(PersistRegistrationAsync));
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
