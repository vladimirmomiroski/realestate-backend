using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Infrastructure.Storage;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed class AgencyLogoFailureContractTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _baseFactory;

    public AgencyLogoFailureContractTests(CustomWebApplicationFactory baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task StorageWriteFailure_ReturnsSanitized500WithoutMutation()
    {
        using var factory = CreateFactory();
        (HttpClient client, Guid agencyId, _) = await CreateAgencyAsync(factory);
        factory.Probe.ThrowOnSave = true;

        using MultipartFormDataContent content = CreateLogoContent(
            "storage-secret.png");
        using HttpResponseMessage response = await client.PutAsync(
            $"/api/agencies/{agencyId}/logo",
            content);

        JsonElement body = await AssertUnexpectedAsync(response, agencyId);
        body.GetRawText().Should().NotContain("storage-secret.png");
        AssertNoLogo(await GetAgencyAsync(factory, agencyId));
        GetLogoFiles(factory, agencyId).Should().BeEmpty();
    }

    [Fact]
    public async Task PersistenceFailure_ReturnsSanitized500AndCompensatesNewFile()
    {
        using var factory = CreateFactory();
        (HttpClient client, Guid agencyId, _) = await CreateAgencyAsync(factory);
        factory.Probe.ThrowOnSaveChanges = true;

        using MultipartFormDataContent content = CreateLogoContent(
            "persistence-secret.png");
        using HttpResponseMessage response = await client.PutAsync(
            $"/api/agencies/{agencyId}/logo",
            content);

        JsonElement body = await AssertUnexpectedAsync(response, agencyId);
        body.GetRawText().Should().NotContain("persistence-secret.png");
        AssertNoLogo(await GetAgencyAsync(factory, agencyId));
        GetLogoFiles(factory, agencyId).Should().BeEmpty();
    }

    [Fact]
    public async Task ReplacementOldFileDeletionFailure_LeavesDurableNewLogoAndOldFile()
    {
        using var factory = CreateFactory();
        (HttpClient client, Guid agencyId, _) = await CreateAgencyAsync(factory);
        await UploadSuccessfullyAsync(client, agencyId, "old.png");
        Agency oldAgency = await GetAgencyAsync(factory, agencyId);
        string oldStoredName = oldAgency.LogoStoredFileName!;

        factory.Probe.ThrowOnDelete = true;
        using MultipartFormDataContent content = CreateLogoContent(
            "replacement-secret.webp",
            "image/webp");
        using HttpResponseMessage response = await client.PutAsync(
            $"/api/agencies/{agencyId}/logo",
            content);

        JsonElement body = await AssertUnexpectedAsync(response, agencyId);
        body.GetRawText().Should().NotContain("replacement-secret.webp");
        Agency persisted = await GetAgencyAsync(factory, agencyId);
        persisted.LogoStoredFileName.Should().NotBe(oldStoredName);
        persisted.LogoContentType.Should().Be("image/webp");
        GetLogoFiles(factory, agencyId).Select(Path.GetFileName)
            .Should().Contain([oldStoredName, persisted.LogoStoredFileName!]);
    }

    [Fact]
    public async Task DeletePersistenceFailure_PreservesMetadataAndPhysicalFile()
    {
        using var factory = CreateFactory();
        (HttpClient client, Guid agencyId, _) = await CreateAgencyAsync(factory);
        await UploadSuccessfullyAsync(client, agencyId, "delete-db.png");
        Agency before = await GetAgencyAsync(factory, agencyId);
        string storedName = before.LogoStoredFileName!;
        factory.Probe.ThrowOnSaveChanges = true;

        using HttpResponseMessage response = await client.DeleteAsync(
            $"/api/agencies/{agencyId}/logo");

        await AssertUnexpectedAsync(response, agencyId);
        Agency persisted = await GetAgencyAsync(factory, agencyId);
        persisted.LogoStoredFileName.Should().Be(storedName);
        GetLogoFiles(factory, agencyId).Select(Path.GetFileName)
            .Should().Contain(storedName);
    }

    [Fact]
    public async Task PostCommitPhysicalDeletionFailure_LeavesDurableDatabaseDeletion()
    {
        using var factory = CreateFactory();
        (HttpClient client, Guid agencyId, _) = await CreateAgencyAsync(factory);
        await UploadSuccessfullyAsync(client, agencyId, "delete-file-secret.png");
        string storedName = (await GetAgencyAsync(factory, agencyId))
            .LogoStoredFileName!;
        factory.Probe.ThrowOnDelete = true;

        using HttpResponseMessage response = await client.DeleteAsync(
            $"/api/agencies/{agencyId}/logo");

        JsonElement body = await AssertUnexpectedAsync(response, agencyId);
        body.GetRawText().Should().NotContain("delete-file-secret.png");
        AssertNoLogo(await GetAgencyAsync(factory, agencyId));
        GetLogoFiles(factory, agencyId).Select(Path.GetFileName)
            .Should().Contain(storedName);
    }

    private AgencyLogoFailureWebApplicationFactory CreateFactory()
    {
        using IServiceScope scope = _baseFactory.Services.CreateScope();
        string connectionString = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>()
            .Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The initialized test connection string is unavailable.");
        return new AgencyLogoFailureWebApplicationFactory(connectionString);
    }

    private static async Task<(HttpClient Client, Guid AgencyId, AuthenticatedTestUser User)>
        CreateAgencyAsync(AgencyLogoFailureWebApplicationFactory factory)
    {
        HttpClient client = factory.CreateClient();
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            client,
            $"agency-logo-failure-{Guid.NewGuid():N}@test.com");
        client.AuthorizeAs(user.AccessToken);
        string slug = $"agency-logo-failure-{Guid.NewGuid():N}";

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/agencies",
            new { name = "Agency Logo Failure", slug });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (client, body.GetProperty("id").GetGuid(), user);
    }

    private static async Task UploadSuccessfullyAsync(
        HttpClient client,
        Guid agencyId,
        string fileName)
    {
        using MultipartFormDataContent content = CreateLogoContent(fileName);
        using HttpResponseMessage response = await client.PutAsync(
            $"/api/agencies/{agencyId}/logo",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static MultipartFormDataContent CreateLogoContent(
        string fileName,
        string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    private static Task<JsonElement> AssertUnexpectedAsync(
        HttpResponseMessage response,
        Guid agencyId)
    {
        return ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerUnexpected,
            $"/api/agencies/{agencyId}/logo");
    }

    private static async Task<Agency> GetAgencyAsync(
        AgencyLogoFailureWebApplicationFactory factory,
        Guid agencyId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>()
            .Agencies.AsNoTracking()
            .SingleAsync(agency => agency.Id == agencyId);
    }

    private static void AssertNoLogo(Agency agency)
    {
        agency.LogoUrl.Should().BeNull();
        agency.LogoStoredFileName.Should().BeNull();
        agency.LogoContentType.Should().BeNull();
        agency.LogoSizeBytes.Should().BeNull();
    }

    private static IReadOnlyList<string> GetLogoFiles(
        AgencyLogoFailureWebApplicationFactory factory,
        Guid agencyId)
    {
        string directory = Path.Combine(
            factory.StorageRoot,
            "agencies",
            agencyId.ToString(),
            "logo");
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory)
            : [];
    }

    private sealed class AgencyLogoFailureWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public AgencyLogoFailureWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
            StorageRoot = Path.Combine(
                Path.GetTempPath(),
                "realestate-12g-logo-tests",
                Guid.NewGuid().ToString("N"));
        }

        public string StorageRoot { get; }
        public AgencyLogoFailureProbe Probe { get; } = new();

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
                        ["ConnectionStrings:DefaultConnection"] = _connectionString
                    }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<RealEstateDbContext>>();
                services.RemoveAll<RealEstateDbContext>();
                services.AddDbContext<RealEstateDbContext>(options =>
                    options.UseNpgsql(_connectionString));
                services.PostConfigure<LocalFileStorageOptions>(options =>
                    options.RootPath = StorageRoot);
                services.AddSingleton(Probe);

                services.RemoveAll<IFileStorageService>();
                services.AddScoped<LocalFileStorageService>();
                services.AddScoped<IFileStorageService>(provider =>
                    new AgencyLogoStorageDecorator(
                        provider.GetRequiredService<LocalFileStorageService>(),
                        Probe));

                services.RemoveAll<IAgencyRepository>();
                services.AddScoped<AgencyRepository>();
                services.AddScoped<IAgencyRepository>(provider =>
                    new AgencyLogoRepositoryDecorator(
                        provider.GetRequiredService<AgencyRepository>(),
                        Probe));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(StorageRoot))
            {
                Directory.Delete(StorageRoot, recursive: true);
            }
        }
    }

    private sealed class AgencyLogoFailureProbe
    {
        public bool ThrowOnSave { get; set; }
        public bool ThrowOnDelete { get; set; }
        public bool ThrowOnSaveChanges { get; set; }
    }

    private sealed class AgencyLogoStorageDecorator(
        IFileStorageService inner,
        AgencyLogoFailureProbe probe) : IFileStorageService
    {
        public Task<StoredFileResult> SaveAgencyLogoAsync(
            Guid agencyId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            probe.ThrowOnSave
                ? throw new InjectedAgencyLogoStorageException()
                : inner.SaveAgencyLogoAsync(agencyId, file, cancellationToken);

        public Task DeleteAgencyLogoAsync(
            Guid agencyId,
            string storedFileName,
            CancellationToken cancellationToken) =>
            probe.ThrowOnDelete
                ? throw new InjectedAgencyLogoStorageException()
                : inner.DeleteAgencyLogoAsync(
                    agencyId,
                    storedFileName,
                    cancellationToken);

        public Task<StoredFileResult> SaveListingImageAsync(
            Guid listingId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            inner.SaveListingImageAsync(listingId, file, cancellationToken);

        public Task DeleteListingImageAsync(
            Guid listingId,
            string storedFileName,
            CancellationToken cancellationToken) =>
            inner.DeleteListingImageAsync(listingId, storedFileName, cancellationToken);

        public Task<StoredFileResult> SaveUserAvatarAsync(
            Guid userId,
            UploadedFile file,
            CancellationToken cancellationToken) =>
            inner.SaveUserAvatarAsync(userId, file, cancellationToken);

        public Task DeleteUserAvatarAsync(
            Guid userId,
            string storedFileName,
            CancellationToken cancellationToken) =>
            inner.DeleteUserAvatarAsync(userId, storedFileName, cancellationToken);
    }

    private sealed class AgencyLogoRepositoryDecorator(
        IAgencyRepository inner,
        AgencyLogoFailureProbe probe) : IAgencyRepository
    {
        public Task CreateAsync(Agency agency, CancellationToken cancellationToken) =>
            inner.CreateAsync(agency, cancellationToken);

        public Task<Agency?> GetByIdReadOnlyAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.GetByIdReadOnlyAsync(agencyId, cancellationToken);

        public Task<Agency?> GetBySlugReadOnlyAsync(
            string slug,
            CancellationToken cancellationToken) =>
            inner.GetBySlugReadOnlyAsync(slug, cancellationToken);

        public Task<Agency?> GetByIdForUpdateAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.GetByIdForUpdateAsync(agencyId, cancellationToken);

        public Task<Agency?> GetByIdWithMembersForUpdateAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.GetByIdWithMembersForUpdateAsync(agencyId, cancellationToken);

        public void AddMember(AgencyMember member) => inner.AddMember(member);

        public Task<IAgencyOwnerMutationScope?> BeginLastActiveOwnerMutationAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.BeginLastActiveOwnerMutationAsync(agencyId, cancellationToken);

        public Task<AgencyMember?> GetMemberByIdForUpdateAsync(
            Guid agencyId,
            Guid memberId,
            CancellationToken cancellationToken) =>
            inner.GetMemberByIdForUpdateAsync(
                agencyId,
                memberId,
                cancellationToken);

        public Task<AgencyDashboardSummaryReadModel?> GetDashboardSummaryReadOnlyAsync(
            Guid agencyId,
            DateTime utcNow,
            CancellationToken cancellationToken) =>
            inner.GetDashboardSummaryReadOnlyAsync(
                agencyId,
                utcNow,
                cancellationToken);

        public Task<int> CountActiveOwnersAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.CountActiveOwnersAsync(agencyId, cancellationToken);

        public Task<IReadOnlyList<UserAgencyMembershipReadModel>> GetByUserIdReadOnlyAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            inner.GetByUserIdReadOnlyAsync(userId, cancellationToken);

        public Task<IReadOnlyList<AgencyMemberReadModel>> GetMembersByAgencyIdReadOnlyAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.GetMembersByAgencyIdReadOnlyAsync(agencyId, cancellationToken);

        public Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(
            Guid agencyId,
            Guid userId,
            CancellationToken cancellationToken) =>
            inner.GetMemberAccessReadOnlyAsync(agencyId, userId, cancellationToken);

        public Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken) =>
            inner.SlugExistsAsync(slug, cancellationToken);

        public Task<bool> ExistsAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            inner.ExistsAsync(agencyId, cancellationToken);

        public Task<bool> IsActiveMemberAsync(
            Guid agencyId,
            Guid userId,
            CancellationToken cancellationToken) =>
            inner.IsActiveMemberAsync(agencyId, userId, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            probe.ThrowOnSaveChanges
                ? throw new InjectedAgencyLogoPersistenceException()
                : inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class InjectedAgencyLogoStorageException : Exception
    {
    }

    private sealed class InjectedAgencyLogoPersistenceException : Exception
    {
    }
}
