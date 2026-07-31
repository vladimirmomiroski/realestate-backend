using Xunit;

namespace RealEstate.Tests.Integration.Api;

[CollectionDefinition(
    Name,
    DisableParallelization = true)]
public sealed class HealthEndpointTestCollection
    : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name =
        "Health endpoint integration tests";
}
