using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.RateLimiting;

internal interface IGeocodingActorRateLimiterFactory
{
    RateLimiter Create();
}

internal sealed class GeocodingActorRateLimiterFactory
    : IGeocodingActorRateLimiterFactory
{
    private readonly GeocodingRateLimitOptions _options;

    public GeocodingActorRateLimiterFactory(
        IOptions<GeocodingRateLimitOptions> options)
    {
        _options = options.Value;
    }

    public RateLimiter Create()
    {
        return new FixedWindowRateLimiter(
            new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = _options.PermitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = TimeSpan.FromSeconds(_options.WindowSeconds)
            });
    }
}
