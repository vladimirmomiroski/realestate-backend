using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace RealEstate.QueryReview;

internal static class ExplainRunner
{
    private const string ExplainPrefix =
        "EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, FORMAT JSON)\n";
    private const int ExpectedCommandCount = 33;
    private const int ExpectedParameterCount = 80;
    private const int WarmUpRunsPerCommand = 1;
    private const int MeasuredRunsPerCommand = 5;
    private const int ExpectedProfileInvariantCount = 61;
    private const long ExpectedListingCount = 100_000;
    private const long ExpectedTranslationCount = 200_000;
    private const int ExpectedPlanCount =
        ExpectedCommandCount * (WarmUpRunsPerCommand + MeasuredRunsPerCommand);

    private static readonly string[] ExpectedCommandKeys =
    [
        "N1-01-filtered-count",
        "N1-02-page-root",
        "N1-03-translation-split",
        "N1-04-image-split",
        "P1-01-filtered-count",
        "P1-02-page-root",
        "P1-03-translation-split",
        "P1-04-image-split",
        "P2-01-filtered-count",
        "P2-02-page-root",
        "P2-03-translation-split",
        "P2-04-image-split",
        "A1-01-agency-existence",
        "A1-02-filtered-count",
        "A1-03-page-root",
        "A1-04-translation-split",
        "A1-05-image-split",
        "R1-01-filtered-count",
        "R1-02-page-root",
        "R1-03-translation-split",
        "R1-04-image-split",
        "L1-01-filtered-count",
        "L1-02-page-root",
        "L1-03-translation-split",
        "L1-04-image-split",
        "Q1-01-filtered-count",
        "Q1-02-page-root",
        "Q1-03-translation-split",
        "Q1-04-image-split",
        "C1-01-comparable-source",
        "C1-02-comparable-ranked-root",
        "C1-03-comparable-translation-split",
        "C1-04-comparable-image-split"
    ];

    private static readonly SequenceDefinition[] SequenceDefinitions =
    [
        FirstPage("N1"),
        FirstPage("P1"),
        FirstPage("P2"),
        new(
            "A1-first-page",
            ["A1-03-page-root", "A1-04-translation-split", "A1-05-image-split"]),
        new(
            "A1-endpoint-supplementary",
            [
                "A1-01-agency-existence",
                "A1-02-filtered-count",
                "A1-03-page-root",
                "A1-04-translation-split",
                "A1-05-image-split"
            ]),
        FirstPage("R1"),
        FirstPage("L1"),
        FirstPage("Q1"),
        new(
            "C1-candidate-page",
            [
                "C1-02-comparable-ranked-root",
                "C1-03-comparable-translation-split",
                "C1-04-comparable-image-split"
            ]),
        new(
            "C1-endpoint-supplementary",
            [
                "C1-01-comparable-source",
                "C1-02-comparable-ranked-root",
                "C1-03-comparable-translation-split",
                "C1-04-comparable-image-split"
            ])
    ];

    public static async Task<RawBaselineManifest> RunAsync(
        NpgsqlConnection connection,
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        ProductionCaptureSession captureSession,
        BaselineEnvironmentSnapshot environment,
        DeterministicProfileVerificationSnapshot profileVerification,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateConnectionSettings(connectionStringBuilder);
        ValidateCaptureSession(captureSession);
        ValidateProfileVerification(profileVerification);

        if (environment.PostgreSql.ActiveVacuumCount != 0)
        {
            throw new BaselinePlanValidationException(
                $"An official baseline cannot start while " +
                $"{environment.PostgreSql.ActiveVacuumCount} vacuum operation(s) are active.");
        }

        var startedAtUtc = DateTime.UtcNow;
        var shortCommit = environment.Git.Commit[..Math.Min(8, environment.Git.Commit.Length)];
        var baselineRunId =
            $"{DeterministicProfileSeeder.ProfileVersion}-baseline-" +
            $"{startedAtUtc:yyyyMMddTHHmmssZ}-{shortCommit}";
        var runDirectory = Path.GetFullPath(Path.Combine(outputDirectory, baselineRunId));

        if (Directory.Exists(runDirectory))
        {
            throw new BaselinePlanValidationException(
                $"Baseline output directory '{runDirectory}' already exists.");
        }

        Directory.CreateDirectory(runDirectory);

        var capturedCommandsPath = Path.Combine(runDirectory, "captured-commands.json");
        var environmentPath = Path.Combine(runDirectory, "environment-raw.json");
        await JsonArtifactOutput.WriteAsync(
            capturedCommandsPath,
            captureSession.CaptureRun,
            cancellationToken);
        await JsonArtifactOutput.WriteAsync(environmentPath, environment, cancellationToken);

        var resultSha256 = ComputeSha256(JsonSerializer.Serialize(
            captureSession.CaptureRun.ShapeResults,
            JsonArtifactOutput.SerializerOptions));
        var structuralHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var rowCounts = new Dictionary<string, (long Rows, long Loops)>(StringComparer.Ordinal);
        var samples = new List<RawPlanSample>(ExpectedCommandCount * 6);

        for (var round = 0; round <= MeasuredRunsPerCommand; round++)
        {
            var runKind = round == 0 ? "warmup" : "measured";
            var runNumber = round;

            foreach (var replayableCommand in captureSession.ReplayableCommands)
            {
                var captured = replayableCommand.CapturedCommand;
                var commandKey = CreateCommandKey(captured);
                var sqlSha256 = ComputeSha256(captured.CommandText);
                var parameterSha256 = ComputeParameterSha256(captured.Parameters);
                var planFileName = round == 0 ? "warmup.json" : $"run-{round}.json";
                var relativePlanPath = Path.Combine(
                    "raw-plans",
                    commandKey,
                    planFileName);
                var planPath = Path.Combine(runDirectory, relativePlanPath);
                var execution = await ExecuteExplainAsync(
                    connection,
                    replayableCommand,
                    cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
                await File.WriteAllTextAsync(planPath, execution.RawJson, cancellationToken);

                if (structuralHashes.TryGetValue(commandKey, out var expectedStructuralHash))
                {
                    if (!string.Equals(
                            expectedStructuralHash,
                            execution.StructuralPlanSha256,
                            StringComparison.Ordinal))
                    {
                        throw new BaselinePlanValidationException(
                            $"{commandKey}: structural plan changed between baseline rounds.");
                    }
                }
                else
                {
                    structuralHashes.Add(commandKey, execution.StructuralPlanSha256);
                }

                if (rowCounts.TryGetValue(commandKey, out var expectedRows))
                {
                    if (expectedRows.Rows != execution.ActualRows ||
                        expectedRows.Loops != execution.ActualLoops)
                    {
                        throw new BaselinePlanValidationException(
                            $"{commandKey}: top-level actual rows/loops changed. " +
                            $"Expected {expectedRows.Rows}/{expectedRows.Loops}, actual " +
                            $"{execution.ActualRows}/{execution.ActualLoops}.");
                    }
                }
                else
                {
                    rowCounts.Add(commandKey, (execution.ActualRows, execution.ActualLoops));
                }

                samples.Add(new RawPlanSample(
                    commandKey,
                    captured.ShapeId,
                    captured.ShapeSequence,
                    captured.CommandRole,
                    runKind,
                    runNumber,
                    relativePlanPath.Replace(Path.DirectorySeparatorChar, '/'),
                    sqlSha256,
                    parameterSha256,
                    execution.StructuralPlanSha256,
                    execution.ActualRows,
                    execution.ActualLoops));
            }
        }

        var expectedPlanCount = ExpectedCommandCount *
                                (WarmUpRunsPerCommand + MeasuredRunsPerCommand);

        if (samples.Count != expectedPlanCount)
        {
            throw new BaselinePlanValidationException(
                $"Expected {expectedPlanCount} raw plans, captured {samples.Count}.");
        }

        var manifest = new RawBaselineManifest(
            baselineRunId,
            startedAtUtc,
            DateTime.UtcNow,
            DeterministicProfileSeeder.ProfileVersion,
            DeterministicProfileSeeder.CSharpSeed,
            DeterministicProfileSeeder.PostgreSqlSeed,
            environment.Git.Commit,
            environment.PostgreSql.ServerVersion,
            resultSha256,
            ExpectedCommandCount,
            ExpectedParameterCount,
            WarmUpRunsPerCommand,
            MeasuredRunsPerCommand,
            samples.Count,
            "captured-commands.json",
            "environment-raw.json",
            CredentialScanPassed: true,
            samples,
            profileVerification);
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        await JsonArtifactOutput.WriteAsync(manifestPath, manifest, cancellationToken);

        ScanOutputForCredentials(runDirectory, connectionStringBuilder);
        return manifest;
    }

    public static async Task<BaselineVerificationResult> VerifyAsync(
        string runDirectory,
        CancellationToken cancellationToken = default)
    {
        var fullRunDirectory = Path.GetFullPath(runDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!Path.IsPathFullyQualified(runDirectory) || !Directory.Exists(fullRunDirectory))
        {
            throw new BaselinePlanValidationException(
                $"Raw baseline run directory '{runDirectory}' does not exist as an absolute path.");
        }

        QueryShapeDefinitions.EnsureOutputIsOutsideRepository(fullRunDirectory);

        var manifest = await ReadRequiredJsonAsync<RawBaselineManifest>(
            Path.Combine(fullRunDirectory, "manifest.json"),
            cancellationToken);
        var captureRun = await ReadRequiredJsonAsync<SqlCaptureRun>(
            ResolveArtifactPath(fullRunDirectory, manifest.CapturedCommandsPath),
            cancellationToken);
        var environment = await ReadRequiredJsonAsync<BaselineEnvironmentSnapshot>(
            ResolveArtifactPath(fullRunDirectory, manifest.EnvironmentPath),
            cancellationToken);

        ValidateOfflineInputs(fullRunDirectory, manifest, captureRun, environment);

        var sampleMeasurements = new List<PlanSampleMeasurement>(ExpectedPlanCount);
        var expectedPlanPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var round = 0; round <= MeasuredRunsPerCommand; round++)
        {
            for (var commandIndex = 0; commandIndex < ExpectedCommandCount; commandIndex++)
            {
                var manifestIndex = (round * ExpectedCommandCount) + commandIndex;
                var sample = manifest.Samples[manifestIndex];
                var command = captureRun.Commands[commandIndex];
                var commandKey = ExpectedCommandKeys[commandIndex];
                var expectedRunKind = round == 0 ? "warmup" : "measured";
                var expectedPlanPath = CreateRelativePlanPath(commandKey, round);

                ValidateManifestSample(
                    sample,
                    command,
                    commandKey,
                    expectedRunKind,
                    round,
                    expectedPlanPath);

                var planPath = ResolveArtifactPath(fullRunDirectory, sample.RelativePlanPath);
                expectedPlanPaths.Add(Path.GetFullPath(planPath));

                if (!File.Exists(planPath))
                {
                    throw new BaselinePlanValidationException(
                        $"{commandKey}: required plan '{sample.RelativePlanPath}' is missing.");
                }

                var rawJson = await File.ReadAllTextAsync(planPath, cancellationToken);
                var measurement = ParsePlanMeasurement(sample, rawJson);

                if (!string.Equals(
                        measurement.StructuralPlanSha256,
                        sample.StructuralPlanSha256,
                        StringComparison.Ordinal) ||
                    measurement.ActualRows != sample.ActualRows ||
                    measurement.ActualLoops != sample.ActualLoops)
                {
                    throw new BaselinePlanValidationException(
                        $"{commandKey}/{expectedRunKind}-{round}: plan hash or row metadata drifted.");
                }

                sampleMeasurements.Add(measurement);
            }
        }

        ValidateRawPlanFileSet(fullRunDirectory, expectedPlanPaths);
        var commandSummaries = BuildCommandSummaries(sampleMeasurements);
        var sequenceSummaries = BuildSequenceSummaries(commandSummaries);
        var q1Gate = EvaluateQ1Gate(commandSummaries, sequenceSummaries);
        var verifiedAtUtc = DateTime.UtcNow;
        var measurements = new BaselineMeasurementsRaw(
            manifest.BaselineRunId,
            verifiedAtUtc,
            ExpectedCommandCount,
            sampleMeasurements.Count,
            ExpectedCommandCount,
            ExpectedCommandCount * MeasuredRunsPerCommand,
            sampleMeasurements,
            commandSummaries,
            sequenceSummaries,
            q1Gate,
            Array.Empty<string>());

        var measurementsPath = Path.Combine(fullRunDirectory, "measurements-raw.json");
        await JsonArtifactOutput.WriteAsync(measurementsPath, measurements, cancellationToken);
        var curatedDirectory = await WriteTemporaryCuratedEvidenceAsync(
            fullRunDirectory,
            environment,
            captureRun,
            measurements,
            cancellationToken);

        ScanOutputForCredentials(fullRunDirectory);

        return new BaselineVerificationResult(
            measurements,
            fullRunDirectory,
            measurementsPath,
            curatedDirectory,
            CredentialScanPassed: true);
    }

    private static void ValidateOfflineInputs(
        string runDirectory,
        RawBaselineManifest manifest,
        SqlCaptureRun captureRun,
        BaselineEnvironmentSnapshot environment)
    {
        if (!string.Equals(
                Path.GetFileName(runDirectory),
                manifest.BaselineRunId,
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Raw-run directory name does not match the manifest baseline run ID.");
        }

        if (manifest.CommandCount != ExpectedCommandCount ||
            manifest.ParameterCount != ExpectedParameterCount ||
            manifest.WarmUpRunsPerCommand != WarmUpRunsPerCommand ||
            manifest.MeasuredRunsPerCommand != MeasuredRunsPerCommand ||
            manifest.PlanCount != ExpectedPlanCount ||
            manifest.Samples.Count != ExpectedPlanCount ||
            !manifest.CredentialScanPassed)
        {
            throw new BaselinePlanValidationException(
                $"Raw manifest does not contain exactly 33 commands, {ExpectedParameterCount} parameters, " +
                "one warm-up and five measured plans per command, and a passed credential scan.");
        }

        ValidateProfileVerification(manifest.ProfileVerification);

        if (!string.Equals(manifest.CapturedCommandsPath, "captured-commands.json", StringComparison.Ordinal) ||
            !string.Equals(manifest.EnvironmentPath, "environment-raw.json", StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Raw manifest references unexpected capture or environment artifact paths.");
        }

        if (captureRun.Commands.Count != ExpectedCommandCount ||
            captureRun.Commands.Sum(command => command.Parameters.Count) != ExpectedParameterCount)
        {
            throw new BaselinePlanValidationException(
                "Captured production artifact does not contain the required 33 commands and " +
                $"{ExpectedParameterCount} typed parameters.");
        }

        for (var index = 0; index < ExpectedCommandCount; index++)
        {
            var actualKey = CreateCommandKey(captureRun.Commands[index]);

            if (!string.Equals(actualKey, ExpectedCommandKeys[index], StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Fixed command order drifted at position {index + 1}: expected " +
                    $"'{ExpectedCommandKeys[index]}', actual '{actualKey}'.");
            }
        }

        var resultSha256 = ComputeSha256(JsonSerializer.Serialize(
            captureRun.ShapeResults,
            JsonArtifactOutput.SerializerOptions));

        if (!string.Equals(resultSha256, manifest.ResultSha256, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Production semantic-result hash does not match the raw manifest.");
        }

        if (!string.Equals(captureRun.ProfileVersion, manifest.ProfileVersion, StringComparison.Ordinal) ||
            captureRun.CSharpSeed != manifest.CSharpSeed ||
            captureRun.PostgreSqlSeed != manifest.PostgreSqlSeed ||
            !string.Equals(environment.ProfileVersion, manifest.ProfileVersion, StringComparison.Ordinal) ||
            environment.CSharpSeed != manifest.CSharpSeed ||
            environment.PostgreSqlSeed != manifest.PostgreSqlSeed ||
            !string.Equals(environment.Git.Commit, manifest.GitCommit, StringComparison.Ordinal) ||
            !string.Equals(
                environment.PostgreSql.ServerVersion,
                manifest.PostgreSqlVersion,
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Profile, seed, Git, or PostgreSQL identity drift exists between raw artifacts.");
        }
    }

    private static void ValidateProfileVerification(
        DeterministicProfileVerificationSnapshot? profileVerification)
    {
        if (profileVerification is null ||
            !string.Equals(
                profileVerification.ProfileIdentity,
                DeterministicProfileSeeder.ProfileVersion,
                StringComparison.Ordinal) ||
            profileVerification.ListingCount != ExpectedListingCount ||
            profileVerification.TranslationCount != ExpectedTranslationCount ||
            profileVerification.InvariantTotal != ExpectedProfileInvariantCount ||
            profileVerification.InvariantPassed != ExpectedProfileInvariantCount ||
            profileVerification.InvariantFailed != 0)
        {
            throw new BaselinePlanValidationException(
                "Raw manifest lacks the complete successful chapter-10f-v1 61-invariant " +
                "profile verification required for baseline verification and export.");
        }
    }

    private static void ValidateManifestSample(
        RawPlanSample sample,
        CapturedCommand command,
        string commandKey,
        string expectedRunKind,
        int expectedRunNumber,
        string expectedPlanPath)
    {
        var sqlSha256 = ComputeSha256(command.CommandText);
        var parameterSha256 = ComputeParameterSha256(command.Parameters);

        if (!string.Equals(sample.CommandKey, commandKey, StringComparison.Ordinal) ||
            !string.Equals(sample.ShapeId, command.ShapeId, StringComparison.Ordinal) ||
            sample.ShapeSequence != command.ShapeSequence ||
            !string.Equals(sample.CommandRole, command.CommandRole, StringComparison.Ordinal) ||
            !string.Equals(sample.RunKind, expectedRunKind, StringComparison.Ordinal) ||
            sample.RunNumber != expectedRunNumber ||
            !string.Equals(sample.RelativePlanPath, expectedPlanPath, StringComparison.Ordinal) ||
            !string.Equals(sample.SqlSha256, sqlSha256, StringComparison.Ordinal) ||
            !string.Equals(sample.ParameterSha256, parameterSha256, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                $"{commandKey}: sample order, role, path, SQL hash, or parameter hash drifted.");
        }
    }

    private static void ValidateRawPlanFileSet(
        string runDirectory,
        IReadOnlySet<string> expectedPlanPaths)
    {
        var rawPlansDirectory = Path.Combine(runDirectory, "raw-plans");

        if (!Directory.Exists(rawPlansDirectory))
        {
            throw new BaselinePlanValidationException("Raw plan directory is missing.");
        }

        var actualPlanPaths = Directory
            .EnumerateFiles(rawPlansDirectory, "*.json", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (actualPlanPaths.Count != ExpectedPlanCount ||
            !actualPlanPaths.SetEquals(expectedPlanPaths))
        {
            throw new BaselinePlanValidationException(
                $"Raw plan file set must contain exactly the expected {ExpectedPlanCount} files.");
        }
    }

    private static PlanSampleMeasurement ParsePlanMeasurement(
        RawPlanSample sample,
        string rawJson)
    {
        JsonDocument planDocument;

        try
        {
            planDocument = JsonDocument.Parse(rawJson);
        }
        catch (JsonException exception)
        {
            throw new BaselinePlanValidationException(
                $"{sample.CommandKey}/{sample.RunKind}-{sample.RunNumber}: malformed plan JSON: " +
                exception.Message);
        }

        using (planDocument)
        {
            var root = planDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1 ||
                root[0].ValueKind != JsonValueKind.Object ||
                !root[0].TryGetProperty("Plan", out var topPlan))
            {
                throw new BaselinePlanValidationException(
                    $"{sample.CommandKey}: EXPLAIN artifact has no single top-level plan.");
            }

            var planningTime = ReadRequiredDecimal(root[0], "Planning Time");
            var executionTime = ReadRequiredDecimal(root[0], "Execution Time");
            var settings = ReadSettings(root[0]);
            var nodes = new List<PlanNodeMeasurement>();
            var spillReasons = new List<string>();
            ReadPlanNode(topPlan, "0", 0, nodes, spillReasons);

            if (nodes.Count == 0)
            {
                throw new BaselinePlanValidationException(
                    $"{sample.CommandKey}: plan contains no executable nodes.");
            }

            var topNode = nodes[0];
            var rawPlanSha256 = ComputeSha256(rawJson);
            var structuralPlanSha256 = ComputeSha256(CreateStructuralPlanJson(root));
            var scanTypes = DistinctOrdered(nodes
                .Where(node => node.NodeType.Contains("Scan", StringComparison.Ordinal))
                .Select(node => node.NodeType));
            var joinTypes = DistinctOrdered(nodes
                .Where(node => node.JoinType is not null ||
                               node.NodeType.Contains("Join", StringComparison.Ordinal))
                .Select(node => node.JoinType ?? node.NodeType));
            var sortMethods = DistinctOrdered(nodes
                .Where(node => node.SortMethod is not null)
                .Select(node => node.SortMethod!));
            var indexNames = DistinctOrdered(nodes
                .Where(node => node.IndexName is not null)
                .Select(node => node.IndexName!));
            var totalRowsRemoved = nodes.Sum(node =>
                node.RowsRemovedByFilter +
                node.RowsRemovedByIndexRecheck +
                node.RowsRemovedByJoinFilter);
            var maximumPeakMemory = nodes
                .Where(node => node.PeakMemoryUsageKilobytes.HasValue)
                .Select(node => node.PeakMemoryUsageKilobytes!.Value)
                .DefaultIfEmpty(0)
                .Max();

            return new PlanSampleMeasurement(
                sample.CommandKey,
                sample.ShapeId,
                sample.ShapeSequence,
                sample.CommandRole,
                sample.RunKind,
                sample.RunNumber,
                sample.RelativePlanPath,
                rawPlanSha256,
                sample.SqlSha256,
                sample.ParameterSha256,
                structuralPlanSha256,
                planningTime,
                executionTime,
                topNode.ActualRows,
                topNode.ActualLoops,
                topNode.Buffers,
                settings,
                spillReasons.Count > 0,
                DistinctOrdered(spillReasons),
                scanTypes,
                joinTypes,
                sortMethods,
                indexNames,
                totalRowsRemoved,
                maximumPeakMemory,
                nodes);
        }
    }

    private static void ReadPlanNode(
        JsonElement element,
        string path,
        int depth,
        ICollection<PlanNodeMeasurement> nodes,
        ICollection<string> spillReasons)
    {
        var nodeType = ReadRequiredString(element, "Node Type");
        var buffers = ReadBuffers(element);
        var sortMethod = ReadOptionalString(element, "Sort Method");
        var sortSpaceType = ReadOptionalString(element, "Sort Space Type");

        if (buffers.TempRead > 0 || buffers.TempWritten > 0)
        {
            spillReasons.Add(
                $"{path}/{nodeType}: temp blocks read={buffers.TempRead}, " +
                $"written={buffers.TempWritten}");
        }

        if (string.Equals(sortSpaceType, "Disk", StringComparison.OrdinalIgnoreCase))
        {
            spillReasons.Add($"{path}/{nodeType}: sort space type is Disk");
        }

        if (sortMethod is not null &&
            (sortMethod.Contains("external", StringComparison.OrdinalIgnoreCase) ||
             sortMethod.Contains("disk", StringComparison.OrdinalIgnoreCase)))
        {
            spillReasons.Add($"{path}/{nodeType}: sort method is {sortMethod}");
        }

        var node = new PlanNodeMeasurement(
            path,
            depth,
            nodeType,
            ReadOptionalString(element, "Parent Relationship"),
            ReadOptionalString(element, "Relation Name"),
            ReadOptionalString(element, "Schema"),
            ReadOptionalString(element, "Alias"),
            ReadOptionalString(element, "Scan Direction"),
            ReadOptionalString(element, "Index Name"),
            ReadOptionalString(element, "Join Type"),
            ReadOptionalDecimal(element, "Startup Cost"),
            ReadOptionalDecimal(element, "Total Cost"),
            ReadOptionalInt64(element, "Plan Rows"),
            ReadOptionalInt64(element, "Plan Width"),
            ReadOptionalDecimal(element, "Actual Startup Time"),
            ReadOptionalDecimal(element, "Actual Total Time"),
            ReadRequiredInt64(element, "Actual Rows"),
            ReadRequiredInt64(element, "Actual Loops"),
            ReadOptionalInt64(element, "Rows Removed by Filter") ?? 0,
            ReadOptionalInt64(element, "Rows Removed by Index Recheck") ?? 0,
            ReadOptionalInt64(element, "Rows Removed by Join Filter") ?? 0,
            ReadOptionalString(element, "Filter"),
            ReadOptionalString(element, "Index Cond"),
            ReadOptionalString(element, "Recheck Cond"),
            ReadOptionalString(element, "Join Filter"),
            ReadOptionalString(element, "Hash Cond"),
            ReadOptionalString(element, "Merge Cond"),
            ReadStringArray(element, "Sort Key"),
            sortMethod,
            ReadOptionalInt64(element, "Sort Space Used"),
            sortSpaceType,
            ReadOptionalInt64(element, "Hash Batches"),
            ReadOptionalInt64(element, "Peak Memory Usage"),
            buffers);
        nodes.Add(node);

        if (!element.TryGetProperty("Plans", out var plans))
        {
            return;
        }

        if (plans.ValueKind != JsonValueKind.Array)
        {
            throw new BaselinePlanValidationException(
                $"{path}/{nodeType}: nested Plans value is not an array.");
        }

        var childIndex = 0;

        foreach (var child in plans.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object)
            {
                throw new BaselinePlanValidationException(
                    $"{path}/{nodeType}: nested plan is not an object.");
            }

            ReadPlanNode(child, $"{path}.{childIndex}", depth + 1, nodes, spillReasons);
            childIndex++;
        }
    }

    private static IReadOnlyList<CommandMeasurementSummary> BuildCommandSummaries(
        IReadOnlyList<PlanSampleMeasurement> samples)
    {
        var summaries = new List<CommandMeasurementSummary>(ExpectedCommandCount);

        foreach (var commandKey in ExpectedCommandKeys)
        {
            var commandSamples = samples
                .Where(sample => string.Equals(sample.CommandKey, commandKey, StringComparison.Ordinal))
                .OrderBy(sample => sample.RunNumber)
                .ToArray();

            ValidateCompleteAndStableSamples(commandKey, commandSamples);
            var measured = commandSamples
                .Where(sample => string.Equals(sample.RunKind, "measured", StringComparison.Ordinal))
                .ToArray();
            var planningMedian = SelectMedian(
                measured.Select(sample => (sample.PlanningTimeMilliseconds, sample.RunNumber)));
            var executionMedian = SelectMedian(
                measured.Select(sample => (sample.ExecutionTimeMilliseconds, sample.RunNumber)));
            var sharedMedian = SelectMedian(measured.Select(sample =>
                ((decimal)(sample.TopLevelBuffers.SharedHit + sample.TopLevelBuffers.SharedRead),
                    sample.RunNumber)));
            var tempMedian = SelectMedian(measured.Select(sample =>
                ((decimal)(sample.TopLevelBuffers.TempRead + sample.TopLevelBuffers.TempWritten),
                    sample.RunNumber)));
            var executionMedianSample = measured.Single(sample =>
                sample.RunNumber == executionMedian.RunNumber);
            var first = commandSamples[0];

            summaries.Add(new CommandMeasurementSummary(
                commandKey,
                first.ShapeId,
                first.ShapeSequence,
                first.CommandRole,
                commandSamples,
                planningMedian,
                executionMedian,
                sharedMedian,
                tempMedian,
                executionMedianSample.RelativePlanPath,
                executionMedianSample.RawPlanSha256,
                commandSamples.Any(sample => sample.Spilled),
                DistinctOrdered(commandSamples.SelectMany(sample => sample.ScanTypes)),
                DistinctOrdered(commandSamples.SelectMany(sample => sample.JoinTypes)),
                DistinctOrdered(commandSamples.SelectMany(sample => sample.SortMethods)),
                DistinctOrdered(commandSamples.SelectMany(sample => sample.IndexNames))));
        }

        return summaries;
    }

    private static void ValidateCompleteAndStableSamples(
        string commandKey,
        IReadOnlyList<PlanSampleMeasurement> samples)
    {
        if (samples.Count != WarmUpRunsPerCommand + MeasuredRunsPerCommand ||
            samples[0].RunKind != "warmup" ||
            samples[0].RunNumber != 0 ||
            samples.Skip(1).Where((sample, index) =>
                sample.RunKind != "measured" || sample.RunNumber != index + 1).Any())
        {
            throw new BaselinePlanValidationException(
                $"{commandKey}: incomplete warm-up/measured sample set.");
        }

        if (samples.Select(sample => sample.SqlSha256).Distinct(StringComparer.Ordinal).Count() != 1 ||
            samples.Select(sample => sample.ParameterSha256).Distinct(StringComparer.Ordinal).Count() != 1 ||
            samples.Select(sample => sample.StructuralPlanSha256).Distinct(StringComparer.Ordinal).Count() != 1 ||
            samples.Select(sample => sample.ActualRows).Distinct().Count() != 1 ||
            samples.Select(sample => sample.ActualLoops).Distinct().Count() != 1 ||
            samples.Select(sample => sample.CommandRole).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            throw new BaselinePlanValidationException(
                $"{commandKey}: SQL, parameter, structure, row, loop, or role stability failed.");
        }
    }

    private static IReadOnlyList<SequenceMeasurementSummary> BuildSequenceSummaries(
        IReadOnlyList<CommandMeasurementSummary> commands)
    {
        var commandLookup = commands.ToDictionary(
            command => command.CommandKey,
            StringComparer.Ordinal);
        var summaries = new List<SequenceMeasurementSummary>(SequenceDefinitions.Length);

        foreach (var definition in SequenceDefinitions)
        {
            var sequenceCommands = definition.CommandKeys
                .Select(commandKey => commandLookup.TryGetValue(commandKey, out var command)
                    ? command
                    : throw new BaselinePlanValidationException(
                        $"{definition.SequenceId}: required command '{commandKey}' is missing."))
                .ToArray();
            var runs = new List<SequenceRunMeasurement>(MeasuredRunsPerCommand);

            for (var runNumber = 1; runNumber <= MeasuredRunsPerCommand; runNumber++)
            {
                var alignedSamples = sequenceCommands
                    .Select(command => command.Samples.Single(sample =>
                        sample.RunKind == "measured" && sample.RunNumber == runNumber))
                    .ToArray();

                runs.Add(new SequenceRunMeasurement(
                    runNumber,
                    alignedSamples.Sum(sample => sample.PlanningTimeMilliseconds),
                    alignedSamples.Sum(sample => sample.ExecutionTimeMilliseconds),
                    alignedSamples.Sum(sample =>
                        sample.TopLevelBuffers.SharedHit + sample.TopLevelBuffers.SharedRead),
                    alignedSamples.Sum(sample =>
                        sample.TopLevelBuffers.TempRead + sample.TopLevelBuffers.TempWritten),
                    alignedSamples.Any(sample => sample.Spilled)));
            }

            summaries.Add(new SequenceMeasurementSummary(
                definition.SequenceId,
                definition.CommandKeys,
                runs,
                SelectMedian(runs.Select(run => (run.PlanningTimeMilliseconds, run.RunNumber))),
                SelectMedian(runs.Select(run => (run.ExecutionTimeMilliseconds, run.RunNumber))),
                SelectMedian(runs.Select(run => ((decimal)run.SharedAccessBlocks, run.RunNumber))),
                SelectMedian(runs.Select(run => ((decimal)run.TempAccessBlocks, run.RunNumber))),
                runs.Any(run => run.Spilled)));
        }

        return summaries;
    }

    private static Q1GateResult EvaluateQ1Gate(
        IReadOnlyList<CommandMeasurementSummary> commands,
        IReadOnlyList<SequenceMeasurementSummary> sequences)
    {
        var filteredCount = commands.Single(command =>
            command.CommandKey == "Q1-01-filtered-count");
        var firstPage = sequences.Single(sequence => sequence.SequenceId == "Q1-first-page");
        var q1Commands = commands.Where(command => command.ShapeId == "Q1").ToArray();
        var reasons = new List<string>();

        if (filteredCount.ExecutionTimeMedian.Value > 250m)
        {
            reasons.Add(
                $"Q1 filtered-count median {filteredCount.ExecutionTimeMedian.Value} ms exceeds 250 ms.");
        }

        if (firstPage.ExecutionTimeMedian.Value > 250m)
        {
            reasons.Add(
                $"Q1 first-page sequence median {firstPage.ExecutionTimeMedian.Value} ms exceeds 250 ms.");
        }

        var spilledQ1Samples = q1Commands
            .SelectMany(command => command.Samples)
            .Where(sample => sample.Spilled)
            .ToArray();

        foreach (var sample in spilledQ1Samples)
        {
            reasons.Add(
                $"{sample.CommandKey}/{sample.RunKind}-{sample.RunNumber} spilled: " +
                string.Join("; ", sample.SpillReasons));
        }

        return new Q1GateResult(
            reasons.Count == 0,
            filteredCount.ExecutionTimeMedian.Value,
            firstPage.ExecutionTimeMedian.Value,
            spilledQ1Samples.Length > 0,
            reasons);
    }

    private static MedianSelection SelectMedian(
        IEnumerable<(decimal Value, int RunNumber)> values)
    {
        var ordered = values
            .OrderBy(value => value.Value)
            .ThenBy(value => value.RunNumber)
            .ToArray();

        if (ordered.Length != MeasuredRunsPerCommand)
        {
            throw new BaselinePlanValidationException(
                $"Median calculation requires exactly {MeasuredRunsPerCommand} measured values.");
        }

        return new MedianSelection(ordered[2].Value, ordered[2].RunNumber);
    }

    private static async Task<string> WriteTemporaryCuratedEvidenceAsync(
        string runDirectory,
        BaselineEnvironmentSnapshot environment,
        SqlCaptureRun captureRun,
        BaselineMeasurementsRaw measurements,
        CancellationToken cancellationToken)
    {
        var curatedDirectory = Path.Combine(runDirectory, "curated");
        var sqlDirectory = Path.Combine(curatedDirectory, "sql");
        var planDirectory = Path.Combine(curatedDirectory, "baseline-plans");
        Directory.CreateDirectory(sqlDirectory);
        Directory.CreateDirectory(planDirectory);

        await JsonArtifactOutput.WriteAsync(
            Path.Combine(curatedDirectory, "environment.json"),
            environment,
            cancellationToken);

        var curatedCommands = measurements.Commands.Select(command =>
            new CuratedCommandMeasurement(
                command.CommandKey,
                command.ShapeId,
                command.ShapeSequence,
                command.CommandRole,
                command.PlanningTimeMedian,
                command.ExecutionTimeMedian,
                command.SharedAccessBlocksMedian,
                command.TempAccessBlocksMedian,
                command.ExecutionMedianPlanPath,
                command.ExecutionMedianPlanSha256,
                command.AnySpill,
                command.ScanTypes,
                command.JoinTypes,
                command.SortMethods,
                command.IndexNames)).ToArray();
        var curatedMeasurements = new CuratedBaselineMeasurements(
            measurements.BaselineRunId,
            measurements.VerifiedAtUtc,
            measurements.CommandCount,
            measurements.SampleCount,
            curatedCommands,
            measurements.Sequences,
            measurements.Q1Gate,
            measurements.Anomalies);
        await JsonArtifactOutput.WriteAsync(
            Path.Combine(curatedDirectory, "baseline-measurements.json"),
            curatedMeasurements,
            cancellationToken);

        for (var index = 0; index < ExpectedCommandCount; index++)
        {
            var commandKey = ExpectedCommandKeys[index];
            var command = captureRun.Commands[index];
            var summary = measurements.Commands[index];
            var sqlPath = Path.Combine(sqlDirectory, $"{commandKey}.sql");
            var sqlText = BuildCuratedSql(commandKey, command);
            await File.WriteAllTextAsync(sqlPath, sqlText, cancellationToken);

            var sourcePlanPath = ResolveArtifactPath(
                runDirectory,
                summary.ExecutionMedianPlanPath);
            var destinationPlanPath = Path.Combine(planDirectory, $"{commandKey}.json");
            File.Copy(sourcePlanPath, destinationPlanPath, overwrite: true);
        }

        var summaryMarkdown = BuildSummaryMarkdown(measurements);
        await File.WriteAllTextAsync(
            Path.Combine(curatedDirectory, "baseline-summary.md"),
            summaryMarkdown,
            cancellationToken);

        return curatedDirectory;
    }

    private static string BuildCuratedSql(string commandKey, CapturedCommand command)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"-- {commandKey}");

        foreach (var parameter in command.Parameters)
        {
            builder.Append("-- ")
                .Append(parameter.Name)
                .Append(": CLR=")
                .Append(parameter.ClrType)
                .Append(", DbType=")
                .Append(parameter.DbType)
                .Append(", NpgsqlDbType=")
                .Append(parameter.NpgsqlDbType ?? "n/a")
                .Append(", Nullable=")
                .Append(parameter.IsNullable)
                .Append(", Value=")
                .AppendLine(parameter.Value);
        }

        builder.AppendLine(command.CommandText);
        return builder.ToString();
    }

    private static string BuildSummaryMarkdown(BaselineMeasurementsRaw measurements)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Chapter 10F temporary baseline summary");
        builder.AppendLine();
        builder.AppendLine($"Run: `{measurements.BaselineRunId}`");
        builder.AppendLine();
        builder.AppendLine("## Command medians");
        builder.AppendLine();
        builder.AppendLine("| Command | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |");
        builder.AppendLine("|---|---:|---:|---:|---:|---|");

        foreach (var command in measurements.Commands)
        {
            builder.Append("| ").Append(command.CommandKey)
                .Append(" | ").Append(command.PlanningTimeMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(command.ExecutionTimeMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(command.SharedAccessBlocksMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(command.TempAccessBlocksMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(command.AnySpill ? "yes" : "no")
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Sequence medians");
        builder.AppendLine();
        builder.AppendLine("| Sequence | Planning ms | Execution ms | Shared hit+read | Temp read+write | Spill |");
        builder.AppendLine("|---|---:|---:|---:|---:|---|");

        foreach (var sequence in measurements.Sequences)
        {
            builder.Append("| ").Append(sequence.SequenceId)
                .Append(" | ").Append(sequence.PlanningTimeMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(sequence.ExecutionTimeMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(sequence.SharedAccessBlocksMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(sequence.TempAccessBlocksMedian.Value.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(sequence.AnySpill ? "yes" : "no")
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine($"## Q1 gate: {(measurements.Q1Gate.Passed ? "PASS" : "FAIL")}");
        builder.AppendLine();
        builder.AppendLine(
            $"Filtered-count median: {measurements.Q1Gate.FilteredCountMedianMilliseconds.ToString(CultureInfo.InvariantCulture)} ms.");
        builder.AppendLine(
            $"First-page sequence median: {measurements.Q1Gate.FirstPageSequenceMedianMilliseconds.ToString(CultureInfo.InvariantCulture)} ms.");

        if (measurements.Q1Gate.Reasons.Count == 0)
        {
            builder.AppendLine("No gate failure reasons.");
        }
        else
        {
            foreach (var reason in measurements.Q1Gate.Reasons)
            {
                builder.Append("- ").AppendLine(reason);
            }
        }

        return builder.ToString();
    }

    private static void ValidateConnectionSettings(
        NpgsqlConnectionStringBuilder connectionStringBuilder)
    {
        if (connectionStringBuilder.Multiplexing)
        {
            throw new BaselinePlanValidationException(
                "Official baseline replay rejects Npgsql multiplexing.");
        }

        if (connectionStringBuilder.MaxAutoPrepare > 0)
        {
            throw new BaselinePlanValidationException(
                "Official baseline replay rejects Npgsql auto-prepare.");
        }
    }

    private static void ValidateCaptureSession(ProductionCaptureSession captureSession)
    {
        if (captureSession.CaptureRun.Commands.Count != ExpectedCommandCount ||
            captureSession.ReplayableCommands.Count != ExpectedCommandCount)
        {
            throw new BaselinePlanValidationException(
                $"Expected {ExpectedCommandCount} captured/replayable commands; actual " +
                $"{captureSession.CaptureRun.Commands.Count}/" +
                $"{captureSession.ReplayableCommands.Count}.");
        }

        var parameterCount = captureSession.CaptureRun.Commands
            .Sum(command => command.Parameters.Count);

        if (parameterCount != ExpectedParameterCount)
        {
            throw new BaselinePlanValidationException(
                $"Expected {ExpectedParameterCount} typed parameters, captured {parameterCount}.");
        }

        var commandKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < ExpectedCommandCount; index++)
        {
            var captured = captureSession.CaptureRun.Commands[index];
            var replayable = captureSession.ReplayableCommands[index];

            if (!ReferenceEquals(captured, replayable.CapturedCommand) &&
                captured != replayable.CapturedCommand)
            {
                throw new BaselinePlanValidationException(
                    $"Captured and replayable command {index + 1} do not match.");
            }

            if (!string.Equals(captured.CommandType, CommandType.Text.ToString(), StringComparison.Ordinal) ||
                !captured.CommandText.TrimStart().StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
            {
                throw new BaselinePlanValidationException(
                    $"{captured.ShapeId}/{captured.CommandRole}: only captured SELECT text commands " +
                    "may be replayed.");
            }

            if (captured.Parameters.Count != replayable.Parameters.Count)
            {
                throw new BaselinePlanValidationException(
                    $"{captured.ShapeId}/{captured.CommandRole}: captured/replay parameter counts differ.");
            }

            var commandKey = CreateCommandKey(captured);

            if (!commandKeys.Add(commandKey))
            {
                throw new BaselinePlanValidationException(
                    $"Duplicate command key '{commandKey}'.");
            }
        }
    }

    private static async Task<ExplainExecution> ExecuteExplainAsync(
        NpgsqlConnection connection,
        ReplayableCommand replayableCommand,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandType = CommandType.Text;
        command.CommandText = ExplainPrefix + replayableCommand.CapturedCommand.CommandText;

        foreach (var replayableParameter in replayableCommand.Parameters.OrderBy(parameter => parameter.Ordinal))
        {
            command.Parameters.Add(CreateParameter(replayableParameter));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new BaselinePlanValidationException(
                $"{replayableCommand.CapturedCommand.ShapeId}/" +
                $"{replayableCommand.CapturedCommand.CommandRole}: EXPLAIN returned no plan row.");
        }

        var rawValue = reader.GetValue(0);
        var rawJson = rawValue switch
        {
            string text => text,
            JsonDocument document => document.RootElement.GetRawText(),
            JsonElement element => element.GetRawText(),
            _ => Convert.ToString(rawValue, CultureInfo.InvariantCulture)
                 ?? throw new BaselinePlanValidationException("EXPLAIN returned a null JSON plan.")
        };

        if (await reader.ReadAsync(cancellationToken))
        {
            throw new BaselinePlanValidationException(
                $"{replayableCommand.CapturedCommand.ShapeId}/" +
                $"{replayableCommand.CapturedCommand.CommandRole}: EXPLAIN returned multiple plan rows.");
        }

        using var planDocument = JsonDocument.Parse(rawJson);
        var root = planDocument.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1 ||
            !root[0].TryGetProperty("Plan", out var plan))
        {
            throw new BaselinePlanValidationException(
                $"{replayableCommand.CapturedCommand.ShapeId}/" +
                $"{replayableCommand.CapturedCommand.CommandRole}: invalid EXPLAIN JSON shape.");
        }

        var actualRows = ReadRequiredInt64(plan, "Actual Rows");
        var actualLoops = ReadRequiredInt64(plan, "Actual Loops");
        var structuralJson = CreateStructuralPlanJson(root);

        return new ExplainExecution(
            rawJson,
            ComputeSha256(structuralJson),
            actualRows,
            actualLoops);
    }

    private static NpgsqlParameter CreateParameter(ReplayableParameter source)
    {
        var parameter = new NpgsqlParameter
        {
            ParameterName = source.Name,
            Direction = source.Direction,
            IsNullable = source.IsNullable,
            Size = source.Size,
            Precision = source.Precision,
            Scale = source.Scale,
            Value = CloneValue(source.Value) ?? DBNull.Value
        };

        if (source.NpgsqlDbType.HasValue)
        {
            parameter.NpgsqlDbType = source.NpgsqlDbType.Value;
        }
        else if (!string.IsNullOrWhiteSpace(source.DataTypeName))
        {
            parameter.DataTypeName = source.DataTypeName;
        }
        else
        {
            parameter.DbType = source.DbType;
        }

        return parameter;
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null or DBNull => value,
            byte[] bytes => bytes.ToArray(),
            char[] characters => characters.ToArray(),
            Array array => array.Clone(),
            _ => value
        };
    }

    private static string CreateCommandKey(CapturedCommand command)
    {
        return $"{command.ShapeId}-{command.ShapeSequence:D2}-{command.CommandRole}";
    }

    private static string ComputeParameterSha256(IReadOnlyList<CapturedParameter> parameters)
    {
        return ComputeSha256(JsonSerializer.Serialize(
            parameters,
            JsonArtifactOutput.SerializerOptions));
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new BaselinePlanValidationException(
                $"Required raw baseline artifact '{path}' is missing.");
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(
                       stream,
                       JsonArtifactOutput.SerializerOptions,
                       cancellationToken)
                   ?? throw new BaselinePlanValidationException(
                       $"Required raw baseline artifact '{path}' deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new BaselinePlanValidationException(
                $"Required raw baseline artifact '{path}' is malformed: {exception.Message}");
        }
    }

    private static string ResolveArtifactPath(string runDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            throw new BaselinePlanValidationException(
                $"Artifact path '{relativePath}' must be a nonblank relative path.");
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(runDirectory, normalizedRelativePath));
        var relativeToRun = Path.GetRelativePath(runDirectory, fullPath);

        if (relativeToRun == ".." ||
            relativeToRun.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                $"Artifact path '{relativePath}' escapes the raw-run directory.");
        }

        return fullPath;
    }

    private static string CreateRelativePlanPath(string commandKey, int runNumber)
    {
        var fileName = runNumber == 0 ? "warmup.json" : $"run-{runNumber}.json";
        return $"raw-plans/{commandKey}/{fileName}";
    }

    private static IReadOnlyDictionary<string, string> ReadSettings(JsonElement root)
    {
        if (!root.TryGetProperty("Settings", out var settings))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (settings.ValueKind != JsonValueKind.Object)
        {
            throw new BaselinePlanValidationException("EXPLAIN Settings value is not an object.");
        }

        return settings.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()!
                    : property.Value.GetRawText(),
                StringComparer.Ordinal);
    }

    private static PlanBufferMetrics ReadBuffers(JsonElement element)
    {
        return new PlanBufferMetrics(
            ReadOptionalInt64(element, "Shared Hit Blocks") ?? 0,
            ReadOptionalInt64(element, "Shared Read Blocks") ?? 0,
            ReadOptionalInt64(element, "Shared Dirtied Blocks") ?? 0,
            ReadOptionalInt64(element, "Shared Written Blocks") ?? 0,
            ReadOptionalInt64(element, "Local Hit Blocks") ?? 0,
            ReadOptionalInt64(element, "Local Read Blocks") ?? 0,
            ReadOptionalInt64(element, "Local Dirtied Blocks") ?? 0,
            ReadOptionalInt64(element, "Local Written Blocks") ?? 0,
            ReadOptionalInt64(element, "Temp Read Blocks") ?? 0,
            ReadOptionalInt64(element, "Temp Written Blocks") ?? 0);
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        return ReadOptionalString(element, propertyName)
               ?? throw new BaselinePlanValidationException(
                   $"EXPLAIN plan is missing string '{propertyName}'.");
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new BaselinePlanValidationException(
                $"EXPLAIN plan property '{propertyName}' is not a string.");
        }

        return value.GetString();
    }

    private static decimal ReadRequiredDecimal(JsonElement element, string propertyName)
    {
        return ReadOptionalDecimal(element, propertyName)
               ?? throw new BaselinePlanValidationException(
                   $"EXPLAIN plan is missing numeric '{propertyName}'.");
    }

    private static decimal? ReadOptionalDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var result))
        {
            throw new BaselinePlanValidationException(
                $"EXPLAIN plan property '{propertyName}' is not a decimal number.");
        }

        return result;
    }

    private static long? ReadOptionalInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
        {
            throw new BaselinePlanValidationException(
                $"EXPLAIN plan property '{propertyName}' is not an integer.");
        }

        return result;
    }

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return Array.Empty<string>();
        }

        if (value.ValueKind != JsonValueKind.Array ||
            value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw new BaselinePlanValidationException(
                $"EXPLAIN plan property '{propertyName}' is not a string array.");
        }

        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static IReadOnlyList<string> DistinctOrdered(IEnumerable<string> values)
    {
        return values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static long ReadRequiredInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var result))
        {
            throw new BaselinePlanValidationException(
                $"EXPLAIN plan is missing numeric '{propertyName}'.");
        }

        return result;
    }

    private static string CreateStructuralPlanJson(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteStructuralElement(writer, root);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStructuralElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject()
                             .Where(property => !IsVolatilePlanProperty(property.Name))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteStructuralElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteStructuralElement(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool IsVolatilePlanProperty(string propertyName)
    {
        return propertyName.StartsWith("Actual ", StringComparison.Ordinal) ||
               propertyName.EndsWith(" Blocks", StringComparison.Ordinal) ||
               propertyName.StartsWith("Rows Removed by ", StringComparison.Ordinal) ||
               propertyName is
                   "Execution Time" or
                   "Planning Time" or
                   "I/O Read Time" or
                   "I/O Write Time" or
                   "Peak Memory Usage" or
                   "Sort Space Used" or
                   "Timing" or
                   "Workers" or
                   "Workers Launched";
    }

    private static void ScanOutputForCredentials(
        string runDirectory,
        NpgsqlConnectionStringBuilder connectionStringBuilder)
    {
        var forbiddenValues = new List<string>
        {
            connectionStringBuilder.ConnectionString,
            $"Host={connectionStringBuilder.Host}",
            $"Server={connectionStringBuilder.Host}",
            $"Username={connectionStringBuilder.Username}",
            $"User ID={connectionStringBuilder.Username}",
            "\"Host\":",
            "\"Username\":",
            "\"Password\":"
        };

        if (!string.IsNullOrEmpty(connectionStringBuilder.Password))
        {
            forbiddenValues.Add(connectionStringBuilder.Password);
            forbiddenValues.Add($"Password={connectionStringBuilder.Password}");
        }

        ScanOutputForCredentials(runDirectory, forbiddenValues);
    }

    private static void ScanOutputForCredentials(string runDirectory)
    {
        ScanOutputForCredentials(
            runDirectory,
            [
                "Host=",
                "Server=",
                "Username=",
                "User ID=",
                "Password=",
                "\"Host\":",
                "\"Username\":",
                "\"Password\":",
                "ConnectionString"
            ]);
    }

    private static void ScanOutputForCredentials(
        string runDirectory,
        IEnumerable<string> forbiddenValues)
    {
        var forbiddenPatterns = forbiddenValues
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var file in Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories))
        {
            var contents = File.ReadAllText(file);

            foreach (var forbidden in forbiddenPatterns)
            {
                if (contents.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BaselinePlanValidationException(
                        $"Credential scan rejected output file '{file}'.");
                }
            }
        }
    }

    private static SequenceDefinition FirstPage(string shapeId)
    {
        return new SequenceDefinition(
            $"{shapeId}-first-page",
            [
                $"{shapeId}-02-page-root",
                $"{shapeId}-03-translation-split",
                $"{shapeId}-04-image-split"
            ]);
    }

    private sealed record ExplainExecution(
        string RawJson,
        string StructuralPlanSha256,
        long ActualRows,
        long ActualLoops);

    private sealed record SequenceDefinition(
        string SequenceId,
        IReadOnlyList<string> CommandKeys);
}

internal sealed class BaselinePlanValidationException(string message)
    : InvalidOperationException(message);
