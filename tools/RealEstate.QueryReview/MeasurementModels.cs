using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace RealEstate.QueryReview;

internal sealed record CapturedParameter(
    string Name,
    string ClrType,
    string DbType,
    string? NpgsqlDbType,
    string? DataTypeName,
    bool IsNullable,
    bool IsNull,
    string Value);

internal sealed record CapturedCommand(
    string LogicalRunId,
    string ShapeId,
    int ShapeSequence,
    string CommandRole,
    string CommandType,
    string CommandText,
    IReadOnlyList<CapturedParameter> Parameters);

internal sealed record QueryShapeResult(
    string ShapeId,
    int? ExpectedTotalCount,
    int? ActualTotalCount,
    int ExpectedItemCount,
    int ActualItemCount,
    IReadOnlyList<Guid> ResultIds);

internal sealed record SqlCaptureRun(
    string LogicalRunId,
    string ProfileVersion,
    int CSharpSeed,
    double PostgreSqlSeed,
    string Database,
    string PostgreSqlVersion,
    IReadOnlyList<QueryShapeResult> ShapeResults,
    IReadOnlyList<CapturedCommand> Commands);

internal sealed record ReplayableParameter(
    string Name,
    int Ordinal,
    object? Value,
    DbType DbType,
    NpgsqlDbType? NpgsqlDbType,
    string? DataTypeName,
    ParameterDirection Direction,
    bool IsNullable,
    int Size,
    byte Precision,
    byte Scale);

internal sealed record ReplayableCommand(
    CapturedCommand CapturedCommand,
    IReadOnlyList<ReplayableParameter> Parameters);

internal sealed record ProductionCaptureSession(
    SqlCaptureRun CaptureRun,
    IReadOnlyList<ReplayableCommand> ReplayableCommands);

internal sealed record GitEnvironmentSnapshot(
    string Commit,
    string Branch,
    IReadOnlyList<string> Status);

internal sealed record RuntimeEnvironmentSnapshot(
    string OperatingSystem,
    string OsArchitecture,
    string ProcessArchitecture,
    string Framework,
    string RuntimeVersion,
    string DotNetSdkVersion,
    IReadOnlyList<string> DotNetRuntimes,
    string? ProcessorIdentifier,
    int LogicalProcessorCount,
    long AvailableMemoryBytes);

internal sealed record DockerEnvironmentSnapshot(
    string ContainerName,
    string ContainerId,
    string ContainerImageId,
    string ContainerImage,
    string ContainerCreated,
    string ContainerStatus,
    long MemoryLimitBytes,
    long NanoCpus,
    long CpuQuota,
    long CpuPeriod,
    string DockerServerVersion,
    string DockerOperatingSystem,
    string DockerOsType,
    string DockerArchitecture,
    int DockerCpuCount,
    long DockerMemoryBytes,
    IReadOnlyList<string> ImageRepoDigests);

internal sealed record PostgreSqlSettingSnapshot(
    string Name,
    string Setting,
    string? Unit,
    string Source);

internal sealed record PostgreSqlExtensionSnapshot(string Name, string Version);

internal sealed record PostgreSqlRelationSnapshot(
    string Schema,
    string Name,
    string Kind,
    long RelationSizeBytes,
    long TotalRelationSizeBytes);

internal sealed record PostgreSqlIndexSnapshot(
    string Schema,
    string Table,
    string Name,
    string Definition,
    long SizeBytes,
    string? AccessMethod = null,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<string>? OperatorClasses = null,
    bool? IsValid = null,
    bool? IsReady = null,
    bool? IsLive = null);

internal sealed record PostgreSqlTableStatisticsSnapshot(
    string Schema,
    string Table,
    long EstimatedLiveRows,
    long EstimatedDeadRows,
    DateTime? LastVacuumUtc,
    DateTime? LastAutoVacuumUtc,
    DateTime? LastAnalyzeUtc,
    DateTime? LastAutoAnalyzeUtc,
    long VacuumCount,
    long AutoVacuumCount,
    long AnalyzeCount,
    long AutoAnalyzeCount);

internal sealed record PostgreSqlEnvironmentSnapshot(
    string Version,
    string ServerVersion,
    string ServerVersionNumber,
    string Database,
    long DatabaseSizeBytes,
    int ActiveVacuumCount,
    IReadOnlyList<PostgreSqlSettingSnapshot> Settings,
    IReadOnlyList<PostgreSqlExtensionSnapshot> Extensions,
    IReadOnlyList<PostgreSqlRelationSnapshot> Relations,
    IReadOnlyList<PostgreSqlIndexSnapshot> Indexes,
    IReadOnlyList<PostgreSqlTableStatisticsSnapshot> TableStatistics);

internal sealed record BaselineEnvironmentSnapshot(
    DateTime CapturedAtUtc,
    GitEnvironmentSnapshot Git,
    RuntimeEnvironmentSnapshot Runtime,
    DockerEnvironmentSnapshot Docker,
    PostgreSqlEnvironmentSnapshot PostgreSql,
    string EfCoreVersion,
    string NpgsqlVersion,
    string ToolVersion,
    string ProfileVersion,
    int CSharpSeed,
    double PostgreSqlSeed,
    TimeSpan VacuumAnalyzeDuration);

internal sealed record RawPlanSample(
    string CommandKey,
    string ShapeId,
    int ShapeSequence,
    string CommandRole,
    string RunKind,
    int RunNumber,
    string RelativePlanPath,
    string SqlSha256,
    string ParameterSha256,
    string StructuralPlanSha256,
    long ActualRows,
    long ActualLoops);

internal sealed record DeterministicProfileVerificationSnapshot(
    string ProfileIdentity,
    long ListingCount,
    long TranslationCount,
    int InvariantTotal,
    int InvariantPassed,
    int InvariantFailed);

internal sealed record RawBaselineManifest(
    string BaselineRunId,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string ProfileVersion,
    int CSharpSeed,
    double PostgreSqlSeed,
    string GitCommit,
    string PostgreSqlVersion,
    string ResultSha256,
    int CommandCount,
    int ParameterCount,
    int WarmUpRunsPerCommand,
    int MeasuredRunsPerCommand,
    int PlanCount,
    string CapturedCommandsPath,
    string EnvironmentPath,
    bool CredentialScanPassed,
    IReadOnlyList<RawPlanSample> Samples,
    DeterministicProfileVerificationSnapshot? ProfileVerification = null);

internal sealed record PlanBufferMetrics(
    long SharedHit,
    long SharedRead,
    long SharedDirtied,
    long SharedWritten,
    long LocalHit,
    long LocalRead,
    long LocalDirtied,
    long LocalWritten,
    long TempRead,
    long TempWritten);

internal sealed record PlanNodeMeasurement(
    string Path,
    int Depth,
    string NodeType,
    string? ParentRelationship,
    string? Relation,
    string? Schema,
    string? Alias,
    string? ScanDirection,
    string? IndexName,
    string? JoinType,
    decimal? StartupCost,
    decimal? TotalCost,
    long? PlanRows,
    long? PlanWidth,
    decimal? ActualStartupTimeMilliseconds,
    decimal? ActualTotalTimeMilliseconds,
    long ActualRows,
    long ActualLoops,
    long RowsRemovedByFilter,
    long RowsRemovedByIndexRecheck,
    long RowsRemovedByJoinFilter,
    string? Filter,
    string? IndexCondition,
    string? RecheckCondition,
    string? JoinFilter,
    string? HashCondition,
    string? MergeCondition,
    IReadOnlyList<string> SortKeys,
    string? SortMethod,
    long? SortSpaceUsedKilobytes,
    string? SortSpaceType,
    long? HashBatches,
    long? PeakMemoryUsageKilobytes,
    PlanBufferMetrics Buffers);

internal sealed record PlanSampleMeasurement(
    string CommandKey,
    string ShapeId,
    int ShapeSequence,
    string CommandRole,
    string RunKind,
    int RunNumber,
    string RelativePlanPath,
    string RawPlanSha256,
    string SqlSha256,
    string ParameterSha256,
    string StructuralPlanSha256,
    decimal PlanningTimeMilliseconds,
    decimal ExecutionTimeMilliseconds,
    long ActualRows,
    long ActualLoops,
    PlanBufferMetrics TopLevelBuffers,
    IReadOnlyDictionary<string, string> Settings,
    bool Spilled,
    IReadOnlyList<string> SpillReasons,
    IReadOnlyList<string> ScanTypes,
    IReadOnlyList<string> JoinTypes,
    IReadOnlyList<string> SortMethods,
    IReadOnlyList<string> IndexNames,
    long TotalRowsRemoved,
    long MaximumPeakMemoryUsageKilobytes,
    IReadOnlyList<PlanNodeMeasurement> Nodes);

internal sealed record MedianSelection(
    decimal Value,
    int RunNumber);

internal sealed record CommandMeasurementSummary(
    string CommandKey,
    string ShapeId,
    int ShapeSequence,
    string CommandRole,
    IReadOnlyList<PlanSampleMeasurement> Samples,
    MedianSelection PlanningTimeMedian,
    MedianSelection ExecutionTimeMedian,
    MedianSelection SharedAccessBlocksMedian,
    MedianSelection TempAccessBlocksMedian,
    string ExecutionMedianPlanPath,
    string ExecutionMedianPlanSha256,
    bool AnySpill,
    IReadOnlyList<string> ScanTypes,
    IReadOnlyList<string> JoinTypes,
    IReadOnlyList<string> SortMethods,
    IReadOnlyList<string> IndexNames);

internal sealed record SequenceRunMeasurement(
    int RunNumber,
    decimal PlanningTimeMilliseconds,
    decimal ExecutionTimeMilliseconds,
    long SharedAccessBlocks,
    long TempAccessBlocks,
    bool Spilled);

internal sealed record SequenceMeasurementSummary(
    string SequenceId,
    IReadOnlyList<string> CommandKeys,
    IReadOnlyList<SequenceRunMeasurement> Runs,
    MedianSelection PlanningTimeMedian,
    MedianSelection ExecutionTimeMedian,
    MedianSelection SharedAccessBlocksMedian,
    MedianSelection TempAccessBlocksMedian,
    bool AnySpill);

internal sealed record Q1GateResult(
    bool Passed,
    decimal FilteredCountMedianMilliseconds,
    decimal FirstPageSequenceMedianMilliseconds,
    bool AnyWarmUpOrMeasuredSpill,
    IReadOnlyList<string> Reasons);

internal sealed record BaselineMeasurementsRaw(
    string BaselineRunId,
    DateTime VerifiedAtUtc,
    int CommandCount,
    int SampleCount,
    int WarmUpSampleCount,
    int MeasuredSampleCount,
    IReadOnlyList<PlanSampleMeasurement> Samples,
    IReadOnlyList<CommandMeasurementSummary> Commands,
    IReadOnlyList<SequenceMeasurementSummary> Sequences,
    Q1GateResult Q1Gate,
    IReadOnlyList<string> Anomalies);

internal sealed record CuratedCommandMeasurement(
    string CommandKey,
    string ShapeId,
    int ShapeSequence,
    string CommandRole,
    MedianSelection PlanningTimeMedian,
    MedianSelection ExecutionTimeMedian,
    MedianSelection SharedAccessBlocksMedian,
    MedianSelection TempAccessBlocksMedian,
    string ExecutionMedianPlanPath,
    string ExecutionMedianPlanSha256,
    bool AnySpill,
    IReadOnlyList<string> ScanTypes,
    IReadOnlyList<string> JoinTypes,
    IReadOnlyList<string> SortMethods,
    IReadOnlyList<string> IndexNames);

internal sealed record CuratedBaselineMeasurements(
    string BaselineRunId,
    DateTime VerifiedAtUtc,
    int CommandCount,
    int SampleCount,
    IReadOnlyList<CuratedCommandMeasurement> Commands,
    IReadOnlyList<SequenceMeasurementSummary> Sequences,
    Q1GateResult Q1Gate,
    IReadOnlyList<string> Anomalies,
    PermanentEvidenceMetadata? PermanentEvidence = null);

internal sealed record LockedQueryShapeExpectation(
    string ShapeId,
    int ExpectedTotalCount,
    int ExpectedItemCount,
    IReadOnlyList<Guid> ExpectedOrderedIds);

internal sealed record ProfileVerificationEvidence(
    string ProfileIdentity,
    long ListingCount,
    long TranslationCount,
    int InvariantTotal,
    int InvariantPassed,
    int InvariantFailed,
    bool Passed);

internal sealed record SemanticResultIdentityEvidence(
    string ExpectedResultSha256,
    string ActualResultSha256,
    bool ComparisonPassed);

internal sealed record LockedResultComparisonEvidence(
    string ShapeId,
    int ExpectedTotalCount,
    int ActualTotalCount,
    bool TotalCountComparisonPassed,
    int ExpectedItemCount,
    int ActualItemCount,
    bool ItemCountComparisonPassed,
    IReadOnlyList<Guid> ExpectedOrderedIds,
    IReadOnlyList<Guid> ActualOrderedIds,
    string ExpectedOrderedIdsSha256,
    string ActualOrderedIdsSha256,
    bool OrderedIdsComparisonPassed,
    bool Passed);

internal sealed record A1SequenceExceptionEvidence(
    string SequenceId,
    decimal CorrectedPreIndexMilliseconds,
    decimal IndexedRunMilliseconds,
    decimal DifferenceMilliseconds,
    decimal AbsoluteDifferenceMilliseconds,
    decimal RelativeDifferencePercent,
    bool AbsoluteDifferenceBelowOneMillisecond,
    long ExpectedSharedAccessBlocks,
    long ActualSharedAccessBlocks,
    bool SharedAccessBlocksEquivalent);

internal sealed record A1CommandTopologyEvidence(
    string CommandKey,
    IReadOnlyList<string> ExpectedNodeTypes,
    IReadOnlyList<string> ActualNodeTypes,
    IReadOnlyList<string> ExpectedScanTypes,
    IReadOnlyList<string> ActualScanTypes,
    IReadOnlyList<string> ExpectedJoinTypes,
    IReadOnlyList<string> ActualJoinTypes,
    IReadOnlyList<string> ExpectedIndexNames,
    IReadOnlyList<string> ActualIndexNames,
    bool ComparisonPassed);

internal sealed record A1ApprovedExceptionEvidence(
    A1SequenceExceptionEvidence FirstPage,
    A1SequenceExceptionEvidence Supplementary,
    IReadOnlyList<A1CommandTopologyEvidence> CommandTopologies,
    bool BuffersEquivalent,
    bool ScanJoinIndexTopologyUnchanged,
    bool NoNewExpensiveNode,
    bool Accepted);

internal sealed record TrigramIndexEvidence(
    string ExtensionName,
    string ExtensionVersion,
    string IndexName,
    string AccessMethod,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> OperatorClasses,
    bool IsValid,
    bool IsReady,
    bool IsLive,
    long SizeBytes);

internal sealed record CaptureIdentityEvidence(
    string GitCommit,
    string GitBranch,
    string PostgreSqlVersion,
    TrigramIndexEvidence TrigramIndex,
    int CommandCount,
    int TypedParameterCount,
    int RawPlanCount,
    int WarmUpRounds,
    int MeasuredRounds,
    int SpillCount,
    int PlanSwitchCount,
    int AnomalyCount,
    int CredentialFindingCount);

internal sealed record ArtifactHashEvidence(
    string Path,
    string Sha256);

internal sealed record ArtifactIntegrityEvidence(
    string Algorithm,
    string Canonicalization,
    string ManifestPath,
    string ManifestTrustAnchor,
    IReadOnlyList<ArtifactHashEvidence> RootArtifacts,
    IReadOnlyList<ArtifactHashEvidence> NormalizedSqlArtifacts,
    IReadOnlyList<ArtifactHashEvidence> MedianPlanArtifacts);

internal sealed record PermanentEvidenceMetadata(
    int SchemaVersion,
    ProfileVerificationEvidence ProfileVerification,
    SemanticResultIdentityEvidence SemanticResultIdentity,
    IReadOnlyList<LockedResultComparisonEvidence> LockedResults,
    A1ApprovedExceptionEvidence A1ApprovedException,
    CaptureIdentityEvidence CaptureIdentity,
    ArtifactIntegrityEvidence ArtifactIntegrity);

internal sealed record BaselineVerificationResult(
    BaselineMeasurementsRaw Measurements,
    string RunDirectory,
    string MeasurementsPath,
    string CuratedDirectory,
    bool CredentialScanPassed);

internal static class SqlCaptureOutput
{
    public static async Task<string> WriteAsync(
        SqlCaptureRun run,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var runDirectory = Path.GetFullPath(
            Path.Combine(outputDirectory, run.LogicalRunId));

        Directory.CreateDirectory(runDirectory);

        var outputPath = Path.Combine(runDirectory, "captured-commands.json");
        await JsonArtifactOutput.WriteAsync(outputPath, run, cancellationToken);

        return outputPath;
    }
}

internal static class JsonArtifactOutput
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteAsync<T>(
        string outputPath,
        T value,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException(
                $"Output path '{outputPath}' has no parent directory.");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
}
