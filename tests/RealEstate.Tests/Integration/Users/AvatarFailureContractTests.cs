using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Infrastructure.Storage;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Users;

public sealed class AvatarFailureContractTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _baseFactory;

    public AvatarFailureContractTests(CustomWebApplicationFactory baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task AvatarStorageFailure_ReturnsSanitized500WithoutDatabaseMutation()
    {
        using HttpClient setupClient = _baseFactory.CreateClient();
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            setupClient,
            "avatar-storage-failure@test.com");

        using var factory = new AvatarFailureWebApplicationFactory(
            GetConnectionString(),
            throwOnSaveChanges: false,
            throwOnStorage: true);
        using HttpClient client = factory.CreateClient();
        client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent();
        HttpResponseMessage response = await client.PutAsync(
            "/api/users/me/avatar",
            content);

        var body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerUnexpected,
            "/api/users/me/avatar");
        body.GetRawText().Should().NotContain("avatar-failure-secret.png");
        (await GetUserAsync(user.UserId)).AvatarUrl.Should().BeNull();
        DirectoryContainsFiles(factory.StorageRoot).Should().BeFalse();
    }

    [Fact]
    public async Task AvatarPersistenceFailure_ReturnsSanitized500AndRemovesNewFile()
    {
        using HttpClient setupClient = _baseFactory.CreateClient();
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            setupClient,
            "avatar-persistence-failure@test.com");

        using var factory = new AvatarFailureWebApplicationFactory(
            GetConnectionString(),
            throwOnSaveChanges: true,
            throwOnStorage: false);
        using HttpClient client = factory.CreateClient();
        client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent();
        HttpResponseMessage response = await client.PutAsync(
            "/api/users/me/avatar",
            content);

        var body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerUnexpected,
            "/api/users/me/avatar");
        body.GetRawText().Should().NotContain("avatar-failure-secret.png");

        User persistedUser = await GetUserAsync(user.UserId);
        persistedUser.AvatarUrl.Should().BeNull();
        persistedUser.AvatarStoredFileName.Should().BeNull();
        persistedUser.AvatarContentType.Should().BeNull();
        persistedUser.AvatarSizeBytes.Should().BeNull();
        DirectoryContainsFiles(factory.StorageRoot).Should().BeFalse();
    }

    private static MultipartFormDataContent CreateAvatarContent()
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "avatar-failure-secret.png");
        return content;
    }

    private string GetConnectionString()
    {
        using IServiceScope scope = _baseFactory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>()
            .Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The initialized test connection string is unavailable.");
    }

    private async Task<User> GetUserAsync(Guid userId)
    {
        await using AsyncServiceScope scope =
            _baseFactory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>()
            .Users.AsNoTracking()
            .SingleAsync(user => user.Id == userId);
    }

    private static bool DirectoryContainsFiles(string path)
    {
        return Directory.Exists(path) &&
            Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();
    }

    private sealed class AvatarFailureWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly bool _throwOnSaveChanges;
        private readonly bool _throwOnStorage;

        public AvatarFailureWebApplicationFactory(
            string connectionString,
            bool throwOnSaveChanges,
            bool throwOnStorage)
        {
            _connectionString = connectionString;
            _throwOnSaveChanges = throwOnSaveChanges;
            _throwOnStorage = throwOnStorage;
            StorageRoot = Path.Combine(
                Path.GetTempPath(),
                "realestate-12c-avatar-tests",
                Guid.NewGuid().ToString("N"));
        }

        public string StorageRoot { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                _connectionString);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            _connectionString
                    }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<RealEstateDbContext>>();
                services.RemoveAll<RealEstateDbContext>();
                services.AddDbContext<RealEstateDbContext>(options =>
                    options.UseNpgsql(_connectionString));
                services.PostConfigure<LocalFileStorageOptions>(options =>
                    options.RootPath = StorageRoot);

                if (_throwOnStorage)
                {
                    services.RemoveAll<IFileStorageService>();
                    services.AddSingleton<IFileStorageService,
                        ThrowingAvatarStorageService>();
                }

                if (_throwOnSaveChanges)
                {
                    services.RemoveAll<IUserRepository>();
                    services.AddScoped<UserRepository>();
                    services.AddScoped<IUserRepository>(provider =>
                        new ThrowingSaveUserRepository(
                            provider.GetRequiredService<UserRepository>()));
                }
            });
        }
    }

    private sealed class ThrowingAvatarStorageService : IFileStorageService
    {
        public Task<StoredFileResult> SaveUserAvatarAsync(
            Guid userId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            throw new InjectedAvatarStorageException();

        public Task DeleteUserAvatarAsync(
            Guid userId,
            string storedFileName,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredFileResult> SaveListingImageAsync(
            Guid listingId,
            UploadedFile file,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteListingImageAsync(
            Guid listingId,
            string storedFileName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StoredFileResult> SaveAgencyLogoAsync(
            Guid agencyId,
            UploadedFile file,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAgencyLogoAsync(
            Guid agencyId,
            string storedFileName,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ThrowingSaveUserRepository(IUserRepository inner)
        : IUserRepository
    {
        public Task<bool> ExistsByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            inner.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        public Task<User?> GetByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            inner.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        public Task<User?> GetByNormalizedEmailReadOnlyAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            inner.GetByNormalizedEmailReadOnlyAsync(normalizedEmail, cancellationToken);

        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            inner.GetByIdReadOnlyAsync(id, cancellationToken);

        public Task<User?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            inner.GetByIdForUpdateAsync(id, cancellationToken);

        public Task AddAsync(User user, CancellationToken cancellationToken) =>
            inner.AddAsync(user, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InjectedAvatarPersistenceException();
    }

    private sealed class InjectedAvatarStorageException : Exception
    {
    }

    private sealed class InjectedAvatarPersistenceException : Exception
    {
    }
}
