using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using RealEstate.QueryReview;
using RealEstate.Infrastructure.Persistence;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class SuccessorPermanentExportManifestTests
{
    [Fact]
    public void AcceptedContract_LocksFinalNoIndexSchemaAndIdentities()
    {
        SuccessorPermanentExportContract contract = SuccessorPermanentExportManifest.Accepted;

        contract.GenerationId.Should().Be(QueryReviewGenerations.FourRootDiscoveryId);
        contract.ProfileIdentity.Should().Be(QueryReviewGenerations.FourRootDiscoveryId);
        contract.DispositionSha256.Should().Be(
            "dda685aa1e6053424728cf70dfe8a1a9fc2b9956e8de5c7bd24370cb00c01800");
        contract.CommercialDisposition.Should().Be("NO_INDEX");
        contract.LandDisposition.Should().Be("NO_INDEX");
        contract.MigrationCount.Should().Be(21);
        contract.SourceLineageCommit.Should().Be(
            "ae89a343a3c41a9c1e7f6f5c4f00fe4d86f805c3");
        contract.ProductionSourceTree.Should().Be(
            "367529280368366f8216471daff0c2d9e7984ccd");
        contract.ProfileInvariantCount.Should().Be(179);
        contract.ProfileSha256.Should().Be(
            "7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946");
        contract.InvariantManifestSha256.Should().Be(
            "bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7");
        contract.InvariantResultSha256.Should().Be(
            "bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7");
        contract.QueryShapeManifestSha256.Should().Be(
            DiscoveryQueryShapeManifest.ExpectedSuccessorManifestSha256);
        contract.ResultOrderIdentitySha256.Should().Be(
            DiscoveryQueryShapeManifest.ExpectedSuccessorResultIdentitySha256);
        contract.PostgreSql16Destination.Should().Be(
            "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16");
        contract.PostgreSql184Destination.Should().Be(
            "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4");
        contract.RequiredGates.Should().Equal(SuccessorPermanentExportManifest.Gates);

        Action act = () => SuccessorPermanentExportManifest.ValidateContract(contract);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AcceptedContract_MatchesRepositoryDispositionSourceAndMigrationInventory()
    {
        SuccessorPermanentExportContract contract = SuccessorPermanentExportManifest.Accepted;
        string repositoryRoot = QueryReviewGenerations.GetRepositoryRoot();
        string dispositionPath = Path.Combine(
            repositoryRoot,
            "docs/benchmarks/four-root-discovery-v1/experiments/index-candidates/disposition.md");
        await using FileStream stream = File.OpenRead(dispositionPath);
        string dispositionSha = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
        string sourceTree = (await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["rev-parse", "HEAD:src"],
            CancellationToken.None)).Trim();
        await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["merge-base", "--is-ancestor", contract.SourceLineageCommit, "HEAD"],
            CancellationToken.None);
        int migrationCount = typeof(RealEstateDbContext).Assembly.GetTypes().Count(type =>
            type.GetCustomAttribute<MigrationAttribute>() is not null);

        dispositionSha.Should().Be(contract.DispositionSha256);
        sourceTree.Should().Be(contract.ProductionSourceTree);
        migrationCount.Should().Be(contract.MigrationCount);
        (await File.ReadAllTextAsync(dispositionPath)).Should()
            .Contain("Commercial: `NO_INDEX`")
            .And.Contain("Land: `NO_INDEX`")
            .And.Contain("Production migration inventory: 21");
    }

    [Fact]
    public async Task AcceptedUnindexedCatalog_PassesAndAddedSubtypeIndexFails()
    {
        string repositoryRoot = QueryReviewGenerations.GetRepositoryRoot();
        string environmentPath = Path.Combine(
            repositoryRoot,
            "docs/benchmarks/four-root-discovery-v1/experiments/unindexed/" +
            "cfdde3290bcbde6edf1a95fdcc49f494ff9e84118239dd2403daaafc13a635d4/" +
            "environment.json");
        BaselineEnvironmentSnapshot environment = JsonSerializer.Deserialize<BaselineEnvironmentSnapshot>(
            await File.ReadAllTextAsync(environmentPath),
            JsonArtifactOutput.SerializerOptions)!;

        Action accepted = () =>
            SuccessorPermanentExportManifest.ValidateNoIndexCatalog(environment);
        accepted.Should().NotThrow();

        PostgreSqlIndexSnapshot candidate = environment.PostgreSql.Indexes.First(index =>
            index.Table == "ListingCommercialDetails") with
        {
            Name = "IX_ListingCommercialDetails_CommercialType"
        };
        BaselineEnvironmentSnapshot indexed = environment with
        {
            PostgreSql = environment.PostgreSql with
            {
                Indexes = [.. environment.PostgreSql.Indexes, candidate]
            }
        };
        Action rejected = () =>
            SuccessorPermanentExportManifest.ValidateNoIndexCatalog(indexed);
        rejected.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*NO_INDEX disposition*");
    }

    [Theory]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData(QueryReviewGenerations.PostgreSql184LaneId)]
    public void AcceptedSnapshot_IsAuthorizedOnlyForFixedLane(string laneId)
    {
        SuccessorPermanentExportSnapshot snapshot = CreateAcceptedSnapshot(laneId);

        Action act = () => SuccessorPermanentExportManifest.ValidateSnapshot(
            SuccessorPermanentExportManifest.Accepted,
            snapshot);

        act.Should().NotThrow();
    }

    [Fact]
    public void WrongDisposition_IsRejected()
    {
        SuccessorPermanentExportContract contract =
            SuccessorPermanentExportManifest.Accepted with
            {
                CommercialDisposition = "INDEX"
            };

        Action act = () => SuccessorPermanentExportManifest.ValidateContract(contract);
        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*malformed*accepted final disposition*");
    }

    [Fact]
    public void WrongMigrationCount_IsRejected()
    {
        SuccessorPermanentExportSnapshot snapshot =
            CreateAcceptedSnapshot(QueryReviewGenerations.PostgreSql16LaneId) with
            {
                MigrationCount = 22
            };

        Action act = () => SuccessorPermanentExportManifest.ValidateSnapshot(
            SuccessorPermanentExportManifest.Accepted,
            snapshot);
        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*does not match*schema*");
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("profile")]
    [InlineData("source")]
    [InlineData("shape")]
    [InlineData("result")]
    public void WrongGenerationProfileSourceShapeOrResultIdentity_IsRejected(string field)
    {
        SuccessorPermanentExportSnapshot snapshot =
            CreateAcceptedSnapshot(QueryReviewGenerations.PostgreSql16LaneId);
        snapshot = field switch
        {
            "generation" => snapshot with { GenerationId = "other-generation" },
            "profile" => snapshot with { ProfileIdentity = "other-profile" },
            "source" => snapshot with { ProductionSourceTree = new string('0', 40) },
            "shape" => snapshot with { QueryShapeManifestSha256 = new string('0', 64) },
            "result" => snapshot with { ResultOrderIdentitySha256 = new string('0', 64) },
            _ => throw new InvalidOperationException()
        };

        Action act = () => SuccessorPermanentExportManifest.ValidateSnapshot(
            SuccessorPermanentExportManifest.Accepted,
            snapshot);
        act.Should().Throw<BaselinePlanValidationException>();
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("invariant-manifest")]
    [InlineData("invariant-result")]
    public void WrongMeasuredProfileHash_IsRejected(string field)
    {
        SuccessorPermanentExportSnapshot snapshot =
            CreateAcceptedSnapshot(QueryReviewGenerations.PostgreSql16LaneId);
        snapshot = field switch
        {
            "profile" => snapshot with { ProfileSha256 = new string('0', 64) },
            "invariant-manifest" => snapshot with { InvariantManifestSha256 = new string('0', 64) },
            "invariant-result" => snapshot with { InvariantResultSha256 = new string('0', 64) },
            _ => throw new InvalidOperationException()
        };

        Action act = () => SuccessorPermanentExportManifest.ValidateSnapshot(
            SuccessorPermanentExportManifest.Accepted,
            snapshot);

        act.Should().Throw<BaselinePlanValidationException>();
    }

    [Fact]
    public void MissingOrForgedSchema2MeasuredProfileHashes_AreRejected()
    {
        ProfileVerificationEvidence accepted = CreateAcceptedProfileEvidence();
        ProfileVerificationEvidence missing = accepted with { ProfileSha256 = null };
        ProfileVerificationEvidence forged = accepted with
        {
            InvariantResultSha256 = new string('f', 64)
        };

        Action acceptedAct = () =>
            BaselineEvidenceWriter.ValidateSuccessorProfileEvidence(accepted);
        Action missingAct = () =>
            BaselineEvidenceWriter.ValidateSuccessorProfileEvidence(missing);
        Action forgedAct = () =>
            BaselineEvidenceWriter.ValidateSuccessorProfileEvidence(forged);

        acceptedAct.Should().NotThrow();
        missingAct.Should().Throw<BaselinePlanValidationException>();
        forgedAct.Should().Throw<BaselinePlanValidationException>();
    }

    [Fact]
    public void CurrentPublicationSource_AcceptsOnlyCleanExactCapturedHead()
    {
        SuccessorPermanentExportContract contract = SuccessorPermanentExportManifest.Accepted;
        const string commit = "94490b001946f3ffe68e03a929e62e2c6d281993";
        var accepted = new CurrentRepositoryPublicationState(
            commit,
            [],
            contract.ProductionSourceTree,
            AcceptedLineage: true);

        Action act = () => SuccessorPermanentExportManifest.ValidateCurrentPublicationSource(
            contract,
            commit,
            accepted);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("dirty-tool")]
    [InlineData("dirty-test")]
    [InlineData("head-advanced")]
    [InlineData("raw-forged")]
    [InlineData("lineage")]
    public void CurrentPublicationSource_RejectsMutableOrMismatchedRepositoryState(string drift)
    {
        SuccessorPermanentExportContract contract = SuccessorPermanentExportManifest.Accepted;
        const string captured = "94490b001946f3ffe68e03a929e62e2c6d281993";
        var current = new CurrentRepositoryPublicationState(
            captured,
            [],
            contract.ProductionSourceTree,
            AcceptedLineage: true);
        string sealedRunCommit = captured;

        switch (drift)
        {
            case "dirty-tool":
                current = current with { Status = [" M tools/RealEstate.QueryReview/Program.cs"] };
                break;
            case "dirty-test":
                current = current with { Status = [" M tests/RealEstate.Tests/Unit/QueryReview/Test.cs"] };
                break;
            case "head-advanced":
                current = current with { HeadCommit = new string('a', 40) };
                break;
            case "raw-forged":
                sealedRunCommit = new string('b', 40);
                break;
            case "lineage":
                current = current with { AcceptedLineage = false };
                break;
        }

        Action act = () => SuccessorPermanentExportManifest.ValidateCurrentPublicationSource(
            contract,
            sealedRunCommit,
            current);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*current repository*clean*exact sealed-run source commit*");
    }

    [Theory]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId,
        "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4")]
    [InlineData(QueryReviewGenerations.PostgreSql184LaneId,
        "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16")]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId,
        "docs/benchmarks/chapter-10f/evidence")]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId, "C:/arbitrary/evidence")]
    public void WrongOrNonfixedLaneDestination_IsRejected(
        string laneId,
        string destination)
    {
        SuccessorPermanentExportSnapshot snapshot = CreateAcceptedSnapshot(laneId) with
        {
            LaneDestination = destination
        };

        Action act = () => SuccessorPermanentExportManifest.ValidateSnapshot(
            SuccessorPermanentExportManifest.Accepted,
            snapshot);
        act.Should().Throw<BaselinePlanValidationException>();
    }

    [Fact]
    public void MalformedManifest_FailsClosed()
    {
        SuccessorPermanentExportContract malformed =
            SuccessorPermanentExportManifest.Accepted with
            {
                DispositionSha256 = "not-a-sha",
                RequiredGates = SuccessorPermanentExportManifest.Gates.Skip(1).ToArray()
            };

        Action act = () => SuccessorPermanentExportManifest.ValidateContract(malformed);
        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*malformed*");
    }

    [Fact]
    public void RecordedManifest_RoundTripsAndStillValidatesExactly()
    {
        string json = JsonSerializer.Serialize(
            SuccessorPermanentExportManifest.Accepted,
            JsonArtifactOutput.SerializerOptions);
        SuccessorPermanentExportContract recorded =
            JsonSerializer.Deserialize<SuccessorPermanentExportContract>(
                json,
                JsonArtifactOutput.SerializerOptions)!;

        Action act = () => SuccessorPermanentExportManifest.ValidateContract(recorded);
        act.Should().NotThrow();
    }

    private static SuccessorPermanentExportSnapshot CreateAcceptedSnapshot(string laneId)
    {
        SuccessorPermanentExportContract contract = SuccessorPermanentExportManifest.Accepted;
        string destination = laneId == QueryReviewGenerations.PostgreSql16LaneId
            ? contract.PostgreSql16Destination
            : contract.PostgreSql184Destination;
        return new SuccessorPermanentExportSnapshot(
            contract.GenerationId,
            contract.ProfileIdentity,
            contract.DispositionSha256,
            contract.MigrationCount,
            contract.SourceLineageCommit,
            contract.ProductionSourceTree,
            contract.ProfileInvariantCount,
            contract.ProfileSha256,
            contract.InvariantManifestSha256,
            contract.InvariantResultSha256,
            contract.QueryShapeManifestSha256,
            contract.ResultOrderIdentitySha256,
            laneId,
            destination);
    }

    private static ProfileVerificationEvidence CreateAcceptedProfileEvidence() => new(
        QueryReviewGenerations.FourRootDiscoveryId,
        100_000,
        200_000,
        179,
        179,
        0,
        Passed: true,
        SuccessorPermanentExportManifest.AcceptedProfileSha256,
        SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256,
        SuccessorPermanentExportManifest.AcceptedInvariantResultSha256);
}
