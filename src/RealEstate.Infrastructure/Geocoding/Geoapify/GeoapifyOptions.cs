namespace RealEstate.Infrastructure.Geocoding.Geoapify;

public sealed class GeoapifyOptions
{
    public const string SectionName = "Geoapify";
    public const string DefaultBaseUri = "https://api-eu.geoapify.com/";
    public const int DefaultCandidateLimit = 5;
    public const int MinimumCandidateLimit = 1;
    public const int MaximumCandidateLimit = 20;

    public string BaseUri { get; set; } = DefaultBaseUri;

    public string ApiKey { get; set; } = string.Empty;

    public int CandidateLimit { get; set; } = DefaultCandidateLimit;
}
