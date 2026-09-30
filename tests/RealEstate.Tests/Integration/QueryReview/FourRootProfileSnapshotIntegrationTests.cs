using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RealEstate.Infrastructure.Persistence;
using RealEstate.QueryReview;
using Testcontainers.PostgreSql;

namespace RealEstate.Tests.Integration.QueryReview;

public sealed class FourRootProfileSnapshotIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("realestate_queryreview_snapshot_test")
            .WithUsername("postgres")
            .WithPassword("queryreview_snapshot_disposable")
            .WithAutoRemove(true)
            .Build();

    private NpgsqlConnection? _connection;
    private ProfileVerificationResult? _verification;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        string connectionString = _postgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<RealEstateDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var dbContext = new RealEstateDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        _connection = new NpgsqlConnection(connectionString);
        await _connection.OpenAsync();

        ProfileVerificationResult seeded =
            await DeterministicProfileSeeder.CreateFourRootDiscoveryAsync(_connection);
        seeded.EnsureValid();
        await DeterministicProfileSeeder.NormalizePhysicalProfileAsync(_connection);

        _verification = await FourRootProfileInvariants.VerifyAsync(_connection);
        _verification.EnsureValid();
    }

    [Fact]
    public async Task VerifiedProfile_ProvidesMeasuredHashesBeforeFirstRawRunValidation()
    {
        _connection.Should().NotBeNull();
        _verification.Should().NotBeNull();

        DeterministicProfileVerificationSnapshot snapshot =
            await RealEstate.QueryReview.Program.CreateProfileVerificationSnapshotAsync(
                _connection!,
                _verification!,
                QueryReviewGenerations.FourRootDiscovery);

        snapshot.InvariantPassed.Should().Be(179);
        snapshot.InvariantFailed.Should().Be(0);
        snapshot.ProfileSha256.Should().NotBeNullOrWhiteSpace();
        snapshot.InvariantManifestSha256.Should().NotBeNullOrWhiteSpace();
        snapshot.InvariantResultSha256.Should().NotBeNullOrWhiteSpace();

        Action firstRawRunValidation = () => ExplainRunner.ValidateProfileVerification(
            DiscoveryQueryShapeManifest.GetDefinition(
                QueryReviewGenerations.FourRootDiscovery),
            snapshot);

        firstRawRunValidation.Should().NotThrow();
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await _postgresContainer.DisposeAsync();
    }
}
