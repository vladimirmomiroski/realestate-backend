using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;

namespace RealEstate.Infrastructure.Geocoding.Geoapify;

public static class GeoapifyGeocodingRegistration
{
    public static IServiceCollection AddGeoapifyGeocoding(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<
            IValidateOptions<GeoapifyOptions>,
            GeoapifyOptionsValidator>();

        services
            .AddOptions<GeoapifyOptions>()
            .Bind(configuration.GetSection(GeoapifyOptions.SectionName))
            .ValidateOnStart();

        services
            .AddHttpClient<IListingGeocoder, GeoapifyListingGeocoder>(
                (serviceProvider, client) =>
                {
                    GeoapifyOptions options = serviceProvider
                        .GetRequiredService<IOptions<GeoapifyOptions>>()
                        .Value;

                    client.BaseAddress = new Uri(
                        options.BaseUri,
                        UriKind.Absolute);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
            .RemoveAllLoggers();

        return services;
    }
}
