using Microsoft.Extensions.Options;

namespace RealEstate.Infrastructure.Geocoding.Geoapify;

public sealed class GeoapifyOptionsValidator : IValidateOptions<GeoapifyOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        GeoapifyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!IsValidBaseUri(options.BaseUri))
        {
            failures.Add(
                "Geoapify BaseUri must be an absolute HTTPS origin with no path, query, fragment, or user information.");
        }

        if (!IsValidApiKey(options.ApiKey))
        {
            failures.Add(
                "Geoapify ApiKey must be supplied outside committed configuration and contain valid non-whitespace text without control characters.");
        }

        if (options.CandidateLimit < GeoapifyOptions.MinimumCandidateLimit ||
            options.CandidateLimit > GeoapifyOptions.MaximumCandidateLimit)
        {
            failures.Add(
                $"Geoapify CandidateLimit must be between {GeoapifyOptions.MinimumCandidateLimit} and {GeoapifyOptions.MaximumCandidateLimit}.");
        }

        if (options.OperationTimeoutSeconds <
                GeoapifyOptions.MinimumOperationTimeoutSeconds ||
            options.OperationTimeoutSeconds >
                GeoapifyOptions.MaximumOperationTimeoutSeconds)
        {
            failures.Add(
                $"Geoapify OperationTimeoutSeconds must be between {GeoapifyOptions.MinimumOperationTimeoutSeconds} and {GeoapifyOptions.MaximumOperationTimeoutSeconds}.");
        }

        if (options.MaxRetryAttempts <
                GeoapifyOptions.MinimumMaxRetryAttempts ||
            options.MaxRetryAttempts >
                GeoapifyOptions.MaximumMaxRetryAttempts)
        {
            failures.Add(
                $"Geoapify MaxRetryAttempts must be between {GeoapifyOptions.MinimumMaxRetryAttempts} and {GeoapifyOptions.MaximumMaxRetryAttempts}.");
        }

        if (options.RetryDelayMilliseconds <
                GeoapifyOptions.MinimumRetryDelayMilliseconds ||
            options.RetryDelayMilliseconds >
                GeoapifyOptions.MaximumRetryDelayMilliseconds)
        {
            failures.Add(
                $"Geoapify RetryDelayMilliseconds must be between {GeoapifyOptions.MinimumRetryDelayMilliseconds} and {GeoapifyOptions.MaximumRetryDelayMilliseconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidBaseUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
               uri.Scheme.Equals(
                   Uri.UriSchemeHttps,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsValidApiKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            !GeoapifyText.IsWellFormed(value))
        {
            return false;
        }

        return !value.Any(char.IsControl);
    }
}
