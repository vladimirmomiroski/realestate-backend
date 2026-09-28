using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class SuccessorComparisonRunVerifierTests
{
    [Fact]
    public void PostgreSql16SuccessorRawDescriptor_IsAccepted()
    {
        var descriptor = new QueryReviewArtifactDescriptor(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId),
            QueryReviewArtifactKind.RawRun,
            "comparison");

        Action act = () => SuccessorComparisonRunVerifier.ValidateComparisonDescriptor(descriptor);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("wrong-lane")]
    [InlineData("historical")]
    [InlineData("not-raw")]
    public void WrongLaneGenerationOrArtifactClass_IsRejected(string drift)
    {
        QueryReviewGenerationDefinition generation = drift == "historical"
            ? QueryReviewGenerations.FrozenHistorical
            : QueryReviewGenerations.FourRootDiscovery;
        QueryReviewLaneDefinition lane = drift switch
        {
            "wrong-lane" => QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId),
            "historical" => generation.Lanes.Single(),
            _ => QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId)
        };
        var descriptor = new QueryReviewArtifactDescriptor(
            generation,
            lane,
            drift == "not-raw"
                ? QueryReviewArtifactKind.ExperimentalBundle
                : QueryReviewArtifactKind.RawRun,
            "comparison");

        Action act = () => SuccessorComparisonRunVerifier.ValidateComparisonDescriptor(descriptor);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*sealed*PostgreSQL 16 raw run*");
    }

    [Fact]
    public async Task NonexistentComparisonDirectory_IsRejectedByRealArtifactBoundary()
    {
        string absent = Path.Combine(
            Path.GetTempPath(),
            $"queryreview-absent-{Guid.NewGuid():N}");
        var primaryDescriptor = new QueryReviewArtifactDescriptor(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId),
            QueryReviewArtifactKind.RawRun,
            absent);

        Func<Task> act = () => SuccessorComparisonRunVerifier.VerifyAsync(
            primaryDescriptor,
            null!,
            absent);

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task IncompleteRecordedRawComparison_IsRejectedByRealVerificationBoundary()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"queryreview-incomplete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "manifest.json"),
                """
                {
                  "baselineRunId": "incomplete",
                  "profileVersion": "four-root-discovery-v1",
                  "postgreSqlVersion": "16.14",
                  "generationId": "four-root-discovery-v1",
                  "laneId": "postgresql-16",
                  "capturedCommandsPath": "captured-commands.json",
                  "environmentPath": "environment-raw.json"
                }
                """);
            QueryReviewArtifactDescriptor primaryDescriptor = new(
                QueryReviewGenerations.FourRootDiscovery,
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql184LaneId),
                QueryReviewArtifactKind.RawRun,
                directory);

            Func<Task> act = () => SuccessorComparisonRunVerifier.VerifyAsync(
                primaryDescriptor,
                null!,
                directory);

            await act.Should().ThrowAsync<BaselinePlanValidationException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("profile-identity")]
    [InlineData("invariant-manifest")]
    [InlineData("invariant-result")]
    [InlineData("source")]
    [InlineData("manifest")]
    [InlineData("sql")]
    [InlineData("parameter")]
    [InlineData("result")]
    [InlineData("order")]
    [InlineData("settings")]
    [InlineData("samples")]
    public void ComparisonIdentityDrift_IsRejected(string drift)
    {
        SuccessorRunComparisonIdentity primary = CreateIdentity(
            QueryReviewGenerations.PostgreSql184LaneId);
        SuccessorRunComparisonIdentity comparison = CreateIdentity(
            QueryReviewGenerations.PostgreSql16LaneId);
        comparison = drift switch
        {
            "profile" => comparison with { ProfileSha256 = new string('0', 64) },
            "profile-identity" => comparison with { ProfileIdentity = "other-profile" },
            "invariant-manifest" => comparison with { InvariantManifestSha256 = new string('0', 64) },
            "invariant-result" => comparison with { InvariantResultSha256 = new string('0', 64) },
            "source" => comparison with { GitCommit = new string('0', 40) },
            "manifest" => comparison with { QueryShapeManifestSha256 = new string('0', 64) },
            "sql" => comparison with { SqlIdentitySha256 = new string('0', 64) },
            "parameter" => comparison with { ParameterIdentitySha256 = new string('0', 64) },
            "result" => comparison with { ResultIdentitySha256 = new string('0', 64) },
            "order" => comparison with { OrderIdentitySha256 = new string('0', 64) },
            "settings" => comparison with { SettingsIdentitySha256 = new string('0', 64) },
            "samples" => comparison with { MeasuredRunsPerCommand = 4 },
            _ => throw new InvalidOperationException()
        };

        Action act = () => SuccessorComparisonRunVerifier.ValidateCompatible(primary, comparison);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*does not exactly match*");
    }

    [Fact]
    public void ExactPostgreSql16ComparisonIdentity_IsAccepted()
    {
        Action act = () => SuccessorComparisonRunVerifier.ValidateCompatible(
            CreateIdentity(QueryReviewGenerations.PostgreSql184LaneId),
            CreateIdentity(QueryReviewGenerations.PostgreSql16LaneId));

        act.Should().NotThrow();
    }

    private static SuccessorRunComparisonIdentity CreateIdentity(string laneId) => new(
        QueryReviewGenerations.FourRootDiscoveryId,
        QueryReviewGenerations.FourRootDiscoveryId,
        laneId,
        "94490b001946f3ffe68e03a929e62e2c6d281993",
        SuccessorPermanentExportManifest.AcceptedProfileSha256,
        SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256,
        SuccessorPermanentExportManifest.AcceptedInvariantResultSha256,
        DiscoveryQueryShapeManifest.ExpectedSuccessorManifestSha256,
        new string('1', 64),
        new string('2', 64),
        new string('3', 64),
        new string('4', 64),
        new string('5', 64),
        83,
        190,
        1,
        5,
        498);
}
