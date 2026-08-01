using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace RealEstate.Api.Errors;

internal readonly record struct ApiRequestLogContext(
    string RequestId,
    string Method,
    string Route,
    int StatusCode)
{
    public const string UnmatchedRoute = "unmatched";

    public static ApiRequestLogContext Create(HttpContext httpContext)
    {
        Endpoint? endpoint = httpContext.GetEndpoint()
            ?? httpContext.Features
                .Get<IExceptionHandlerFeature>()?
                .Endpoint;

        string? routeTemplate = (endpoint as RouteEndpoint)?
            .RoutePattern
            .RawText;

        return new ApiRequestLogContext(
            httpContext.TraceIdentifier,
            httpContext.Request.Method,
            string.IsNullOrWhiteSpace(routeTemplate)
                ? UnmatchedRoute
                : routeTemplate,
            httpContext.Response.StatusCode);
    }
}
