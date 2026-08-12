using Microsoft.AspNetCore.Diagnostics;
using RealEstate.Application.Listings.Mappings;

namespace RealEstate.Api.Errors;

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    internal static readonly EventId HandledExceptionEvent = new(
        12002,
        "ApiUnexpectedExceptionHandled");

    private static readonly Action<
        ILogger,
        string,
        string,
        string,
        int,
        Exception?> LogHandledException = LoggerMessage.Define<
            string,
            string,
            string,
            int>(
                LogLevel.Error,
                HandledExceptionEvent,
                "Handled unexpected exception for API request {RequestId} " +
                "{Method} {Route} with status {StatusCode}.");

    private static readonly Action<
        ILogger,
        string,
        string,
        string,
        int,
        Guid,
        string,
        Exception?> LogPublicListingIntegrityException = LoggerMessage.Define<
            string,
            string,
            string,
            int,
            Guid,
            string>(
                LogLevel.Error,
                HandledExceptionEvent,
                "Handled public-listing integrity violation for API request " +
                "{RequestId} {Method} {Route} with status {StatusCode}. " +
                "Listing {ListingId}; violations {IntegrityViolationCodes}.");

    private readonly ApiFailureService _failureService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(
        ApiFailureService failureService,
        ILogger<ApiExceptionHandler> logger)
    {
        _failureService = failureService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestPath.IsApi(httpContext.Request.Path) ||
            httpContext.RequestAborted.IsCancellationRequested ||
            exception is OperationCanceledException &&
            cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        bool written = await _failureService.TryWriteAsync(
            httpContext,
            _failureService.Create(
                httpContext,
                ApiFailureDescriptor.Unexpected));

        if (!written)
        {
            return false;
        }

        ApiRequestLogContext context =
            ApiRequestLogContext.Create(httpContext);

        if (exception is PublicListingIntegrityException integrityException)
        {
            string violationCodes = string.Join(
                ",",
                integrityException.Violations
                    .Select(violation => violation.Code)
                    .Distinct()
                    .Order());

            LogPublicListingIntegrityException(
                _logger,
                context.RequestId,
                context.Method,
                context.Route,
                context.StatusCode,
                integrityException.ListingId,
                violationCodes,
                exception);
        }
        else
        {
            LogHandledException(
                _logger,
                context.RequestId,
                context.Method,
                context.Route,
                context.StatusCode,
                exception);
        }

        return true;
    }
}
