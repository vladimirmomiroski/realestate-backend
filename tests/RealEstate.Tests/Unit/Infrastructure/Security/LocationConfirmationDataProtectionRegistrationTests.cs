using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Geocoding.Tokens;
using RealEstate.Infrastructure.Security;

namespace RealEstate.Tests.Unit.Infrastructure.Security;

public sealed class LocationConfirmationDataProtectionRegistrationTests
{
    [Fact]
    public void TestingRegistration_UsesIsolatedTokenInfrastructure()
    {
        using ServiceProvider services = CreateServices(
            "Testing",
            Configuration());
        ILocationConfirmationTokenProtector protector = services
            .GetRequiredService<ILocationConfirmationTokenProtector>();

        string token = protector.Protect(CreateClaims());
        LocationConfirmationTokenUnprotectResult result =
            protector.Unprotect(token);

        result.Succeeded.Should().BeTrue();
        services.GetRequiredService<TimeProvider>()
            .Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public void DevelopmentRegistration_PersistsKeysAcrossRecreatedContainers()
    {
        using TempDirectory keyRing = TempDirectory.Create();
        IConfiguration configuration = Configuration(
            (LocationConfirmationDataProtectionRegistration
                .KeyRingPathConfigurationKey, keyRing.Path));
        string token;

        using (ServiceProvider first = CreateServices(
                   "Development",
                   configuration))
        {
            token = first
                .GetRequiredService<ILocationConfirmationTokenProtector>()
                .Protect(CreateClaims());
        }

        using ServiceProvider second = CreateServices(
            "Development",
            configuration);
        LocationConfirmationTokenUnprotectResult result = second
            .GetRequiredService<ILocationConfirmationTokenProtector>()
            .Unprotect(token);

        result.Succeeded.Should().BeTrue();
        Directory.GetFiles(keyRing.Path, "key-*.xml")
            .Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DeploymentRegistration_RequiresConfiguredSharedKeyRing(
        string environmentName)
    {
        var services = new ServiceCollection();

        Action act = () => services
            .AddLocationConfirmationDataProtection(
                Configuration(),
                environmentName,
                _ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DataProtection:KeyRingPath*");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DeploymentRegistration_RejectsRelativeKeyRingPath(
        string environmentName)
    {
        var services = new ServiceCollection();
        IConfiguration configuration = Configuration(
            (LocationConfirmationDataProtectionRegistration
                .KeyRingPathConfigurationKey, "relative/key-ring"));

        Action act = () => services
            .AddLocationConfirmationDataProtection(
                configuration,
                environmentName,
                _ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*fully qualified*");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DeploymentRegistration_RequiresAtRestProtectionIntegration(
        string environmentName)
    {
        using TempDirectory keyRing = TempDirectory.Create();
        var services = new ServiceCollection();
        IConfiguration configuration = Configuration(
            (LocationConfirmationDataProtectionRegistration
                .KeyRingPathConfigurationKey, keyRing.Path));

        Action act = () => services
            .AddLocationConfirmationDataProtection(
                configuration,
                environmentName);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*key encryption at rest*");
    }

    [Fact]
    public void Registration_RejectsUnrecognizedEnvironment()
    {
        var services = new ServiceCollection();

        Action act = () => services
            .AddLocationConfirmationDataProtection(
                Configuration(),
                "Unknown");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*explicit Testing, Development, Staging, or Production*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Registration_RejectsOutOfRangeTokenLifetime(
        int lifetimeMinutes)
    {
        IConfiguration configuration = Configuration(
            ($"{LocationConfirmationTokenOptions.SectionName}:LifetimeMinutes",
                lifetimeMinutes.ToString()));
        using ServiceProvider services = CreateServices(
            "Testing",
            configuration);

        Action act = () => services
            .GetRequiredService<ILocationConfirmationTokenProtector>();

        act.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider CreateServices(
        string environmentName,
        IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocationConfirmationDataProtection(
            configuration,
            environmentName);
        return services.BuildServiceProvider();
    }

    private static IConfiguration Configuration(
        params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.ToDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value))
            .Build();
    }

    private static LocationConfirmationTokenClaims CreateClaims()
    {
        string fingerprint = ListingLocationFingerprint.Compute(
            CanonicalListingLocation.From(
            [
                new CanonicalListingLocationInput(
                    "en",
                    "Skopje",
                    "Centar",
                    "Macedonia 10",
                    null)
            ]));

        return LocationConfirmationTokenClaims.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "provider",
            "reference",
            "en",
            fingerprint);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "RealEstate.Tests",
                nameof(LocationConfirmationDataProtectionRegistrationTests),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
