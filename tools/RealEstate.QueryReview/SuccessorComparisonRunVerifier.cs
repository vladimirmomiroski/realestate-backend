using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RealEstate.QueryReview;

internal static class SuccessorComparisonRunVerifier
{
    internal static async Task<VerifiedSuccessorComparisonRun> VerifyAsync(
        QueryReviewArtifactDescriptor primaryDescriptor,
        BaselineVerificationResult primaryVerification,
        string comparisonRunDirectory,
        CancellationToken cancellationToken = default)
    {
        QueryReviewArtifactDescriptor comparisonDescriptor =
            await QueryReviewArtifactRouter.InspectAsync(
                comparisonRunDirectory,
                cancellationToken);

        ValidateComparisonDescriptor(comparisonDescriptor);

        BaselineVerificationResult comparisonVerification = await ExplainRunner.VerifyAsync(
            comparisonDescriptor.Generation,
            comparisonDescriptor.Lane,
            comparisonDescriptor.Directory,
            cancellationToken);
        QueryReviewArtifactDescriptor curatedDescriptor =
            await QueryReviewArtifactRouter.InspectAsync(
                comparisonVerification.CuratedDirectory,
                cancellationToken);
        await ExperimentalEvidenceBundle.VerifyAsync(curatedDescriptor, cancellationToken);

        SuccessorRunComparisonIdentity primary = await CreateIdentityAsync(
            primaryDescriptor,
            primaryVerification,
            cancellationToken);
        SuccessorRunComparisonIdentity comparison = await CreateIdentityAsync(
            comparisonDescriptor,
            comparisonVerification,
            cancellationToken);
        ValidateCompatible(primary, comparison);
        CrossMajorComparisonReport report = CrossMajorCompatibilityReportBuilder.Build(
            primaryVerification.Measurements,
            comparisonVerification.Measurements);
        CrossMajorCompatibilityReportBuilder.ValidateForPublication(report);

        return new VerifiedSuccessorComparisonRun(
            comparisonDescriptor,
            comparisonVerification,
            primary,
            comparison,
            report);
    }

    internal static void ValidateComparisonDescriptor(
        QueryReviewArtifactDescriptor comparisonDescriptor)
    {
        if (comparisonDescriptor.Kind != QueryReviewArtifactKind.RawRun ||
            comparisonDescriptor.Generation != QueryReviewGenerations.FourRootDiscovery ||
            comparisonDescriptor.Lane.Id != QueryReviewGenerations.PostgreSql16LaneId)
        {
            throw new BaselinePlanValidationException(
                "The PostgreSQL 18.4 comparison must be a sealed four-root-discovery-v1 " +
                "PostgreSQL 16 raw run.");
        }
    }

    internal static async Task<SuccessorRunComparisonIdentity> CreateIdentityAsync(
        QueryReviewArtifactDescriptor descriptor,
        BaselineVerificationResult verification,
        CancellationToken cancellationToken = default)
    {
        string runDirectory = descriptor.Directory;
        RawBaselineManifest manifest = await ReadRequiredJsonAsync<RawBaselineManifest>(
            Path.Combine(runDirectory, "manifest.json"),
            cancellationToken);
        SqlCaptureRun capture = await ReadRequiredJsonAsync<SqlCaptureRun>(
            Path.Combine(runDirectory, "captured-commands.json"),
            cancellationToken);
        BaselineEnvironmentSnapshot environment =
            await ReadRequiredJsonAsync<BaselineEnvironmentSnapshot>(
                Path.Combine(runDirectory, "environment-raw.json"),
                cancellationToken);
        DeterministicProfileVerificationSnapshot profile = manifest.ProfileVerification
            ?? throw new BaselinePlanValidationException(
                "The successor comparison run is missing measured profile identity metadata.");

        SuccessorPermanentExportContract contract = descriptor.Generation.PermanentExportContract
            ?? throw new BaselinePlanValidationException(
                "The successor comparison generation has no final export contract.");
        SuccessorPermanentExportSnapshot snapshot =
            await SuccessorPermanentExportManifest.CaptureSnapshotAsync(
                descriptor.Generation,
                descriptor.Lane,
                manifest,
                environment,
                capture,
                cancellationToken);
        SuccessorPermanentExportManifest.ValidateSnapshot(contract, snapshot);
        SuccessorPermanentExportManifest.ValidateNoIndexCatalog(environment);

        QueryShapeManifestSnapshot shape = capture.QueryShapeManifest
            ?? throw new BaselinePlanValidationException(
                "The successor comparison capture is missing its query-shape manifest.");

        return new SuccessorRunComparisonIdentity(
            descriptor.Generation.Id,
            manifest.ProfileVersion,
            descriptor.Lane.Id,
            manifest.GitCommit,
            profile.ProfileSha256 ?? string.Empty,
            profile.InvariantManifestSha256 ?? string.Empty,
            profile.InvariantResultSha256 ?? string.Empty,
            manifest.QueryShapeManifestSha256 ?? string.Empty,
            Hash(shape.Commands.Select(command => new
            {
                command.CommandKey,
                command.NormalizedSqlSha256
            })),
            Hash(shape.Commands.Select(command => new
            {
                command.CommandKey,
                command.TypedParametersSha256,
                command.TypedParameterCount
            })),
            Hash(shape.Results.Select(result => new
            {
                result.ShapeId,
                result.TotalCount,
                result.ItemCount
            })),
            Hash(shape.Results.Select(result => new
            {
                result.ShapeId,
                result.OrderedIds,
                result.OrderedIdsSha256
            })),
            Hash(environment.PostgreSql.Settings
                .OrderBy(setting => setting.Name, StringComparer.Ordinal)
                .Select(setting => new
                {
                    setting.Name,
                    setting.Setting,
                    setting.Unit
                })),
            manifest.CommandCount,
            manifest.ParameterCount,
            manifest.WarmUpRunsPerCommand,
            manifest.MeasuredRunsPerCommand,
            manifest.PlanCount);
    }

    internal static void ValidateCompatible(
        SuccessorRunComparisonIdentity primary,
        SuccessorRunComparisonIdentity comparison)
    {
        if (!string.Equals(primary.GenerationId, QueryReviewGenerations.FourRootDiscoveryId, StringComparison.Ordinal) ||
            !string.Equals(comparison.GenerationId, QueryReviewGenerations.FourRootDiscoveryId, StringComparison.Ordinal) ||
            !string.Equals(primary.ProfileIdentity, comparison.ProfileIdentity, StringComparison.Ordinal) ||
            !string.Equals(primary.LaneId, QueryReviewGenerations.PostgreSql184LaneId, StringComparison.Ordinal) ||
            !string.Equals(comparison.LaneId, QueryReviewGenerations.PostgreSql16LaneId, StringComparison.Ordinal) ||
            !string.Equals(primary.GitCommit, comparison.GitCommit, StringComparison.Ordinal) ||
            !string.Equals(primary.ProfileSha256, SuccessorPermanentExportManifest.AcceptedProfileSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.InvariantManifestSha256, SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.InvariantResultSha256, SuccessorPermanentExportManifest.AcceptedInvariantResultSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.QueryShapeManifestSha256, DiscoveryQueryShapeManifest.ExpectedSuccessorManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.ProfileSha256, comparison.ProfileSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.InvariantManifestSha256, comparison.InvariantManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.InvariantResultSha256, comparison.InvariantResultSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.QueryShapeManifestSha256, comparison.QueryShapeManifestSha256, StringComparison.Ordinal) ||
            !string.Equals(primary.SqlIdentitySha256, comparison.SqlIdentitySha256, StringComparison.Ordinal) ||
            !string.Equals(primary.ParameterIdentitySha256, comparison.ParameterIdentitySha256, StringComparison.Ordinal) ||
            !string.Equals(primary.ResultIdentitySha256, comparison.ResultIdentitySha256, StringComparison.Ordinal) ||
            !string.Equals(primary.OrderIdentitySha256, comparison.OrderIdentitySha256, StringComparison.Ordinal) ||
            !string.Equals(primary.SettingsIdentitySha256, comparison.SettingsIdentitySha256, StringComparison.Ordinal) ||
            primary.CommandCount != comparison.CommandCount ||
            primary.ParameterCount != comparison.ParameterCount ||
            primary.WarmUpRunsPerCommand != comparison.WarmUpRunsPerCommand ||
            primary.MeasuredRunsPerCommand != comparison.MeasuredRunsPerCommand ||
            primary.PlanCount != comparison.PlanCount)
        {
            throw new BaselinePlanValidationException(
                "The PostgreSQL 16 comparison run does not exactly match the PostgreSQL 18.4 " +
                "source/profile/query/SQL/parameter/result/order/settings/sample contract.");
        }
    }

    private static string Hash<T>(IEnumerable<T> values)
    {
        string json = JsonSerializer.Serialize(values, JsonArtifactOutput.SerializerOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new BaselinePlanValidationException(
                $"Required successor comparison artifact '{path}' is missing.");
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(
                       stream,
                       JsonArtifactOutput.SerializerOptions,
                       cancellationToken)
                   ?? throw new BaselinePlanValidationException(
                       $"Successor comparison artifact '{path}' deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new BaselinePlanValidationException(
                $"Successor comparison artifact '{path}' is malformed: {exception.Message}");
        }
    }
}
