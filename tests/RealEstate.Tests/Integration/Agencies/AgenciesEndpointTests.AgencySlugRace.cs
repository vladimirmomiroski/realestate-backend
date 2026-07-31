using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    private static readonly TimeSpan AgencySlugRaceTimeout =
        TimeSpan.FromSeconds(30);

    [Fact]
    public async Task CreateAsync_SlugUniqueViolation_ReturnsDuplicateAndClearsTracking()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        string slug = $"repository-slug-{Guid.NewGuid():N}";
        Guid persistedAgencyId;
        Guid loserAgencyId;

        await using (AsyncServiceScope seedScope =
                     _factory.Services.CreateAsyncScope())
        {
            IAgencyRepository repository =
                seedScope.ServiceProvider
                    .GetRequiredService<IAgencyRepository>();
            Agency agency = CreatePersistenceAgency(slug, user.UserId);

            AgencyCreationPersistenceResult result =
                await repository.CreateAsync(
                    agency,
                    CancellationToken.None);

            result.Should().Be(
                AgencyCreationPersistenceResult.Succeeded);
            persistedAgencyId = agency.Id;
        }

        await using (AsyncServiceScope metadataScope =
                     _factory.Services.CreateAsyncScope())
        {
            RealEstateDbContext dbContext =
                metadataScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();
            Agency duplicate =
                CreatePersistenceAgency(slug, user.UserId);

            dbContext.Agencies.Add(duplicate);

            Func<Task> act = async () =>
                await dbContext.SaveChangesAsync();

            DbUpdateException exception =
                (await act.Should().ThrowAsync<DbUpdateException>())
                .Which;
            PostgresException postgresException =
                exception.InnerException.Should()
                    .BeOfType<PostgresException>()
                    .Subject;

            postgresException.SqlState.Should().Be(
                PostgresErrorCodes.UniqueViolation);
            postgresException.ConstraintName.Should().Be(
                "IX_Agencies_Slug");

            dbContext.ChangeTracker.Clear();
        }

        await using (AsyncServiceScope resultScope =
                     _factory.Services.CreateAsyncScope())
        {
            IAgencyRepository repository =
                resultScope.ServiceProvider
                    .GetRequiredService<IAgencyRepository>();
            RealEstateDbContext dbContext =
                resultScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();
            Agency loser = CreatePersistenceAgency(slug, user.UserId);
            loserAgencyId = loser.Id;

            AgencyCreationPersistenceResult result =
                await repository.CreateAsync(
                    loser,
                    CancellationToken.None);

            result.Should().Be(
                AgencyCreationPersistenceResult.SlugAlreadyExists);
            dbContext.ChangeTracker.Entries().Should().BeEmpty();
        }

        await using AsyncServiceScope verificationScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext verificationDbContext =
            verificationScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<Agency> agencies = await verificationDbContext.Agencies
            .AsNoTracking()
            .Include(agency => agency.Members)
            .Where(agency => agency.Slug == slug)
            .ToListAsync();

        agencies.Should().ContainSingle();
        agencies.Single().Id.Should().Be(persistedAgencyId);
        agencies.Single().Members.Should().ContainSingle(member =>
            member.UserId == user.UserId &&
            member.Role == AgencyMemberRole.Owner &&
            member.Status == AgencyMemberStatus.Active);
        bool loserMembershipExists =
            await verificationDbContext.Set<AgencyMember>()
            .AsNoTracking()
            .AnyAsync(member => member.AgencyId == loserAgencyId);

        loserMembershipExists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DifferentUniqueViolation_IsRethrownWithoutClearingTracking()
    {
        Agency seed = CreatePersistenceAgency(
            $"primary-seed-{Guid.NewGuid():N}");

        await using (AsyncServiceScope seedScope =
                     _factory.Services.CreateAsyncScope())
        {
            IAgencyRepository seedRepository =
                seedScope.ServiceProvider
                    .GetRequiredService<IAgencyRepository>();

            AgencyCreationPersistenceResult seedResult =
                await seedRepository.CreateAsync(
                    seed,
                    CancellationToken.None);

            seedResult.Should().Be(
                AgencyCreationPersistenceResult.Succeeded);
        }

        await using AsyncServiceScope conflictScope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            conflictScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        IAgencyRepository repository =
            conflictScope.ServiceProvider
                .GetRequiredService<IAgencyRepository>();
        Agency conflictingAgency = CreatePersistenceAgency(
            $"primary-conflict-{Guid.NewGuid():N}");

        dbContext.Entry(conflictingAgency)
            .Property(agency => agency.Id)
            .CurrentValue = seed.Id;

        Func<Task> act = async () =>
            await repository.CreateAsync(
                conflictingAgency,
                CancellationToken.None);

        DbUpdateException exception =
            (await act.Should().ThrowAsync<DbUpdateException>())
            .Which;
        PostgresException postgresException =
            exception.InnerException.Should()
                .BeOfType<PostgresException>()
                .Subject;

        postgresException.SqlState.Should().Be(
            PostgresErrorCodes.UniqueViolation);
        postgresException.ConstraintName.Should().Be("PK_Agencies");
        dbContext.ChangeTracker.Entries<Agency>().Should()
            .ContainSingle(entry => entry.State == EntityState.Added);

        dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateAsync_DifferentSqlStateWithSlugConstraint_IsRethrownWithoutClearingTracking()
    {
        var postgresException = new PostgresException(
            "Injected foreign-key failure.",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.ForeignKeyViolation,
            constraintName: "IX_Agencies_Slug");
        var injectedException = new DbUpdateException(
            "Injected foreign-key update failure.",
            postgresException);

        await AssertInjectedAgencyCreationFailureIsRethrownAsync(
            injectedException);
    }

    [Fact]
    public async Task CreateAsync_UnrelatedDbUpdateException_IsRethrownWithoutClearingTracking()
    {
        var injectedException = new DbUpdateException(
            "Injected unrelated agency persistence failure.");

        await AssertInjectedAgencyCreationFailureIsRethrownAsync(
            injectedException);
    }

    [Fact]
    public async Task CreateAgency_UnrelatedPersistenceFailure_ReturnsSanitizedUnexpectedFailure()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        string connectionString = GetAgencyTestConnectionString();
        string slug = $"provider-marker-{Guid.NewGuid():N}";

        using var localFactory =
            new AgencyCreationFailureWebApplicationFactory(
                connectionString,
                AgencyCreationFailureMode.ThrowPersistenceFailure);
        using HttpClient client = localFactory.CreateClient();

        client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/agencies",
            CreateValidCreateAgencyRequest(slug));
        string responseBody = await response.Content.ReadAsStringAsync();

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerUnexpected,
            "/api/agencies");

        AssertNoAgencyPersistenceDetailLeak(responseBody, slug);
    }

    [Fact]
    public async Task CreateAgency_UnknownPersistenceResult_ReturnsSanitizedUnexpectedFailure()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        string connectionString = GetAgencyTestConnectionString();
        string slug = $"unknown-outcome-{Guid.NewGuid():N}";

        using var localFactory =
            new AgencyCreationFailureWebApplicationFactory(
                connectionString,
                AgencyCreationFailureMode.ReturnUnknownResult);
        using HttpClient client = localFactory.CreateClient();

        client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/agencies",
            CreateValidCreateAgencyRequest(slug));

        string responseBody = await response.Content.ReadAsStringAsync();

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerUnexpected,
            "/api/agencies");

        AssertNoAgencyPersistenceDetailLeak(responseBody, slug);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        bool agencyExists = await dbContext.Agencies.AsNoTracking()
            .AnyAsync(agency => agency.Slug == slug);

        agencyExists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAgency_ConcurrentNormalizedSlugRace_ReturnsOneCreatedOneCanonicalConflictAndPersistsOneAggregate()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        string connectionString = GetAgencyTestConnectionString();
        string slugSuffix = Guid.NewGuid().ToString("N");
        string normalizedSlug = $"agency-race-{slugSuffix}";
        string firstSlug = $" Agency-Race-{slugSuffix} ";
        string secondSlug = normalizedSlug.ToUpperInvariant();
        var coordinator = new AgencySlugRaceCoordinator();

        using var localFactory =
            new AgencySlugRaceWebApplicationFactory(
                connectionString,
                normalizedSlug,
                coordinator);
        using HttpClient firstClient = localFactory.CreateClient();
        using HttpClient secondClient = localFactory.CreateClient();
        using var timeout =
            new CancellationTokenSource(AgencySlugRaceTimeout);
        CancellationToken cancellationToken = timeout.Token;

        firstClient.AuthorizeAs(user.AccessToken);
        secondClient.AuthorizeAs(user.AccessToken);

        Task<HttpResponseMessage>? firstTask = null;
        Task<HttpResponseMessage>? secondTask = null;
        HttpResponseMessage[]? responses = null;

        try
        {
            firstTask = firstClient.PostAsJsonAsync(
                "/api/agencies",
                CreateValidCreateAgencyRequest(firstSlug),
                cancellationToken);
            secondTask = secondClient.PostAsJsonAsync(
                "/api/agencies",
                CreateValidCreateAgencyRequest(secondSlug),
                cancellationToken);

            await coordinator.WaitForBothPrechecksAsync(
                cancellationToken);
            await coordinator.WaitForWinnerSaveAsync(
                cancellationToken);

            AgencySlugPrecheckObservation[] observations =
                coordinator.GetObservations();
            AgencySlugPrecheckObservation winner =
                observations.Single(observation =>
                    observation.Role == AgencySlugRaceRole.Winner);
            AgencySlugPrecheckObservation loser =
                observations.Single(observation =>
                    observation.Role == AgencySlugRaceRole.Loser);

            await using AsyncServiceScope observerScope =
                localFactory.Services.CreateAsyncScope();
            RealEstateDbContext observerDbContext =
                observerScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            int blockedBackendPid =
                await WaitForBlockedBackendAsync(
                    observerDbContext,
                    winner.BackendPid,
                    "%INSERT INTO \"Agencies\"%",
                    requireForUpdate: false,
                    cancellationToken,
                    excludedBackendPid: winner.BackendPid);

            blockedBackendPid.Should().Be(loser.BackendPid);

            coordinator.ReleaseWinner();

            responses = await Task.WhenAll(firstTask, secondTask);

            observations.Should().HaveCount(2);
            observations.Should().OnlyContain(observation =>
                observation.NormalizedSlug == normalizedSlug &&
                !observation.SlugExisted);
            observations.Select(observation => observation.DbContextId)
                .Should().OnlyHaveUniqueItems();
            observations.Select(observation => observation.BackendPid)
                .Should().OnlyHaveUniqueItems();
            coordinator.LoserTrackingWasEmpty.Should().BeTrue();

            responses.Count(response =>
                    response.StatusCode == HttpStatusCode.Created)
                .Should().Be(1);
            responses.Count(response =>
                    response.StatusCode == HttpStatusCode.Conflict)
                .Should().Be(1);

            HttpResponseMessage successfulResponse =
                responses.Single(response =>
                    response.StatusCode == HttpStatusCode.Created);
            HttpResponseMessage losingResponse =
                responses.Single(response =>
                    response.StatusCode == HttpStatusCode.Conflict);
            JsonElement successfulBody = await successfulResponse.Content
                .ReadFromJsonAsync<JsonElement>(cancellationToken);
            Guid persistedAgencyId =
                successfulBody.GetProperty("id").GetGuid();

            successfulBody.GetProperty("slug").GetString()
                .Should().Be(normalizedSlug);
            successfulBody.GetProperty("status").GetString()
                .Should().Be("PendingVerification");
            successfulResponse.Headers.Location.Should().NotBeNull();
            successfulResponse.Headers.Location!.IsAbsoluteUri
                .Should().BeFalse();
            successfulResponse.Headers.Location.OriginalString.Should()
                .Be($"/api/agencies/{persistedAgencyId}");

            string loserBody = await losingResponse.Content
                .ReadAsStringAsync(cancellationToken);

            await ApiFailureAssertions.AssertProblemAsync(
                losingResponse,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictAgencySlugAlreadyExists,
                "/api/agencies");

            AssertNoAgencyPersistenceDetailLeak(
                loserBody,
                normalizedSlug);

            await AssertAgencySlugRaceFinalStateAsync(
                normalizedSlug,
                persistedAgencyId,
                user.UserId,
                cancellationToken);
        }
        finally
        {
            coordinator.ReleaseWinner();

            if (responses is not null)
            {
                foreach (HttpResponseMessage response in responses)
                {
                    response.Dispose();
                }
            }
            else
            {
                timeout.Cancel();
                await DrainAndDisposeResponseTaskAsync(firstTask);
                await DrainAndDisposeResponseTaskAsync(secondTask);
            }
        }
    }

    private async Task AssertInjectedAgencyCreationFailureIsRethrownAsync(
        DbUpdateException injectedException)
    {
        string connectionString = GetAgencyTestConnectionString();
        var interceptor = new ThrowingAgencySaveChangesInterceptor(
            injectedException);
        DbContextOptions<RealEstateDbContext> options =
            new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptor)
                .Options;

        await using var dbContext = new RealEstateDbContext(options);
        var repository = new AgencyRepository(dbContext);
        Agency agency = CreatePersistenceAgency(
            $"injected-{Guid.NewGuid():N}");

        Func<Task> act = async () =>
            await repository.CreateAsync(
                agency,
                CancellationToken.None);

        DbUpdateException thrown =
            (await act.Should().ThrowAsync<DbUpdateException>())
            .Which;

        thrown.Should().BeSameAs(injectedException);
        dbContext.ChangeTracker.Entries<Agency>().Should()
            .ContainSingle(entry => entry.State == EntityState.Added);
    }

    private async Task AssertAgencySlugRaceFinalStateAsync(
        string normalizedSlug,
        Guid persistedAgencyId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<Agency> agencies = await dbContext.Agencies
            .AsNoTracking()
            .Include(agency => agency.Members)
            .Where(agency => agency.Slug == normalizedSlug)
            .ToListAsync(cancellationToken);

        agencies.Should().ContainSingle();
        Agency agency = agencies.Single();
        agency.Id.Should().Be(persistedAgencyId);
        agency.Status.Should().Be(AgencyStatus.PendingVerification);
        agency.CreatedAtUtc.Should().NotBe(default);
        agency.Members.Should().ContainSingle();

        AgencyMember member = agency.Members.Single();
        member.AgencyId.Should().Be(persistedAgencyId);
        member.UserId.Should().Be(ownerUserId);
        member.Role.Should().Be(AgencyMemberRole.Owner);
        member.Status.Should().Be(AgencyMemberStatus.Active);
        member.CreatedAtUtc.Should().NotBe(default);

        int orphanMembershipCount = await dbContext.Set<AgencyMember>()
            .AsNoTracking()
            .CountAsync(member =>
                !dbContext.Agencies.Any(agency =>
                    agency.Id == member.AgencyId),
                cancellationToken);

        orphanMembershipCount.Should().Be(0);
    }

    private string GetAgencyTestConnectionString()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The integration-test connection string is unavailable.");
    }

    private static Agency CreatePersistenceAgency(
        string slug,
        Guid? ownerUserId = null)
    {
        var agency = new Agency(
            name: $"Slug Race Agency {Guid.NewGuid():N}",
            slug: slug,
            description: "Agency slug persistence test.",
            phoneNumber: "+38970123456",
            email: "agency-slug@test.com",
            websiteUrl: "https://agency-slug.test",
            addressLine: "Slug Street 1",
            city: "Skopje",
            municipality: "Centar");

        if (ownerUserId is Guid userId)
        {
            agency.AddMember(userId, AgencyMemberRole.Owner);
        }

        return agency;
    }

    private static void AssertNoAgencyPersistenceDetailLeak(
        string responseBody,
        string submittedSlug)
    {
        responseBody.Should().NotContainEquivalentOf(submittedSlug);
        responseBody.Should().NotContainEquivalentOf("23505");
        responseBody.Should().NotContainEquivalentOf("IX_Agencies_Slug");
        responseBody.Should().NotContainEquivalentOf("Npgsql");
        responseBody.Should().NotContainEquivalentOf("Postgres");
        responseBody.Should().NotContainEquivalentOf("SQLSTATE");
        responseBody.Should().NotContainEquivalentOf("SQL");
        responseBody.Should().NotContainEquivalentOf("DbUpdateException");
        responseBody.Should().NotContainEquivalentOf("exception");
        responseBody.Should().NotContainEquivalentOf("provider-marker");
    }

    private abstract class DelegatingAgencyRepository(
        IAgencyRepository inner) : IAgencyRepository
    {
        protected IAgencyRepository Inner { get; } = inner;

        public virtual Task<AgencyCreationPersistenceResult> CreateAsync(
            Agency agency,
            CancellationToken cancellationToken) =>
            Inner.CreateAsync(agency, cancellationToken);

        public Task<Agency?> GetByIdReadOnlyAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            Inner.GetByIdReadOnlyAsync(agencyId, cancellationToken);

        public Task<Agency?> GetBySlugReadOnlyAsync(
            string slug,
            CancellationToken cancellationToken) =>
            Inner.GetBySlugReadOnlyAsync(slug, cancellationToken);

        public Task<Agency?> GetByIdForUpdateAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            Inner.GetByIdForUpdateAsync(agencyId, cancellationToken);

        public Task<Agency?> GetByIdWithMembersForUpdateAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            Inner.GetByIdWithMembersForUpdateAsync(
                agencyId,
                cancellationToken);

        public void AddMember(AgencyMember member) =>
            Inner.AddMember(member);

        public Task<IAgencyOwnerMutationScope?>
            BeginLastActiveOwnerMutationAsync(
                Guid agencyId,
                CancellationToken cancellationToken) =>
            Inner.BeginLastActiveOwnerMutationAsync(
                agencyId,
                cancellationToken);

        public Task<AgencyMember?> GetMemberByIdForUpdateAsync(
            Guid agencyId,
            Guid memberId,
            CancellationToken cancellationToken) =>
            Inner.GetMemberByIdForUpdateAsync(
                agencyId,
                memberId,
                cancellationToken);

        public Task<AgencyDashboardSummaryReadModel?>
            GetDashboardSummaryReadOnlyAsync(
                Guid agencyId,
                DateTime utcNow,
                CancellationToken cancellationToken) =>
            Inner.GetDashboardSummaryReadOnlyAsync(
                agencyId,
                utcNow,
                cancellationToken);

        public Task<int> CountActiveOwnersAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            Inner.CountActiveOwnersAsync(agencyId, cancellationToken);

        public Task<IReadOnlyList<UserAgencyMembershipReadModel>>
            GetByUserIdReadOnlyAsync(
                Guid userId,
                CancellationToken cancellationToken) =>
            Inner.GetByUserIdReadOnlyAsync(userId, cancellationToken);

        public Task<IReadOnlyList<AgencyMemberReadModel>>
            GetMembersByAgencyIdReadOnlyAsync(
                Guid agencyId,
                CancellationToken cancellationToken) =>
            Inner.GetMembersByAgencyIdReadOnlyAsync(
                agencyId,
                cancellationToken);

        public Task<AgencyMemberAccessReadModel?>
            GetMemberAccessReadOnlyAsync(
                Guid agencyId,
                Guid userId,
                CancellationToken cancellationToken) =>
            Inner.GetMemberAccessReadOnlyAsync(
                agencyId,
                userId,
                cancellationToken);

        public virtual Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken) =>
            Inner.SlugExistsAsync(slug, cancellationToken);

        public Task<bool> ExistsAsync(
            Guid agencyId,
            CancellationToken cancellationToken) =>
            Inner.ExistsAsync(agencyId, cancellationToken);

        public Task<bool> IsActiveMemberAsync(
            Guid agencyId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Inner.IsActiveMemberAsync(
                agencyId,
                userId,
                cancellationToken);

        public Task SaveChangesAsync(
            CancellationToken cancellationToken) =>
            Inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class CoordinatedAgencyRepository(
        IAgencyRepository inner,
        RealEstateDbContext dbContext,
        string targetNormalizedSlug,
        AgencySlugRaceCoordinator coordinator)
        : DelegatingAgencyRepository(inner)
    {
        private AgencySlugRaceRole? _role;

        public override async Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken)
        {
            await EnsureConnectionOpenAsync(
                dbContext,
                cancellationToken);

            bool exists = await base.SlugExistsAsync(
                slug,
                cancellationToken);

            if (!exists && string.Equals(
                    slug,
                    targetNormalizedSlug,
                    StringComparison.Ordinal))
            {
                int backendPid = await GetBackendPidAsync(
                    dbContext,
                    cancellationToken);

                _role = await coordinator.RecordPrecheckAndWaitAsync(
                    dbContext.ContextId.InstanceId,
                    backendPid,
                    slug,
                    exists,
                    cancellationToken);
            }

            return exists;
        }

        public override async Task<AgencyCreationPersistenceResult>
            CreateAsync(
                Agency agency,
                CancellationToken cancellationToken)
        {
            if (!string.Equals(
                    agency.Slug,
                    targetNormalizedSlug,
                    StringComparison.Ordinal))
            {
                return await base.CreateAsync(
                    agency,
                    cancellationToken);
            }

            AgencySlugRaceRole role = _role ??
                throw new InvalidOperationException(
                    "The agency slug race precheck was not observed.");

            if (role == AgencySlugRaceRole.Loser)
            {
                await coordinator.WaitForWinnerSaveAsync(
                    cancellationToken);

                AgencyCreationPersistenceResult result =
                    await base.CreateAsync(
                        agency,
                        cancellationToken);

                coordinator.RecordLoserTrackingState(
                    !dbContext.ChangeTracker.Entries().Any());

                return result;
            }

            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
            bool committed = false;

            try
            {
                AgencyCreationPersistenceResult result =
                    await base.CreateAsync(
                        agency,
                        cancellationToken);

                if (result != AgencyCreationPersistenceResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"The designated winner returned {result}.");
                }

                coordinator.SignalWinnerSave();
                await coordinator.WaitForWinnerReleaseAsync(
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                committed = true;

                return result;
            }
            finally
            {
                if (!committed)
                {
                    try
                    {
                        await transaction.RollbackAsync(
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Preserve the original orchestration failure.
                    }
                }
            }
        }
    }

    private sealed class FailureAgencyRepository(
        IAgencyRepository inner,
        AgencyCreationFailureMode mode)
        : DelegatingAgencyRepository(inner)
    {
        public override Task<AgencyCreationPersistenceResult> CreateAsync(
            Agency agency,
            CancellationToken cancellationToken)
        {
            return mode switch
            {
                AgencyCreationFailureMode.ThrowPersistenceFailure =>
                    throw new DbUpdateException(
                        "Injected provider-marker agency persistence failure."),
                AgencyCreationFailureMode.ReturnUnknownResult =>
                    Task.FromResult(
                        (AgencyCreationPersistenceResult)999),
                _ => throw new InvalidOperationException(
                    $"Unsupported agency test failure mode: {mode}.")
            };
        }
    }

    private sealed class AgencySlugRaceCoordinator
    {
        private readonly object _sync = new();
        private readonly List<AgencySlugPrecheckObservation>
            _observations = [];
        private readonly TaskCompletionSource<bool> _bothPrechecks =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _winnerSaved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseWinner =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool? LoserTrackingWasEmpty { get; private set; }

        public async Task<AgencySlugRaceRole>
            RecordPrecheckAndWaitAsync(
                Guid dbContextId,
                int backendPid,
                string normalizedSlug,
                bool slugExisted,
                CancellationToken cancellationToken)
        {
            AgencySlugRaceRole role;

            lock (_sync)
            {
                if (_observations.Count >= 2)
                {
                    throw new InvalidOperationException(
                        "Unexpected third agency slug precheck.");
                }

                role = _observations.Count == 0
                    ? AgencySlugRaceRole.Winner
                    : AgencySlugRaceRole.Loser;
                _observations.Add(
                    new AgencySlugPrecheckObservation(
                        dbContextId,
                        backendPid,
                        normalizedSlug,
                        slugExisted,
                        role));

                if (_observations.Count == 2)
                {
                    _bothPrechecks.TrySetResult(true);
                }
            }

            await _bothPrechecks.Task.WaitAsync(cancellationToken);

            return role;
        }

        public Task WaitForBothPrechecksAsync(
            CancellationToken cancellationToken) =>
            _bothPrechecks.Task.WaitAsync(cancellationToken);

        public void SignalWinnerSave() =>
            _winnerSaved.TrySetResult(true);

        public Task WaitForWinnerSaveAsync(
            CancellationToken cancellationToken) =>
            _winnerSaved.Task.WaitAsync(cancellationToken);

        public void ReleaseWinner() =>
            _releaseWinner.TrySetResult(true);

        public Task WaitForWinnerReleaseAsync(
            CancellationToken cancellationToken) =>
            _releaseWinner.Task.WaitAsync(cancellationToken);

        public void RecordLoserTrackingState(bool wasEmpty)
        {
            lock (_sync)
            {
                LoserTrackingWasEmpty = wasEmpty;
            }
        }

        public AgencySlugPrecheckObservation[] GetObservations()
        {
            lock (_sync)
            {
                return _observations.ToArray();
            }
        }
    }

    private sealed record AgencySlugPrecheckObservation(
        Guid DbContextId,
        int BackendPid,
        string NormalizedSlug,
        bool SlugExisted,
        AgencySlugRaceRole Role);

    private enum AgencySlugRaceRole
    {
        Winner,
        Loser
    }

    private enum AgencyCreationFailureMode
    {
        ThrowPersistenceFailure,
        ReturnUnknownResult
    }

    private sealed class ThrowingAgencySaveChangesInterceptor(
        DbUpdateException exception)
        : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<InterceptionResult<int>>(
                exception);
        }
    }

    private abstract class AgencyCreationWebApplicationFactory(
        string connectionString)
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
                services.AddDbContext<RealEstateDbContext>(options =>
                    options.UseNpgsql(connectionString));

                services.RemoveAll<IAgencyRepository>();
                services.AddScoped<AgencyRepository>();

                ConfigureAgencyRepository(services);
            });
        }

        protected abstract void ConfigureAgencyRepository(
            IServiceCollection services);
    }

    private sealed class AgencyCreationFailureWebApplicationFactory(
        string connectionString,
        AgencyCreationFailureMode mode)
        : AgencyCreationWebApplicationFactory(connectionString)
    {
        protected override void ConfigureAgencyRepository(
            IServiceCollection services)
        {
            services.AddScoped<IAgencyRepository>(provider =>
                new FailureAgencyRepository(
                    provider.GetRequiredService<AgencyRepository>(),
                    mode));
        }
    }

    private sealed class AgencySlugRaceWebApplicationFactory(
        string connectionString,
        string targetNormalizedSlug,
        AgencySlugRaceCoordinator coordinator)
        : AgencyCreationWebApplicationFactory(connectionString)
    {
        protected override void ConfigureAgencyRepository(
            IServiceCollection services)
        {
            services.AddScoped<IAgencyRepository>(provider =>
                new CoordinatedAgencyRepository(
                    provider.GetRequiredService<AgencyRepository>(),
                    provider.GetRequiredService<RealEstateDbContext>(),
                    targetNormalizedSlug,
                    coordinator));
        }
    }
}
