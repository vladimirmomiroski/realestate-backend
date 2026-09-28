using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Migrations;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.QueryReview;

internal sealed record SuccessorPermanentExportContract(
    int SchemaVersion,
    string GenerationId,
    string ProfileIdentity,
    string DispositionSha256,
    string CommercialDisposition,
    string LandDisposition,
    int MigrationCount,
    string SourceLineageCommit,
    string ProductionSourceTree,
    int ProfileInvariantCount,
    string ProfileSha256,
    string InvariantManifestSha256,
    string InvariantResultSha256,
    string QueryShapeManifestSha256,
    string ResultOrderIdentitySha256,
    string PostgreSql16Destination,
    string PostgreSql184Destination,
    IReadOnlyList<string> RequiredGates);

internal sealed record SuccessorPermanentExportSnapshot(
    string GenerationId,
    string ProfileIdentity,
    string DispositionSha256,
    int MigrationCount,
    string SourceLineageCommit,
    string ProductionSourceTree,
    int ProfileInvariantCount,
    string ProfileSha256,
    string InvariantManifestSha256,
    string InvariantResultSha256,
    string QueryShapeManifestSha256,
    string ResultOrderIdentitySha256,
    string LaneId,
    string LaneDestination);

internal sealed record CurrentRepositoryPublicationState(
    string HeadCommit,
    IReadOnlyList<string> Status,
    string ProductionSourceTree,
    bool AcceptedLineage);

internal static class SuccessorPermanentExportManifest
{
    internal const string NoIndexDisposition = "NO_INDEX";
    internal const int AcceptedMigrationCount = 21;
    internal const string AcceptedDispositionSha256 =
        "dda685aa1e6053424728cf70dfe8a1a9fc2b9956e8de5c7bd24370cb00c01800";
    internal const string AcceptedProductionSourceTree =
        "367529280368366f8216471daff0c2d9e7984ccd";
    internal const string AcceptedSourceLineageCommit =
        "ae89a343a3c41a9c1e7f6f5c4f00fe4d86f805c3";
    internal const string AcceptedProfileSha256 =
        "7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946";
    internal const string AcceptedInvariantManifestSha256 =
        "bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7";
    internal const string AcceptedInvariantResultSha256 =
        "bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7";
    internal const string PostgreSql16Destination =
        "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16";
    internal const string PostgreSql184Destination =
        "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4";

    private const string DispositionRelativePath =
        "docs/benchmarks/four-root-discovery-v1/experiments/index-candidates/disposition.md";

    internal static readonly IReadOnlyList<string> Gates =
    [
        "exact-sql-parameters-results-order",
        "accepted-no-index-disposition",
        "final-migration-inventory",
        "complete-warmup-and-five-sample-plans",
        "no-spill-temp-or-capture-anomaly",
        "credential-clean",
        "fixed-lane-routing",
        "historical-verify-only",
        "postgresql-18.4-comparison-run-required"
    ];

    internal static readonly SuccessorPermanentExportContract Accepted = new(
        SchemaVersion: 1,
        QueryReviewGenerations.FourRootDiscoveryId,
        QueryReviewGenerations.FourRootDiscoveryId,
        AcceptedDispositionSha256,
        NoIndexDisposition,
        NoIndexDisposition,
        AcceptedMigrationCount,
        AcceptedSourceLineageCommit,
        AcceptedProductionSourceTree,
        ProfileInvariantCount: 179,
        AcceptedProfileSha256,
        AcceptedInvariantManifestSha256,
        AcceptedInvariantResultSha256,
        DiscoveryQueryShapeManifest.ExpectedSuccessorManifestSha256,
        DiscoveryQueryShapeManifest.ExpectedSuccessorResultIdentitySha256,
        PostgreSql16Destination,
        PostgreSql184Destination,
        Gates);

    internal static void ValidateContract(SuccessorPermanentExportContract contract)
    {
        if (contract.SchemaVersion != Accepted.SchemaVersion ||
            !string.Equals(contract.GenerationId, Accepted.GenerationId, StringComparison.Ordinal) ||
            !string.Equals(contract.ProfileIdentity, Accepted.ProfileIdentity, StringComparison.Ordinal) ||
            !string.Equals(contract.DispositionSha256, Accepted.DispositionSha256, StringComparison.Ordinal) ||
            !string.Equals(contract.CommercialDisposition, Accepted.CommercialDisposition, StringComparison.Ordinal) ||
            !string.Equals(contract.LandDisposition, Accepted.LandDisposition, StringComparison.Ordinal) ||
            contract.MigrationCount != Accepted.MigrationCount ||
            !string.Equals(contract.SourceLineageCommit, Accepted.SourceLineageCommit, StringComparison.Ordinal) ||
            !string.Equals(contract.ProductionSourceTree, Accepted.ProductionSourceTree, StringComparison.Ordinal) ||
            contract.ProfileInvariantCount != Accepted.ProfileInvariantCount ||
            !string.Equals(contract.ProfileSha256, Accepted.ProfileSha256, StringComparison.Ordinal) ||
            !string.Equals(contract.InvariantManifestSha256, Accepted.InvariantManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(contract.InvariantResultSha256, Accepted.InvariantResultSha256, StringComparison.Ordinal) ||
            !string.Equals(contract.QueryShapeManifestSha256, Accepted.QueryShapeManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(contract.ResultOrderIdentitySha256, Accepted.ResultOrderIdentitySha256, StringComparison.Ordinal) ||
            !string.Equals(contract.PostgreSql16Destination, Accepted.PostgreSql16Destination, StringComparison.Ordinal) ||
            !string.Equals(contract.PostgreSql184Destination, Accepted.PostgreSql184Destination, StringComparison.Ordinal) ||
            contract.RequiredGates.Count != Gates.Count ||
            !contract.RequiredGates.SequenceEqual(Gates, StringComparer.Ordinal) ||
            !IsLowerHexSha256(contract.DispositionSha256) ||
            !IsLowerHexSha256(contract.ProfileSha256) ||
            !IsLowerHexSha256(contract.InvariantManifestSha256) ||
            !IsLowerHexSha256(contract.InvariantResultSha256) ||
            !IsLowerHexSha256(contract.QueryShapeManifestSha256) ||
            !IsLowerHexSha256(contract.ResultOrderIdentitySha256))
        {
            throw new BaselinePlanValidationException(
                "The four-root successor permanent-export manifest is malformed or does not " +
                "match the accepted final disposition/schema/source/profile/query contract.");
        }
    }

    internal static void ValidateSnapshot(
        SuccessorPermanentExportContract contract,
        SuccessorPermanentExportSnapshot snapshot)
    {
        ValidateContract(contract);
        string expectedDestination = snapshot.LaneId switch
        {
            QueryReviewGenerations.PostgreSql16LaneId => contract.PostgreSql16Destination,
            QueryReviewGenerations.PostgreSql184LaneId => contract.PostgreSql184Destination,
            _ => string.Empty
        };

        if (!string.Equals(snapshot.GenerationId, contract.GenerationId, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ProfileIdentity, contract.ProfileIdentity, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DispositionSha256, contract.DispositionSha256, StringComparison.Ordinal) ||
            snapshot.MigrationCount != contract.MigrationCount ||
            !string.Equals(snapshot.SourceLineageCommit, contract.SourceLineageCommit, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ProductionSourceTree, contract.ProductionSourceTree, StringComparison.Ordinal) ||
            snapshot.ProfileInvariantCount != contract.ProfileInvariantCount ||
            !string.Equals(snapshot.ProfileSha256, contract.ProfileSha256, StringComparison.Ordinal) ||
            !string.Equals(snapshot.InvariantManifestSha256, contract.InvariantManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(snapshot.InvariantResultSha256, contract.InvariantResultSha256, StringComparison.Ordinal) ||
            !string.Equals(snapshot.QueryShapeManifestSha256, contract.QueryShapeManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ResultOrderIdentitySha256, contract.ResultOrderIdentitySha256, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(expectedDestination) ||
            !string.Equals(snapshot.LaneDestination, expectedDestination, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "The verified raw run does not match the finalized four-root permanent-export " +
                "generation/profile/disposition/schema/source/query/lane contract.");
        }
    }

    internal static async Task<SuccessorPermanentExportSnapshot> CaptureSnapshotAsync(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        RawBaselineManifest manifest,
        BaselineEnvironmentSnapshot environment,
        SqlCaptureRun captureRun,
        CancellationToken cancellationToken)
    {
        SuccessorPermanentExportContract contract = generation.PermanentExportContract
            ?? throw new BaselinePlanValidationException(
                "The successor generation is missing its finalized permanent-export manifest.");
        ValidateContract(contract);

        if (environment.Git.Status.Count != 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent successor evidence requires a clean committed capture source tree.");
        }

        string repositoryRoot = QueryReviewGenerations.GetRepositoryRoot();
        string dispositionHash = await ComputeRawSha256Async(
            Path.Combine(repositoryRoot, DispositionRelativePath),
            cancellationToken);
        string sourceTree = (await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["rev-parse", $"{environment.Git.Commit}:src"],
            cancellationToken)).Trim();
        await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["merge-base", "--is-ancestor", contract.SourceLineageCommit, environment.Git.Commit],
            cancellationToken);
        int migrationCount = typeof(RealEstateDbContext).Assembly.GetTypes().Count(type =>
            type.GetCustomAttribute<MigrationAttribute>() is not null);
        string shapeIdentity = manifest.QueryShapeManifestSha256 ?? string.Empty;
        string resultIdentity = DiscoveryQueryShapeManifest.ComputeResultIdentitySha256(
            captureRun.ShapeResults);

        return new SuccessorPermanentExportSnapshot(
            generation.Id,
            manifest.ProfileVersion,
            dispositionHash,
            migrationCount,
            contract.SourceLineageCommit,
            sourceTree,
            manifest.ProfileVerification?.InvariantTotal ?? -1,
            manifest.ProfileVerification?.ProfileSha256 ?? string.Empty,
            manifest.ProfileVerification?.InvariantManifestSha256 ?? string.Empty,
            manifest.ProfileVerification?.InvariantResultSha256 ?? string.Empty,
            shapeIdentity,
            resultIdentity,
            lane.Id,
            lane.PermanentEvidenceRelativePath ?? string.Empty);
    }

    internal static async Task ValidateCurrentPublicationSourceAsync(
        SuccessorPermanentExportContract contract,
        string sealedRunSourceCommit,
        CancellationToken cancellationToken)
    {
        string head = (await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["rev-parse", "HEAD"],
            cancellationToken)).Trim();
        string statusOutput = await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["status", "--short", "--untracked-files=all"],
            cancellationToken);
        string[] status = statusOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string sourceTree = (await EnvironmentSnapshotCollector.RunProcessAsync(
            "git",
            ["rev-parse", "HEAD:src"],
            cancellationToken)).Trim();
        bool acceptedLineage;

        try
        {
            await EnvironmentSnapshotCollector.RunProcessAsync(
                "git",
                ["merge-base", "--is-ancestor", contract.SourceLineageCommit, "HEAD"],
                cancellationToken);
            acceptedLineage = true;
        }
        catch
        {
            acceptedLineage = false;
        }

        ValidateCurrentPublicationSource(
            contract,
            sealedRunSourceCommit,
            new CurrentRepositoryPublicationState(
                head,
                status,
                sourceTree,
                acceptedLineage));
    }

    internal static void ValidateCurrentPublicationSource(
        SuccessorPermanentExportContract contract,
        string sealedRunSourceCommit,
        CurrentRepositoryPublicationState current)
    {
        ValidateContract(contract);

        if (!IsLowerHexCommit(current.HeadCommit) ||
            !IsLowerHexCommit(sealedRunSourceCommit) ||
            current.Status.Count != 0 ||
            !string.Equals(current.HeadCommit, sealedRunSourceCommit, StringComparison.Ordinal) ||
            !string.Equals(current.ProductionSourceTree, contract.ProductionSourceTree, StringComparison.Ordinal) ||
            !current.AcceptedLineage)
        {
            throw new BaselinePlanValidationException(
                "Permanent successor publication requires the current repository to be clean, " +
                "at the exact sealed-run source commit, and on the accepted production-source lineage.");
        }
    }

    internal static void ValidateNoIndexCatalog(BaselineEnvironmentSnapshot environment)
    {
        string[] tables = ["ListingCommercialDetails", "ListingLandDetails"];
        bool exactSharedPrimaryKeys = tables.All(table =>
        {
            PostgreSqlIndexSnapshot[] indexes = environment.PostgreSql.Indexes
                .Where(index => string.Equals(index.Table, table, StringComparison.Ordinal))
                .ToArray();
            return indexes.Length == 1 &&
                   string.Equals(indexes[0].Schema, "public", StringComparison.Ordinal) &&
                   string.Equals(indexes[0].Name, $"PK_{table}", StringComparison.Ordinal) &&
                   string.Equals(indexes[0].AccessMethod, "btree", StringComparison.Ordinal) &&
                   indexes[0].Columns?.SequenceEqual(["ListingId"], StringComparer.Ordinal) == true &&
                   indexes[0].IsValid is true &&
                   indexes[0].IsReady is true &&
                   indexes[0].IsLive is true;
        });

        if (!exactSharedPrimaryKeys)
        {
            throw new BaselinePlanValidationException(
                "The accepted NO_INDEX disposition requires both subtype detail tables to " +
                "contain exactly their valid, ready, live shared primary-key indexes.");
        }
    }

    private static bool IsLowerHexSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsLowerHexCommit(string value) =>
        value.Length == 40 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async Task<string> ComputeRawSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}
