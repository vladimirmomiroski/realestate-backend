using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RealEstate.Api.RateLimiting;

internal static class GeocodingRateLimitingRegistration
{
    private const int MaximumPermitLimit = 1_000;
    private const int MaximumWindowSeconds = 3_600;

    public static IServiceCollection AddGeocodingRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GeocodingRateLimitOptions>()
            .Bind(configuration.GetSection(
                GeocodingRateLimitOptions.SectionName))
            .Validate(
                options => options.PermitLimit is >= 1 and <= MaximumPermitLimit,
                $"{GeocodingRateLimitOptions.SectionName}:PermitLimit must be " +
                $"between 1 and {MaximumPermitLimit}.")
            .Validate(
                options => options.WindowSeconds is >= 1 and <= MaximumWindowSeconds,
                $"{GeocodingRateLimitOptions.SectionName}:WindowSeconds must be " +
                $"between 1 and {MaximumWindowSeconds}.")
            .ValidateOnStart();

        services.TryAddSingleton<
            IGeocodingActorRateLimiterFactory,
            GeocodingActorRateLimiterFactory>();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, GeocodingRateLimitPolicy>(
                GeocodingRateLimitPolicy.Name);
        });

        return services;
    }
}
