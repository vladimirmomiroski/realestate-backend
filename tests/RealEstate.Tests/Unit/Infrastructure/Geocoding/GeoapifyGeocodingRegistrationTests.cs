using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Infrastructure.Geocoding.Geoapify;

namespace RealEstate.Tests.Unit.Infrastructure.Geocoding;

public sealed class GeoapifyGeocodingRegistrationTests
{
    [Fact]
    public async Task Registration_WithValidConfigurationStartsAndSelectsGeoapifyAdapter()
    {
        using IHost host = CreateHost(new Dictionary<string, string?>
        {
            ["Geoapify:ApiKey"] = "test-only-key"
        });

        await host.StartAsync(CancellationToken.None);

        GeoapifyOptions options = host.Services
            .GetRequiredService<IOptions<GeoapifyOptions>>()
            .Value;
        IListingGeocoder geocoder = host.Services
            .GetRequiredService<IListingGeocoder>();

        options.BaseUri.Should().Be(GeoapifyOptions.DefaultBaseUri);
        options.CandidateLimit.Should().Be(GeoapifyOptions.DefaultCandidateLimit);
        geocoder.Should().BeOfType<GeoapifyListingGeocoder>();

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Registration_MissingApiKeyFailsStartup()
    {
        using IHost host = CreateHost(
            new Dictionary<string, string?>());

        Func<Task> act = () => host.StartAsync(
            CancellationToken.None);

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().Contain(failure =>
                failure.Contains("ApiKey", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://api.geoapify.test/")]
    [InlineData("https://user@api.geoapify.test/")]
    [InlineData("https://api.geoapify.test/path/")]
    [InlineData("https://api.geoapify.test/?query=value")]
    [InlineData("relative")]
    public async Task Registration_InvalidBaseUriFailsStartup(string baseUri)
    {
        using IHost host = CreateHost(new Dictionary<string, string?>
        {
            ["Geoapify:ApiKey"] = "test-only-key",
            ["Geoapify:BaseUri"] = baseUri
        });

        Func<Task> act = () => host.StartAsync(
            CancellationToken.None);

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().Contain(failure =>
                failure.Contains("BaseUri", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Registration_InvalidCandidateLimitFailsStartup(int limit)
    {
        using IHost host = CreateHost(new Dictionary<string, string?>
        {
            ["Geoapify:ApiKey"] = "test-only-key",
            ["Geoapify:CandidateLimit"] = limit.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        });

        Func<Task> act = () => host.StartAsync(
            CancellationToken.None);

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().Contain(failure =>
                failure.Contains("CandidateLimit", StringComparison.Ordinal));
    }

    [Fact]
    public void CommittedApplicationConfigurationContainsNoGeoapifyApiKey()
    {
        string repositoryRoot = FindRepositoryRoot();
        string path = Path.Combine(
            repositoryRoot,
            "src",
            "RealEstate.Api",
            "appsettings.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement geoapify = document.RootElement.GetProperty("Geoapify");

        geoapify.TryGetProperty("ApiKey", out _).Should().BeFalse();
        geoapify.GetProperty("BaseUri").GetString()
            .Should().Be(GeoapifyOptions.DefaultBaseUri);
    }

    private static IHost CreateHost(
        IDictionary<string, string?> configurationValues)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddGeoapifyGeocoding(builder.Configuration);
        return builder.Build();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RealEstate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            "Could not locate the repository root.");
    }
}
