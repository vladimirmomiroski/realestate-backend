using Xunit;

namespace RealEstate.Tests.Integration.Api;

[CollectionDefinition(
    Name,
    DisableParallelization = true)]
public sealed class CorsAndStaticMediaTestCollection
    : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name =
        "CORS and static media integration tests";
}
