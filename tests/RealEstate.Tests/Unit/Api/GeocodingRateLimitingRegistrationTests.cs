using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealEstate.Api.RateLimiting;

namespace RealEstate.Tests.Unit.Api;

public sealed class GeocodingRateLimitingRegistrationTests
{
    [Fact]
    public void Defaults_AreApplicationOwnedAndRejectImmediatelyAfterTenPermits()
    {
        using ServiceProvider services = CreateServices(Configuration());
        GeocodingRateLimitOptions options = services
            .GetRequiredService<IOptions<GeocodingRateLimitOptions>>()
            .Value;

        options.PermitLimit.Should().Be(10);
        options.WindowSeconds.Should().Be(60);

        using RateLimiter limiter = services
            .GetRequiredService<IGeocodingActorRateLimiterFactory>()
            .Create();

        for (int attempt = 0; attempt < options.PermitLimit; attempt++)
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            lease.IsAcquired.Should().BeTrue();
        }

        using RateLimitLease rejected = limiter.AttemptAcquire();
        rejected.IsAcquired.Should().BeFalse();
        rejected.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter)
            .Should().BeTrue();
        retryAfter.Should().BeGreaterThan(TimeSpan.Zero)
            .And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1001, 60)]
    [InlineData(10, 0)]
    [InlineData(10, 3601)]
    public void InvalidConfiguredBounds_FailOptionsValidation(
        int permitLimit,
        int windowSeconds)
    {
        using ServiceProvider services = CreateServices(
            Configuration(
                ($"{GeocodingRateLimitOptions.SectionName}:PermitLimit",
                    permitLimit.ToString()),
                ($"{GeocodingRateLimitOptions.SectionName}:WindowSeconds",
                    windowSeconds.ToString())));

        Action act = () =>
        {
            _ = services
                .GetRequiredService<IOptions<GeocodingRateLimitOptions>>()
                .Value;
        };

        act.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider CreateServices(
        IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGeocodingRateLimiting(configuration);
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
}
