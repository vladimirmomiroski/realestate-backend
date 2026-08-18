using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using RealEstate.Api.Errors;

namespace RealEstate.Api.RateLimiting;

internal sealed class GeocodingRateLimitPolicy : IRateLimiterPolicy<string>
{
    public const string Name = "GeocodingActor";

    private const string UnresolvedActorPartition = "unresolved-actor";

    private readonly IGeocodingActorRateLimiterFactory _limiterFactory;
    private readonly ApiFailureService _failureService;

    public GeocodingRateLimitPolicy(
        IGeocodingActorRateLimiterFactory limiterFactory,
        ApiFailureService failureService)
    {
        _limiterFactory = limiterFactory;
        _failureService = failureService;
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        HandleRejectedAsync;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return RateLimitPartition.GetNoLimiter(UnresolvedActorPartition);
        }

        string? actorClaim = httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(actorClaim, out Guid actorId) || actorId == Guid.Empty)
        {
            return RateLimitPartition.GetNoLimiter(UnresolvedActorPartition);
        }

        return RateLimitPartition.Get(
            actorId.ToString("D"),
            _ => _limiterFactory.Create());
    }

    private async ValueTask HandleRejectedAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter))
        {
            long retryAfterSeconds = checked((long)Math.Ceiling(
                Math.Max(0, retryAfter.TotalSeconds)));

            context.HttpContext.Response.Headers["Retry-After"] =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }

        await _failureService.TryWriteAsync(
            context.HttpContext,
            _failureService.Create(
                context.HttpContext,
                ApiFailureDescriptor.GeocodingRateLimitExceeded));
    }
}
