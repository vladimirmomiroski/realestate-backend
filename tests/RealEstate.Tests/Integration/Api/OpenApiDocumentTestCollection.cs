using Xunit;

namespace RealEstate.Tests.Integration.Api;

[CollectionDefinition(
    Name,
    DisableParallelization = true)]
public sealed class OpenApiDocumentTestCollection
    : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name =
        "OpenAPI document integration tests";
}
