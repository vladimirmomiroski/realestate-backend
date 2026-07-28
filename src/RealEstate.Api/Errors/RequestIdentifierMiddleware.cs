namespace RealEstate.Api.Errors;

internal sealed class RequestIdentifierMiddleware
{
    public const string HeaderName = "X-Request-ID";

    private readonly RequestDelegate _next;

    public RequestIdentifierMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (ApiRequestPath.NeedsRequestIdentifier(httpContext.Request.Path))
        {
            httpContext.Response.OnStarting(
                static state =>
                {
                    var context = (HttpContext)state;
                    context.Response.Headers[HeaderName] =
                        context.TraceIdentifier;

                    return Task.CompletedTask;
                },
                httpContext);
        }

        await _next(httpContext);
    }
}
