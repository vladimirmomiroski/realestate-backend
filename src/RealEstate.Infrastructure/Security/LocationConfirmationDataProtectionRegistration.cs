using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstate.Application.Listings.Geocoding.Tokens;

namespace RealEstate.Infrastructure.Security;

public static class LocationConfirmationDataProtectionRegistration
{
    public const string KeyRingPathConfigurationKey =
        "DataProtection:KeyRingPath";

    private const string ApplicationDiscriminatorPrefix =
        "RealEstate.Api.LocationConfirmationToken";

    public static IServiceCollection AddLocationConfirmationDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName,
        Action<IDataProtectionBuilder>? configureKeyEncryptionAtRest = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string canonicalEnvironment = CanonicalizeEnvironment(environmentName);

        services
            .AddOptions<LocationConfirmationTokenOptions>()
            .Bind(configuration.GetSection(
                LocationConfirmationTokenOptions.SectionName))
            .Validate(
                options => options.LifetimeMinutes >=
                           LocationConfirmationTokenOptions.MinimumLifetimeMinutes &&
                           options.LifetimeMinutes <=
                           LocationConfirmationTokenOptions.MaximumLifetimeMinutes,
                $"Token lifetime must be between {LocationConfirmationTokenOptions.MinimumLifetimeMinutes} and {LocationConfirmationTokenOptions.MaximumLifetimeMinutes} minutes.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        IDataProtectionBuilder dataProtectionBuilder = services
            .AddDataProtection()
            .SetApplicationName(
                $"{ApplicationDiscriminatorPrefix}:{canonicalEnvironment}");

        if (canonicalEnvironment == "Testing")
        {
            dataProtectionBuilder.UseEphemeralDataProtectionProvider();
        }
        else if (canonicalEnvironment == "Development")
        {
            string keyRingPath = GetDevelopmentKeyRingPath(configuration);
            Directory.CreateDirectory(keyRingPath);
            dataProtectionBuilder.PersistKeysToFileSystem(
                new DirectoryInfo(keyRingPath));
        }
        else
        {
            string keyRingPath = GetRequiredDeploymentKeyRingPath(
                configuration,
                canonicalEnvironment);

            if (configureKeyEncryptionAtRest is null)
            {
                throw new InvalidOperationException(
                    $"{canonicalEnvironment} Data Protection requires deployment-supplied key encryption at rest.");
            }

            Directory.CreateDirectory(keyRingPath);
            dataProtectionBuilder.PersistKeysToFileSystem(
                new DirectoryInfo(keyRingPath));
            configureKeyEncryptionAtRest(dataProtectionBuilder);
        }

        services.AddSingleton<
            ILocationConfirmationTokenProtector,
            DataProtectionLocationConfirmationTokenProtector>();

        return services;
    }

    private static string CanonicalizeEnvironment(string environmentName)
    {
        if (environmentName.Equals(
                "Testing",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Testing";
        }

        if (environmentName.Equals(
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Development";
        }

        if (environmentName.Equals(
                "Staging",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Staging";
        }

        if (environmentName.Equals(
                "Production",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Production";
        }

        throw new InvalidOperationException(
            "Data Protection requires an explicit Testing, Development, Staging, or Production environment.");
    }

    private static string GetDevelopmentKeyRingPath(
        IConfiguration configuration)
    {
        string? configuredPath =
            configuration[KeyRingPathConfigurationKey];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return GetFullyQualifiedPath(configuredPath, "Development");
        }

        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Development Data Protection requires a persistent local application-data directory.");
        }

        return Path.Combine(
            localApplicationData,
            "RealEstate.Api",
            "DataProtection-Keys",
            "Development");
    }

    private static string GetRequiredDeploymentKeyRingPath(
        IConfiguration configuration,
        string environmentName)
    {
        string? configuredPath =
            configuration[KeyRingPathConfigurationKey];

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"{environmentName} Data Protection requires configuration '{KeyRingPathConfigurationKey}'.");
        }

        return GetFullyQualifiedPath(configuredPath, environmentName);
    }

    private static string GetFullyQualifiedPath(
        string configuredPath,
        string environmentName)
    {
        if (!Path.IsPathFullyQualified(configuredPath))
        {
            throw new InvalidOperationException(
                $"{environmentName} Data Protection key-ring path must be fully qualified.");
        }

        return Path.GetFullPath(configuredPath);
    }
}
