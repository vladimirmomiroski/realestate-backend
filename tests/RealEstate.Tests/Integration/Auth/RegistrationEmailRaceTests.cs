using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Auth.Dtos;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;
using RealEstate.Tests.Integration.Api;

namespace RealEstate.Tests.Integration.Auth;

public sealed class RegistrationEmailRaceTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly TimeSpan TestTimeout =
        TimeSpan.FromSeconds(30);

    private readonly CustomWebApplicationFactory _factory;

    public RegistrationEmailRaceTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PersistRegistration_NormalizedEmailUniqueViolation_ReturnsExpectedOutcomeAndClearsTracking()
    {
        string email =
            $"repository-duplicate-{Guid.NewGuid():N}@test.com";

        await using (AsyncServiceScope seedScope =
                     _factory.Services.CreateAsyncScope())
        {
            IUserRepository seedRepository =
                seedScope.ServiceProvider
                    .GetRequiredService<IUserRepository>();

            UserRegistrationPersistenceResult seedResult =
                await seedRepository.PersistRegistrationAsync(
                    CreateUser(email),
                    CancellationToken.None);

            seedResult.Should().Be(
                UserRegistrationPersistenceResult.Succeeded);
        }

        await using (AsyncServiceScope metadataScope =
                     _factory.Services.CreateAsyncScope())
        {
            RealEstateDbContext dbContext =
                metadataScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            dbContext.Users.Add(CreateUser(email.ToUpperInvariant()));

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
                "IX_Users_NormalizedEmail");

            dbContext.ChangeTracker.Clear();
        }

        await using (AsyncServiceScope resultScope =
                     _factory.Services.CreateAsyncScope())
        {
            IUserRepository repository =
                resultScope.ServiceProvider
                    .GetRequiredService<IUserRepository>();
            RealEstateDbContext dbContext =
                resultScope.ServiceProvider
                    .GetRequiredService<RealEstateDbContext>();

            UserRegistrationPersistenceResult result =
                await repository.PersistRegistrationAsync(
                    CreateUser(email.ToUpperInvariant()),
                    CancellationToken.None);

            result.Should().Be(
                UserRegistrationPersistenceResult
                    .NormalizedEmailAlreadyExists);
            dbContext.ChangeTracker.Entries().Should().BeEmpty();
        }

        await AssertNormalizedEmailCountAsync(
            email.ToUpperInvariant(),
            expectedCount: 1);
    }

    [Fact]
    public async Task PersistRegistration_DifferentUniqueViolation_IsRethrownWithoutClearingTracking()
    {
        User seed = CreateUser(
            $"primary-owner-{Guid.NewGuid():N}@test.com");

        await using (AsyncServiceScope seedScope =
                     _factory.Services.CreateAsyncScope())
        {
            IUserRepository seedRepository =
                seedScope.ServiceProvider
                    .GetRequiredService<IUserRepository>();

            UserRegistrationPersistenceResult seedResult =
                await seedRepository.PersistRegistrationAsync(
                    seed,
                    CancellationToken.None);

            seedResult.Should().Be(
                UserRegistrationPersistenceResult.Succeeded);
        }

        await using AsyncServiceScope conflictScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            conflictScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
        IUserRepository repository =
            conflictScope.ServiceProvider
                .GetRequiredService<IUserRepository>();

        User conflictingUser = CreateUser(
            $"primary-conflict-{Guid.NewGuid():N}@test.com");

        dbContext.Entry(conflictingUser)
            .Property(user => user.Id)
            .CurrentValue = seed.Id;

        Func<Task> act = async () =>
            await repository.PersistRegistrationAsync(
                conflictingUser,
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
        postgresException.ConstraintName.Should().Be("PK_Users");
        dbContext.ChangeTracker.Entries<User>().Should()
            .ContainSingle(entry => entry.State == EntityState.Added);

        dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PersistRegistration_UnrelatedDbUpdateException_IsRethrownWithoutClearingTracking()
    {
        var injectedException = new DbUpdateException(
            "Injected unrelated persistence failure.");

        await AssertInjectedPersistenceFailureIsRethrownAsync(
            injectedException);
    }

    [Fact]
    public async Task PersistRegistration_ForeignKeyViolation_IsRethrownWithoutClearingTracking()
    {
        var postgresException = new PostgresException(
            "Injected foreign-key failure.",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.ForeignKeyViolation);

        var injectedException = new DbUpdateException(
            "Injected foreign-key update failure.",
            postgresException);

        await AssertInjectedPersistenceFailureIsRethrownAsync(
            injectedException);
    }

    [Fact]
    public async Task Register_UnrelatedPersistenceFailure_ReturnsSanitizedUnexpectedFailure()
    {
        string connectionString = GetRequiredConnectionString();

        using var localFactory =
            new RegistrationFailureWebApplicationFactory(
                connectionString);
        using HttpClient client = localFactory.CreateClient();

        string email =
            $"unrelated-failure-{Guid.NewGuid():N}@test.com";

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            CreateRequest(email));

        string responseBody = await response.Content.ReadAsStringAsync();

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerUnexpected,
            "/api/auth/register");

        responseBody.Should().NotContainEquivalentOf("DbUpdateException");
        responseBody.Should().NotContainEquivalentOf("provider-marker");
        responseBody.Should().NotContainEquivalentOf("23505");
        responseBody.Should().NotContainEquivalentOf(
            "IX_Users_NormalizedEmail");
    }

    [Fact]
    public async Task Register_ConcurrentNormalizedEmailRace_ReturnsOneCreatedOneCanonicalConflictAndPersistsOneRow()
    {
        string connectionString = GetRequiredConnectionString();
        string email = $"race-{Guid.NewGuid():N}@test.com";
        string normalizedEmail = email.ToUpperInvariant();
        var coordinator = new RegistrationPrecheckCoordinator();

        using var localFactory =
            new RegistrationRaceWebApplicationFactory(
                connectionString,
                normalizedEmail,
                coordinator);
        using HttpClient firstClient = localFactory.CreateClient();
        using HttpClient secondClient = localFactory.CreateClient();
        using var timeoutSource =
            new CancellationTokenSource(TestTimeout);

        CancellationToken cancellationToken = timeoutSource.Token;

        Task<HttpResponseMessage> firstRequest =
            firstClient.PostAsJsonAsync(
                "/api/auth/register",
                CreateRequest(email),
                cancellationToken);
        Task<HttpResponseMessage> secondRequest =
            secondClient.PostAsJsonAsync(
                "/api/auth/register",
                CreateRequest(email.ToUpperInvariant()),
                cancellationToken);

        await coordinator.WaitForBothPrechecksAsync(cancellationToken);

        HttpResponseMessage[] responses = await Task.WhenAll(
            firstRequest,
            secondRequest);

        try
        {
            RegistrationPrecheckObservation[] observations =
                coordinator.GetObservations();

            observations.Should().HaveCount(2);
            observations.Should().OnlyContain(observation =>
                observation.NormalizedEmail == normalizedEmail &&
                !observation.EmailExisted);
            observations.Select(observation => observation.DbContextId)
                .Should().OnlyHaveUniqueItems();

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

            string successBody = await successfulResponse.Content
                .ReadAsStringAsync(cancellationToken);
            using JsonDocument successDocument =
                JsonDocument.Parse(successBody);

            successDocument.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .Should().Equal("user");

            AuthResponse? success = await successfulResponse.Content
                .ReadFromJsonAsync<AuthResponse>(cancellationToken);

            success.Should().NotBeNull();
            success!.User.Email.Should().BeOneOf(
                email,
                email.ToUpperInvariant());
            success.User.FirstName.Should().Be("Race");
            success.User.LastName.Should().Be("User");
            success.User.PhoneNumber.Should().Be("+38970123456");
            success.User.Role.Should().Be("User");
            success.User.Status.Should().Be("PendingVerification");
            successfulResponse.Headers.Location.Should().Be(
                $"/api/users/{success.User.Id}");

            string loserBody = await losingResponse.Content
                .ReadAsStringAsync(cancellationToken);

            await ApiFailureAssertions.AssertProblemAsync(
                losingResponse,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictEmailAlreadyExists,
                "/api/auth/register");

            loserBody.Should().NotContainEquivalentOf("23505");
            loserBody.Should().NotContainEquivalentOf(
                "IX_Users_NormalizedEmail");
            loserBody.Should().NotContainEquivalentOf("Npgsql");
            loserBody.Should().NotContainEquivalentOf("Postgres");
            loserBody.Should().NotContainEquivalentOf("DbUpdateException");
            loserBody.Should().NotContainEquivalentOf("SQLSTATE");
            loserBody.Should().NotContainEquivalentOf("SQL");

            await AssertNormalizedEmailCountAsync(
                normalizedEmail,
                expectedCount: 1);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    private async Task AssertInjectedPersistenceFailureIsRethrownAsync(
        DbUpdateException injectedException)
    {
        string connectionString = GetRequiredConnectionString();
        var interceptor = new ThrowingSaveChangesInterceptor(
            injectedException);
        DbContextOptions<RealEstateDbContext> options =
            new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptor)
                .Options;

        await using var dbContext = new RealEstateDbContext(options);
        var repository = new UserRepository(dbContext);
        User user = CreateUser(
            $"injected-{Guid.NewGuid():N}@test.com");

        Func<Task> act = async () =>
            await repository.PersistRegistrationAsync(
                user,
                CancellationToken.None);

        DbUpdateException thrown =
            (await act.Should().ThrowAsync<DbUpdateException>())
            .Which;

        thrown.Should().BeSameAs(injectedException);
        dbContext.ChangeTracker.Entries<User>().Should()
            .ContainSingle(entry => entry.State == EntityState.Added);
    }

    private async Task AssertNormalizedEmailCountAsync(
        string normalizedEmail,
        int expectedCount)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        int count = await dbContext.Users
            .AsNoTracking()
            .CountAsync(user =>
                user.NormalizedEmail == normalizedEmail);

        count.Should().Be(expectedCount);
    }

    private string GetRequiredConnectionString()
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The integration-test connection string is unavailable.");
    }

    private static RegisterRequest CreateRequest(string email)
    {
        return new RegisterRequest(
            email,
            "Password123!",
            "Race",
            "User",
            "+38970123456");
    }

    private static User CreateUser(string email)
    {
        return new User(
            email,
            "test-password-hash",
            "Test",
            "User",
            null);
    }

    private abstract class DelegatingUserRepository(
        IUserRepository inner)
        : IUserRepository
    {
        protected IUserRepository Inner { get; } = inner;

        public virtual Task<bool> ExistsByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Inner.ExistsByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

        public Task<User?> GetByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Inner.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

        public Task<User?> GetByNormalizedEmailReadOnlyAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Inner.GetByNormalizedEmailReadOnlyAsync(
                normalizedEmail,
                cancellationToken);

        public Task<User?> GetByIdReadOnlyAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Inner.GetByIdReadOnlyAsync(id, cancellationToken);

        public Task<User?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Inner.GetByIdForUpdateAsync(id, cancellationToken);

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken) =>
            Inner.AddAsync(user, cancellationToken);

        public virtual Task<UserRegistrationPersistenceResult>
            PersistRegistrationAsync(
                User user,
                CancellationToken cancellationToken) =>
            Inner.PersistRegistrationAsync(user, cancellationToken);

        public Task SaveChangesAsync(
            CancellationToken cancellationToken) =>
            Inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class CoordinatedUserRepository(
        IUserRepository inner,
        Guid dbContextId,
        string targetNormalizedEmail,
        RegistrationPrecheckCoordinator coordinator)
        : DelegatingUserRepository(inner)
    {
        public override async Task<bool>
            ExistsByNormalizedEmailAsync(
                string normalizedEmail,
                CancellationToken cancellationToken)
        {
            bool exists = await base.ExistsByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (!exists && string.Equals(
                    normalizedEmail,
                    targetNormalizedEmail,
                    StringComparison.Ordinal))
            {
                await coordinator.RecordAndWaitAsync(
                    new RegistrationPrecheckObservation(
                        dbContextId,
                        normalizedEmail,
                        exists),
                    cancellationToken);
            }

            return exists;
        }
    }

    private sealed class ThrowingRegistrationUserRepository(
        IUserRepository inner)
        : DelegatingUserRepository(inner)
    {
        public override Task<UserRegistrationPersistenceResult>
            PersistRegistrationAsync(
                User user,
                CancellationToken cancellationToken)
        {
            throw new DbUpdateException(
                "Injected provider-marker registration failure.");
        }
    }

    private sealed class RegistrationPrecheckCoordinator
    {
        private readonly object _sync = new();
        private readonly List<RegistrationPrecheckObservation>
            _observations = [];
        private readonly TaskCompletionSource<bool> _bothPrechecks =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RecordAndWaitAsync(
            RegistrationPrecheckObservation observation,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_observations.Count >= 2)
                {
                    throw new InvalidOperationException(
                        "Unexpected third registration precheck.");
                }

                _observations.Add(observation);

                if (_observations.Count == 2)
                {
                    _bothPrechecks.TrySetResult(true);
                }
            }

            await _bothPrechecks.Task.WaitAsync(cancellationToken);
        }

        public async Task WaitForBothPrechecksAsync(
            CancellationToken cancellationToken)
        {
            await _bothPrechecks.Task.WaitAsync(cancellationToken);
        }

        public RegistrationPrecheckObservation[] GetObservations()
        {
            lock (_sync)
            {
                return _observations.ToArray();
            }
        }
    }

    private sealed record RegistrationPrecheckObservation(
        Guid DbContextId,
        string NormalizedEmail,
        bool EmailExisted);

    private sealed class ThrowingSaveChangesInterceptor(
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

    private abstract class RegistrationWebApplicationFactory(
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

                services.RemoveAll<IUserRepository>();
                services.AddScoped<UserRepository>();

                ConfigureUserRepository(services);
            });
        }

        protected abstract void ConfigureUserRepository(
            IServiceCollection services);
    }

    private sealed class RegistrationRaceWebApplicationFactory(
        string connectionString,
        string targetNormalizedEmail,
        RegistrationPrecheckCoordinator coordinator)
        : RegistrationWebApplicationFactory(connectionString)
    {
        protected override void ConfigureUserRepository(
            IServiceCollection services)
        {
            services.AddScoped<IUserRepository>(provider =>
                new CoordinatedUserRepository(
                    provider.GetRequiredService<UserRepository>(),
                    provider.GetRequiredService<RealEstateDbContext>()
                        .ContextId.InstanceId,
                    targetNormalizedEmail,
                    coordinator));
        }
    }

    private sealed class RegistrationFailureWebApplicationFactory(
        string connectionString)
        : RegistrationWebApplicationFactory(connectionString)
    {
        protected override void ConfigureUserRepository(
            IServiceCollection services)
        {
            services.AddScoped<IUserRepository>(provider =>
                new ThrowingRegistrationUserRepository(
                    provider.GetRequiredService<UserRepository>()));
        }
    }
}
