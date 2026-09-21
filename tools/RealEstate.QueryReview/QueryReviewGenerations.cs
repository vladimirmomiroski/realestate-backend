namespace RealEstate.QueryReview;

internal enum QueryReviewArtifactKind
{
    RawRun,
    ExperimentalBundle,
    PermanentEvidence
}

internal sealed record QueryReviewLaneDefinition(
    string Id,
    int PostgreSqlMajorVersion,
    string RequiredContainerImage,
    string PostgreSqlDataPath,
    bool RequireImageDeclaredAnonymousVolume,
    string? PermanentEvidenceRelativePath);

internal sealed record QueryReviewGenerationDefinition(
    string Id,
    string ProfileIdentity,
    IReadOnlyList<string> RecordedProfileIdentities,
    bool IsFrozenHistorical,
    bool ProfileProvisioned,
    bool CaptureProvisioned,
    bool PermanentExportFinalized,
    string PermanentEvidenceRootRelativePath,
    IReadOnlyList<QueryReviewLaneDefinition> Lanes)
{
    public bool MatchesRecordedProfile(string profileIdentity) =>
        RecordedProfileIdentities.Contains(profileIdentity, StringComparer.Ordinal);

    public QueryReviewLaneDefinition RequireLane(string laneId)
    {
        return Lanes.SingleOrDefault(lane =>
                   string.Equals(lane.Id, laneId, StringComparison.Ordinal))
               ?? throw new BaselinePlanValidationException(
                   $"Generation '{Id}' does not define lane '{laneId}'.");
    }

    public QueryReviewLaneDefinition RequireLaneForPostgreSqlMajor(
        int majorVersion)
    {
        return Lanes.SingleOrDefault(lane =>
                   lane.PostgreSqlMajorVersion == majorVersion)
               ?? throw new BaselinePlanValidationException(
                   $"PostgreSQL major version {majorVersion} is not an accepted lane " +
                   $"for generation '{Id}'.");
    }

    public void EnsureOnlineCommandAvailable(QueryReviewCommand command)
    {
        if (command == QueryReviewCommand.Doctor && IsFrozenHistorical)
        {
            return;
        }

        if (IsFrozenHistorical)
        {
            throw new QueryReviewGenerationNotReadyException(
                $"Generation '{Id}' is frozen and verify-only; command " +
                $"'{QueryReviewOptions.FormatCommand(command)}' is disabled.");
        }

        bool available = command switch
        {
            QueryReviewCommand.ProfileCreate or QueryReviewCommand.ProfileVerify =>
                ProfileProvisioned,
            QueryReviewCommand.CaptureSql or QueryReviewCommand.BaselineRun =>
                CaptureProvisioned,
            _ => false
        };

        if (!available)
        {
            throw new QueryReviewGenerationNotReadyException(
                $"Generation '{Id}' is recognized, but command " +
                $"'{QueryReviewOptions.FormatCommand(command)}' is not provisioned yet.");
        }
    }

    public void EnsureOfflineCommandAvailable(
        QueryReviewCommand command,
        QueryReviewArtifactKind artifactKind)
    {
        if (command == QueryReviewCommand.BaselineExport)
        {
            if (IsFrozenHistorical)
            {
                throw new QueryReviewGenerationNotReadyException(
                    $"Generation '{Id}' is frozen and verify-only; permanent export " +
                    "cannot replace its accepted evidence.");
            }

            if (!PermanentExportFinalized)
            {
                throw new QueryReviewGenerationNotReadyException(
                    $"Generation '{Id}' permanent export is not finalized or provisioned yet.");
            }

            if (artifactKind != QueryReviewArtifactKind.RawRun)
            {
                throw new BaselinePlanValidationException(
                    "Permanent export accepts only a sealed raw run; experimental and " +
                    "permanent artifacts are verify-only inputs.");
            }
        }

        if (command == QueryReviewCommand.BaselineVerify && !IsFrozenHistorical)
        {
            throw new QueryReviewGenerationNotReadyException(
                $"Generation '{Id}' offline verification is not provisioned yet.");
        }

        if (command == QueryReviewCommand.BaselineVerify &&
            artifactKind is not (QueryReviewArtifactKind.RawRun or
                QueryReviewArtifactKind.ExperimentalBundle or
                QueryReviewArtifactKind.PermanentEvidence))
        {
            throw new QueryReviewGenerationNotReadyException(
                $"Artifact kind '{artifactKind}' cannot be verified.");
        }
    }
}

internal static class QueryReviewGenerations
{
    public const string FrozenHistoricalId = "frozen-historical";
    public const string FourRootDiscoveryId = "four-root-discovery-v1";
    public const string PostgreSql16LaneId = "postgresql-16";
    public const string PostgreSql184LaneId = "postgresql-18.4";

    public static readonly QueryReviewGenerationDefinition FrozenHistorical =
        new(
            FrozenHistoricalId,
            "chapter-10f-v1",
            ["chapter-10f-v1", "chapter-10f-v2"],
            IsFrozenHistorical: true,
            ProfileProvisioned: false,
            CaptureProvisioned: false,
            PermanentExportFinalized: false,
            "docs/benchmarks/chapter-10f/evidence",
            [
                new QueryReviewLaneDefinition(
                    PostgreSql16LaneId,
                    16,
                    "postgres:16-alpine",
                    "/var/lib/postgresql/data",
                    true,
                    "docs/benchmarks/chapter-10f/evidence")
            ]);

    public static readonly QueryReviewGenerationDefinition FourRootDiscovery =
        new(
            FourRootDiscoveryId,
            FourRootDiscoveryId,
            [FourRootDiscoveryId],
            IsFrozenHistorical: false,
            ProfileProvisioned: true,
            CaptureProvisioned: false,
            PermanentExportFinalized: false,
            "docs/benchmarks/four-root-discovery-v1/evidence",
            [
                new QueryReviewLaneDefinition(
                    PostgreSql16LaneId,
                    16,
                    "postgres:16-alpine",
                    "/var/lib/postgresql/data",
                    true,
                    "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16"),
                new QueryReviewLaneDefinition(
                    PostgreSql184LaneId,
                    18,
                    "postgres:18.4",
                    "/var/lib/postgresql",
                    true,
                    "docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4")
            ]);

    public static IReadOnlyList<QueryReviewGenerationDefinition> All { get; } =
        [FrozenHistorical, FourRootDiscovery];

    public static QueryReviewGenerationDefinition ResolveOrThrow(string? profile)
    {
        string requested = string.IsNullOrWhiteSpace(profile)
            ? FrozenHistoricalId
            : profile.Trim();

        return All.SingleOrDefault(definition =>
                   string.Equals(definition.Id, requested, StringComparison.Ordinal))
               ?? throw new QueryReviewGenerationNotReadyException(
                   $"Unknown QueryReview profile/generation '{requested}'. Expected exactly " +
                   $"'{FrozenHistoricalId}' or '{FourRootDiscoveryId}'.");
    }

    public static QueryReviewGenerationDefinition ResolveRecordedProfileOrThrow(
        string profileIdentity)
    {
        return All.SingleOrDefault(definition =>
                   definition.MatchesRecordedProfile(profileIdentity))
               ?? throw new BaselinePlanValidationException(
                   $"Recorded profile '{profileIdentity}' does not identify a supported generation.");
    }

    public static string GetRepositoryRoot()
    {
        foreach (string start in new[]
                 {
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));

            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new BaselinePlanValidationException(
            "Unable to locate the repository root for QueryReview path validation.");
    }

    public static string GetPermanentEvidencePath(
        QueryReviewGenerationDefinition definition,
        QueryReviewLaneDefinition lane)
    {
        QueryReviewGenerationDefinition? catalogDefinition = All.SingleOrDefault(
            candidate => ReferenceEquals(candidate, definition));

        if (catalogDefinition is null ||
            !catalogDefinition.Lanes.Any(candidate => ReferenceEquals(candidate, lane)) ||
            string.IsNullOrWhiteSpace(lane.PermanentEvidenceRelativePath))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence routing accepts only the exact catalog generation/lane " +
                "definition pair.");
        }

        string repositoryRoot = GetRepositoryRoot();
        string resolved = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            lane.PermanentEvidenceRelativePath));
        string expectedRoot = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            definition.PermanentEvidenceRootRelativePath));
        string relative = Path.GetRelativePath(expectedRoot, resolved);

        if (relative == ".." ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                $"Permanent destination for '{definition.Id}/{lane.Id}' escapes its " +
                "allowlisted generation root.");
        }

        return resolved;
    }
}

internal sealed class QueryReviewGenerationNotReadyException(string message)
    : Exception(message);
