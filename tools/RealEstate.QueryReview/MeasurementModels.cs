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
    long SizeBytes);

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
    IReadOnlyList<RawPlanSample> Samples);

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
    IReadOnlyList<string> Anomalies);

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
