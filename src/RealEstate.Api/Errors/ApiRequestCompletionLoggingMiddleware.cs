using System.Diagnostics;

namespace RealEstate.Api.Errors;

internal sealed class ApiRequestCompletionLoggingMiddleware
{
    internal static readonly EventId CompletionEvent = new(
        12001,
        "ApiRequestCompleted");

    private static readonly Action<
        ILogger,
        string,
        string,
        string,
        int,
        double,
        Exception?> LogCompletion = LoggerMessage.Define<
            string,
            string,
            string,
            int,
            double>(
                LogLevel.Information,
                CompletionEvent,
                "API request {RequestId} {Method} {Route} completed with " +
                "{StatusCode} in {ElapsedMilliseconds} ms.");

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiRequestCompletionLoggingMiddleware> _logger;

    public ApiRequestCompletionLoggingMiddleware(
        RequestDelegate next,
        ILogger<ApiRequestCompletionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (!ApiRequestPath.IsApi(httpContext.Request.Path))
        {
            await _next(httpContext);
            return;
        }

        using IDisposable? scope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["RequestId"] = httpContext.TraceIdentifier
            });

        long startedAt = Stopwatch.GetTimestamp();

        await _next(httpContext);

        ApiRequestLogContext context =
            ApiRequestLogContext.Create(httpContext);

        LogCompletion(
            _logger,
            context.RequestId,
            context.Method,
            context.Route,
            context.StatusCode,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            null);
    }
}
