using FluentAssertions;
using Microsoft.Extensions.Options;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Infrastructure.Storage;

namespace RealEstate.Tests.Unit.Infrastructure.Storage;

public sealed class LocalFileStorageServiceTests
{
    [Theory]
    [InlineData(StorageArea.ListingImage)]
    [InlineData(StorageArea.UserAvatar)]
    [InlineData(StorageArea.AgencyLogo)]
    public async Task SaveOperation_WhenCopySucceeds_PreservesStoredFileContract(
        StorageArea storageArea)
    {
        // Arrange
        string rootPath = CreateTestRoot();

        try
        {
            var targetId = Guid.NewGuid();

            byte[] expectedContent =
            [
                10,
                20,
                30,
                40,
                50
            ];

            using var sourceStream =
                new MemoryStream(expectedContent);

            var file = new UploadedFile(
                sourceStream,
                Path.Combine("nested", "photo.PNG"),
                "image/png",
                expectedContent.LongLength);

            LocalFileStorageService service =
                CreateService(rootPath);

            // Act
            StoredFileResult result =
                await SaveAsync(
                    service,
                    storageArea,
                    targetId,
                    file,
                    CancellationToken.None);

            // Assert
            result.OriginalFileName.Should()
                .Be("photo.PNG");

            result.StoredFileName.Should()
                .MatchRegex("^[0-9a-f]{32}\\.png$");

            result.ContentType.Should()
                .Be("image/png");

            result.SizeBytes.Should()
                .Be(expectedContent.LongLength);

            result.Url.Should()
                .Be(
                    $"/test-uploads/" +
                    $"{GetRelativeUrlPath(storageArea, targetId)}/" +
                    $"{result.StoredFileName}");

            string destinationDirectory =
                GetDestinationDirectory(
                    rootPath,
                    storageArea,
                    targetId);

            Directory.Exists(destinationDirectory)
                .Should()
                .BeTrue();

            string[] files =
                Directory.GetFiles(destinationDirectory);

            files.Should()
                .ContainSingle();

            Path.GetFileName(files.Single())
                .Should()
                .Be(result.StoredFileName);

            byte[] actualContent =
                await File.ReadAllBytesAsync(
                    files.Single());

            actualContent.Should()
                .Equal(expectedContent);

            GetTemporaryFiles(destinationDirectory)
                .Should()
                .BeEmpty();
        }
        finally
        {
            DeleteTestRoot(rootPath);
        }
    }

    [Theory]
    [InlineData(StorageArea.ListingImage)]
    [InlineData(StorageArea.UserAvatar)]
    [InlineData(StorageArea.AgencyLogo)]
    public async Task SaveOperation_WhenCalledTwice_UsesUniqueStoredFileNames(
        StorageArea storageArea)
    {
        // Arrange
        string rootPath = CreateTestRoot();

        try
        {
            var targetId = Guid.NewGuid();

            byte[] expectedContent =
            [
                1,
                2,
                3,
                4
            ];

            LocalFileStorageService service =
                CreateService(rootPath);

            using var firstSource =
                new MemoryStream(expectedContent);

            using var secondSource =
                new MemoryStream(expectedContent);

            var firstFile = new UploadedFile(
                firstSource,
                "photo.PNG",
                "image/png",
                expectedContent.LongLength);

            var secondFile = new UploadedFile(
                secondSource,
                "photo.PNG",
                "image/png",
                expectedContent.LongLength);

            // Act
            StoredFileResult firstResult =
                await SaveAsync(
                    service,
                    storageArea,
                    targetId,
                    firstFile,
                    CancellationToken.None);

            StoredFileResult secondResult =
                await SaveAsync(
                    service,
                    storageArea,
                    targetId,
                    secondFile,
                    CancellationToken.None);

            // Assert
            firstResult.StoredFileName.Should()
                .NotBe(secondResult.StoredFileName);

            firstResult.Url.Should()
                .NotBe(secondResult.Url);

            string destinationDirectory =
                GetDestinationDirectory(
                    rootPath,
                    storageArea,
                    targetId);

            string[] files =
                Directory.GetFiles(destinationDirectory);

            files.Should()
                .HaveCount(2);

            files
                .Select(Path.GetFileName)
                .Should()
                .BeEquivalentTo(
                    firstResult.StoredFileName,
                    secondResult.StoredFileName);

            byte[] firstStoredContent =
                await File.ReadAllBytesAsync(
                    Path.Combine(
                        destinationDirectory,
                        firstResult.StoredFileName));

            byte[] secondStoredContent =
                await File.ReadAllBytesAsync(
                    Path.Combine(
                        destinationDirectory,
                        secondResult.StoredFileName));

            firstStoredContent.Should()
                .Equal(expectedContent);

            secondStoredContent.Should()
                .Equal(expectedContent);

            GetTemporaryFiles(destinationDirectory)
                .Should()
                .BeEmpty();
        }
        finally
        {
            DeleteTestRoot(rootPath);
        }
    }

    [Theory]
    [InlineData(StorageArea.ListingImage)]
    [InlineData(StorageArea.UserAvatar)]
    [InlineData(StorageArea.AgencyLogo)]
    public async Task SaveOperation_WhenSourceThrowsAfterPartialCopy_LeavesNoResidue(
        StorageArea storageArea)
    {
        // Arrange
        string rootPath = CreateTestRoot();

        try
        {
            var targetId = Guid.NewGuid();

            byte[] firstChunk =
            [
                11,
                22,
                33,
                44
            ];

            var injectedException =
                new IOException(
                    "Injected source copy failure.");

            using var sourceStream =
                new ThrowAfterFirstReadStream(
                    firstChunk,
                    injectedException);

            var file = new UploadedFile(
                sourceStream,
                "photo.PNG",
                "image/png",
                firstChunk.LongLength);

            LocalFileStorageService service =
                CreateService(rootPath);

            // Act
            Func<Task> act = async () =>
            {
                await SaveAsync(
                    service,
                    storageArea,
                    targetId,
                    file,
                    CancellationToken.None);
            };

            // Assert
            var thrown =
                await act.Should()
                    .ThrowExactlyAsync<IOException>();

            thrown.Which.Should()
                .BeSameAs(injectedException);

            string destinationDirectory =
                GetDestinationDirectory(
                    rootPath,
                    storageArea,
                    targetId);

            Directory.Exists(destinationDirectory)
                .Should()
                .BeTrue();

            Directory.GetFiles(destinationDirectory)
                .Should()
                .BeEmpty();
        }
        finally
        {
            DeleteTestRoot(rootPath);
        }
    }

    [Theory]
    [InlineData(StorageArea.ListingImage)]
    [InlineData(StorageArea.UserAvatar)]
    [InlineData(StorageArea.AgencyLogo)]
    public async Task SaveOperation_WhenCancellationOccursAfterPartialCopy_LeavesNoResidue(
        StorageArea storageArea)
    {
        // Arrange
        string rootPath = CreateTestRoot();

        using var cancellationSource =
            new CancellationTokenSource();

        Task<StoredFileResult>? saveTask = null;

        try
        {
            var targetId = Guid.NewGuid();

            byte[] firstChunk =
            [
                101,
                102,
                103,
                104
            ];

            using var sourceStream =
                new CancellationAfterFirstReadStream(
                    firstChunk);

            var file = new UploadedFile(
                sourceStream,
                "photo.PNG",
                "image/png",
                firstChunk.LongLength);

            LocalFileStorageService service =
                CreateService(rootPath);

            // Act
            saveTask =
                SaveAsync(
                    service,
                    storageArea,
                    targetId,
                    file,
                    cancellationSource.Token);

            await sourceStream.SecondReadStarted
                .WaitAsync(TimeSpan.FromSeconds(5));

            string destinationDirectory =
                GetDestinationDirectory(
                    rootPath,
                    storageArea,
                    targetId);

            string[] filesDuringCopy =
                Directory.GetFiles(
                    destinationDirectory);

            // The copy has started, but nothing final is published.
            filesDuringCopy.Should()
                .ContainSingle();

            filesDuringCopy.Single()
                .Should()
                .EndWith(".tmp");

            cancellationSource.Cancel();

            Func<Task> act = async () =>
            {
                await saveTask;
            };

            // Assert
            await act.Should()
                .ThrowAsync<OperationCanceledException>();

            Directory.GetFiles(destinationDirectory)
                .Should()
                .BeEmpty();
        }
        finally
        {
            cancellationSource.Cancel();

            if (saveTask is not null)
            {
                try
                {
                    await saveTask;
                }
                catch
                {
                    // The cancellation or test failure is asserted above.
                }
            }

            DeleteTestRoot(rootPath);
        }
    }

    private static LocalFileStorageService CreateService(
        string rootPath)
    {
        var options =
            Options.Create(
                new LocalFileStorageOptions
                {
                    RootPath = rootPath,
                    PublicBasePath = "/test-uploads/"
                });

        return new LocalFileStorageService(options);
    }

    private static Task<StoredFileResult> SaveAsync(
        LocalFileStorageService service,
        StorageArea storageArea,
        Guid targetId,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        return storageArea switch
        {
            StorageArea.ListingImage =>
                service.SaveListingImageAsync(
                    targetId,
                    file,
                    cancellationToken),

            StorageArea.UserAvatar =>
                service.SaveUserAvatarAsync(
                    targetId,
                    file,
                    cancellationToken),

            StorageArea.AgencyLogo =>
                service.SaveAgencyLogoAsync(
                    targetId,
                    file,
                    cancellationToken),

            _ => throw new ArgumentOutOfRangeException(
                nameof(storageArea),
                storageArea,
                null)
        };
    }

    private static string GetDestinationDirectory(
        string rootPath,
        StorageArea storageArea,
        Guid targetId)
    {
        return storageArea switch
        {
            StorageArea.ListingImage =>
                Path.Combine(
                    rootPath,
                    "listings",
                    targetId.ToString()),

            StorageArea.UserAvatar =>
                Path.Combine(
                    rootPath,
                    "users",
                    targetId.ToString(),
                    "avatar"),

            StorageArea.AgencyLogo =>
                Path.Combine(
                    rootPath,
                    "agencies",
                    targetId.ToString(),
                    "logo"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(storageArea),
                storageArea,
                null)
        };
    }

    private static string GetRelativeUrlPath(
        StorageArea storageArea,
        Guid targetId)
    {
        return storageArea switch
        {
            StorageArea.ListingImage =>
                $"listings/{targetId}",

            StorageArea.UserAvatar =>
                $"users/{targetId}/avatar",

            StorageArea.AgencyLogo =>
                $"agencies/{targetId}/logo",

            _ => throw new ArgumentOutOfRangeException(
                nameof(storageArea),
                storageArea,
                null)
        };
    }

    private static string[] GetTemporaryFiles(
        string destinationDirectory)
    {
        return Directory
            .GetFiles(destinationDirectory)
            .Where(
                path => path.EndsWith(
                    ".tmp",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string CreateTestRoot()
    {
        string rootPath =
            Path.Combine(
                Path.GetTempPath(),
                "RealEstate.Tests",
                nameof(LocalFileStorageServiceTests),
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        return rootPath;
    }

    private static void DeleteTestRoot(
        string rootPath)
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(
                rootPath,
                recursive: true);
        }
    }

    public enum StorageArea
    {
        ListingImage,
        UserAvatar,
        AgencyLogo
    }

    private abstract class ReadOnlyTestStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(
            long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowAfterFirstReadStream
        : ReadOnlyTestStream
    {
        private readonly byte[] _firstChunk;
        private readonly Exception _exception;
        private bool _firstReadCompleted;

        public ThrowAfterFirstReadStream(
            byte[] firstChunk,
            Exception exception)
        {
            _firstChunk = firstChunk;
            _exception = exception;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            return ReadCore(
                buffer.AsSpan(
                    offset,
                    count));
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return Task.FromResult(
                    ReadCore(
                        buffer.AsSpan(
                            offset,
                            count)));
            }
            catch (Exception exception)
            {
                return Task.FromException<int>(
                    exception);
            }
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return ValueTask.FromResult(
                    ReadCore(buffer.Span));
            }
            catch (Exception exception)
            {
                return ValueTask.FromException<int>(
                    exception);
            }
        }

        private int ReadCore(
            Span<byte> destination)
        {
            if (_firstReadCompleted)
            {
                throw _exception;
            }

            int byteCount =
                Math.Min(
                    destination.Length,
                    _firstChunk.Length);

            _firstChunk
                .AsSpan(0, byteCount)
                .CopyTo(destination);

            _firstReadCompleted = true;

            return byteCount;
        }
    }

    private sealed class CancellationAfterFirstReadStream
        : ReadOnlyTestStream
    {
        private readonly byte[] _firstChunk;

        private readonly TaskCompletionSource<bool>
            _secondReadStarted =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        private bool _firstReadCompleted;

        public CancellationAfterFirstReadStream(
            byte[] firstChunk)
        {
            _firstChunk = firstChunk;
        }

        public Task SecondReadStarted =>
            _secondReadStarted.Task;

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException(
                "Synchronous reads are not supported.");
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return ReadAsyncCore(
                    buffer.AsMemory(
                        offset,
                        count),
                    cancellationToken)
                .AsTask();
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return ReadAsyncCore(
                buffer,
                cancellationToken);
        }

        private ValueTask<int> ReadAsyncCore(
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_firstReadCompleted)
            {
                int byteCount =
                    Math.Min(
                        destination.Length,
                        _firstChunk.Length);

                _firstChunk
                    .AsMemory(0, byteCount)
                    .CopyTo(destination);

                _firstReadCompleted = true;

                return ValueTask.FromResult(byteCount);
            }

            _secondReadStarted.TrySetResult(true);

            return new ValueTask<int>(
                WaitForCancellationAsync(
                    cancellationToken));
        }

        private static async Task<int>
            WaitForCancellationAsync(
                CancellationToken cancellationToken)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            return 0;
        }
    }
}