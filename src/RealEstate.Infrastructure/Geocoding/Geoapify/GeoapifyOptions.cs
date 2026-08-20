namespace RealEstate.Infrastructure.Geocoding.Geoapify;

public sealed class GeoapifyOptions
{
    public const string SectionName = "Geoapify";
    public const string DefaultBaseUri = "https://api-eu.geoapify.com/";
    public const int DefaultCandidateLimit = 5;
    public const int MinimumCandidateLimit = 1;
    public const int MaximumCandidateLimit = 20;
    public const int DefaultOperationTimeoutSeconds = 10;
    public const int MinimumOperationTimeoutSeconds = 1;
    public const int MaximumOperationTimeoutSeconds = 30;
    public const int DefaultMaxRetryAttempts = 1;
    public const int MinimumMaxRetryAttempts = 0;
    public const int MaximumMaxRetryAttempts = 1;
    public const int DefaultRetryDelayMilliseconds = 200;
    public const int MinimumRetryDelayMilliseconds = 0;
    public const int MaximumRetryDelayMilliseconds = 1000;

    public string BaseUri { get; set; } = DefaultBaseUri;

    public string ApiKey { get; set; } = string.Empty;

    public int CandidateLimit { get; set; } = DefaultCandidateLimit;

    public int OperationTimeoutSeconds { get; set; } =
        DefaultOperationTimeoutSeconds;

    public int MaxRetryAttempts { get; set; } = DefaultMaxRetryAttempts;

    public int RetryDelayMilliseconds { get; set; } =
        DefaultRetryDelayMilliseconds;
}
