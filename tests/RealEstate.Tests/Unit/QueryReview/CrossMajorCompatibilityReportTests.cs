using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class CrossMajorCompatibilityReportTests
{
    [Fact]
    public void VerifiedReportAndRealPermanentSummary_SealCompleteCompatibilityDecision()
    {
        BaselineMeasurementsRaw postgreSql16 = CreateMeasurements(postgreSql184: false);
        BaselineMeasurementsRaw postgreSql184 = CreateMeasurements(postgreSql184: true);

        CrossMajorComparisonReport report = CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            postgreSql16);
        string summary = BuildSummary(report, postgreSql184);

        report.Sequences.Should().HaveCount(23);
        report.ThresholdAExceedanceCount.Should().Be(0);
        report.ThresholdBExceedanceCount.Should().Be(0);
        report.Q1TrigramIndexPreserved.Should().BeTrue();
        report.Sequences[0].PostgreSql16ExecutionTimeMedianMilliseconds.Should().Be(10m);
        report.Sequences[0].PostgreSql184ExecutionTimeMedianMilliseconds.Should().Be(9m);
        report.Sequences.Where(HasTopologyDifference)
            .Select(sequence => sequence.SequenceId)
            .Should().Equal("L1-first-page", "Q1-first-page");

        summary.Should().StartWith("# PostgreSQL 18.4 Compatibility Evidence");
        summary.Should().NotStartWith("# Authoritative");
        summary.Should().Contain(
            "PostgreSQL 16 remains the authoritative Chapter 15 correctness lane");
        summary.Should().Contain(
            "historical-comparison lane, performance lane, and index-decision lane");
        summary.Should().Contain(
            "PostgreSQL 18.4 is a bounded compatibility/performance-observation lane");
        summary.Should().Contain("Cross-major timing is observational and is not an SLA");
        summary.Should().Contain("Medians use only the five measured samples");
        summary.Should().Contain("Threshold A exceedances: 0");
        summary.Should().Contain("Threshold B exceedances: 0");
        summary.Should().Contain(
            "L1-first-page: PostgreSQL 18.4 eliminates the PostgreSQL 16 named `Listings` sequential scan");
        summary.Should().Contain(
            "Q1-first-page: PostgreSQL 18.4 eliminates the PostgreSQL 16 named `Listings` sequential scan");
        summary.Should().Contain(
            "Q1-first-page: PostgreSQL 16-only " +
            "[scan:Index Scan;relation:Agencies;index:IX_Agencies_Test, " +
            "scan:Seq Scan;relation:Listings;index:-]; PostgreSQL 18.4-only " +
            "[join:Hash Join;type:Inner].");
        summary.Should().Contain(
            "Required Q1 `IX_ListingTranslations_Q_Trigram` behavior remains intact: PASS");

        foreach (string sequenceId in CrossMajorCompatibilityReportBuilder.ExpectedSequenceIds)
        {
            summary.Should().Contain($"| {sequenceId} |");
        }
    }

    [Fact]
    public void PostgreSql16PermanentSummary_RemainsAuthoritativeAndUnchangedInRole()
    {
        BaselineMeasurementsRaw measurements = CreateMeasurements(postgreSql184: false);

        string summary = BaselineEvidenceWriter.BuildSuccessorPermanentSummary(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId),
            measurements,
            CreateProfileEvidence(),
            CreateCaptureIdentity("16.14", "160014"),
            comparisonReport: null);

        summary.Should().StartWith(
            "# Authoritative permanent four-root discovery baseline summary");
        summary.Should().NotContain("Compatibility Evidence");
        summary.Should().NotContain("Cross-major sequence comparison");
    }

    [Fact]
    public void PostgreSql184PermanentSummary_RejectsMissingComparisonReport()
    {
        BaselineMeasurementsRaw measurements = CreateMeasurements(postgreSql184: true);

        Action act = () => BaselineEvidenceWriter.BuildSuccessorPermanentSummary(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId),
            measurements,
            CreateProfileEvidence(),
            CreateCaptureIdentity(
                "18.4 (Debian 18.4-1.pgdg13+1)",
                "180004"),
            comparisonReport: null);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*complete cross-major report*");
    }

    [Fact]
    public void ReportBuilder_RejectsMissingSequence()
    {
        BaselineMeasurementsRaw postgreSql184 = CreateMeasurements(postgreSql184: true);
        postgreSql184 = postgreSql184 with
        {
            Sequences = postgreSql184.Sequences.Skip(1).ToArray()
        };

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            CreateMeasurements(postgreSql184: false));

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*exact locked 23-sequence inventory*");
    }

    [Fact]
    public void ReportBuilder_RejectsMissingMeasuredSample()
    {
        BaselineMeasurementsRaw postgreSql184 = MutateSequence(
            CreateMeasurements(postgreSql184: true),
            "N1-first-page",
            sequence => sequence with { Runs = sequence.Runs.Take(4).ToArray() });

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            CreateMeasurements(postgreSql184: false));

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*incomplete or invalid*measured-sample evidence*");
    }

    [Fact]
    public void ReportBuilder_RejectsMissingSharedBlockEvidence()
    {
        BaselineMeasurementsRaw postgreSql184 = MutateSequence(
            CreateMeasurements(postgreSql184: true),
            "N1-first-page",
            sequence =>
            {
                SequenceRunMeasurement[] runs = sequence.Runs.ToArray();
                runs[0] = runs[0] with { SharedAccessBlocks = -1 };
                return sequence with { Runs = runs };
            });

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            CreateMeasurements(postgreSql184: false));

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*incomplete or invalid*measured-sample evidence*");
    }

    [Fact]
    public void ReportBuilder_RejectsIncompleteMedianPlanTopology()
    {
        BaselineMeasurementsRaw postgreSql184 = CreateMeasurements(postgreSql184: true);
        postgreSql184 = postgreSql184 with
        {
            Commands = postgreSql184.Commands
                .Where(command => !string.Equals(
                    command.CommandKey,
                    "N1-first-page-command",
                    StringComparison.Ordinal))
                .ToArray()
        };

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            CreateMeasurements(postgreSql184: false));

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*command*N1-first-page-command*missing*");
    }

    [Fact]
    public void ReportBuilder_RejectsMissingQ1TrigramEvidence()
    {
        BaselineMeasurementsRaw postgreSql184 = CreateMeasurements(postgreSql184: true);
        CommandMeasurementSummary[] commands = postgreSql184.Commands.ToArray();
        int index = Array.FindIndex(commands, command =>
            string.Equals(
                command.CommandKey,
                "Q1-01-filtered-count",
                StringComparison.Ordinal));
        commands[index] = RemoveIndex(commands[index], "IX_ListingTranslations_Q_Trigram");
        postgreSql184 = postgreSql184 with { Commands = commands };

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            CreateMeasurements(postgreSql184: false));

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*trigram-index use in count and page plans*");
    }

    [Fact]
    public void ReportBuilder_AllowsComparisonQ1UnrelatedListingsSequentialScan()
    {
        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            CreateMeasurements(postgreSql184: true),
            CreateMeasurements(postgreSql184: false));

        act.Should().NotThrow();
    }

    [Fact]
    public void ReportBuilder_RejectsComparisonQ1TranslationSequentialScanDespiteTrigram()
    {
        BaselineMeasurementsRaw postgreSql16 = AddNodeToCommand(
            CreateMeasurements(postgreSql184: false),
            "Q1-02-page-root",
            CreateNode(
                "Seq Scan",
                relation: "ListingTranslations",
                alias: "effective_translation",
                depth: 11));

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            CreateMeasurements(postgreSql184: true),
            postgreSql16);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*ListingTranslations sequential scan*" +
                "IX_ListingTranslations_Q_Trigram*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportBuilder_RejectsComparisonQ1SequentialScanWithoutRelation(
        string? relation)
    {
        BaselineMeasurementsRaw postgreSql16 = AddNodeToCommand(
            CreateMeasurements(postgreSql184: false),
            "Q1-02-page-root",
            CreateNode(
                "Seq Scan",
                relation,
                alias: "ListingTranslations",
                depth: 11));

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            CreateMeasurements(postgreSql184: true),
            postgreSql16);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*missing or blank Relation Name*alias cannot prove*");
    }

    [Fact]
    public void ReportBuilder_RejectsComparisonQ1MissingTrigramEvidence()
    {
        BaselineMeasurementsRaw postgreSql16 = MutateCommand(
            CreateMeasurements(postgreSql184: false),
            "Q1-01-filtered-count",
            command => RemoveIndex(
                command,
                "IX_ListingTranslations_Q_Trigram"));

        Action act = () => CrossMajorCompatibilityReportBuilder.Build(
            CreateMeasurements(postgreSql184: true),
            postgreSql16);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*trigram-index use in count and page plans*");
    }

    [Fact]
    public void ThresholdArithmetic_IsComputedFromInputsAndRequiresBothTimeConditions()
    {
        BaselineMeasurementsRaw postgreSql16 = CreateMeasurements(postgreSql184: false);
        BaselineMeasurementsRaw postgreSql184 = CreateMeasurements(postgreSql184: true);
        postgreSql184 = SetSequenceMedians(
            postgreSql184,
            "N1-first-page",
            executionTime: 13m,
            sharedBlocks: 121);
        postgreSql184 = SetSequenceMedians(
            postgreSql184,
            "P1-first-page",
            executionTime: 6.5m,
            sharedBlocks: 120);
        postgreSql184 = SetSequenceMedians(
            postgreSql184,
            "P2-first-page",
            executionTime: 23m,
            sharedBlocks: 100);
        postgreSql16 = SetSequenceMedians(
            postgreSql16,
            "P1-first-page",
            executionTime: 5m,
            sharedBlocks: 100);
        postgreSql16 = SetSequenceMedians(
            postgreSql16,
            "P2-first-page",
            executionTime: 20m,
            sharedBlocks: 100);

        CrossMajorComparisonReport report = CrossMajorCompatibilityReportBuilder.Build(
            postgreSql184,
            postgreSql16);

        report.Sequences.Single(sequence => sequence.SequenceId == "N1-first-page")
            .ThresholdAExceeded.Should().BeTrue();
        report.Sequences.Single(sequence => sequence.SequenceId == "P1-first-page")
            .ThresholdAExceeded.Should().BeFalse(
                "a >25% change without a >2 ms change must not cross threshold A");
        report.Sequences.Single(sequence => sequence.SequenceId == "P2-first-page")
            .ThresholdAExceeded.Should().BeFalse(
                "a >2 ms change without a >25% change must not cross threshold A");
        report.Sequences.Single(sequence => sequence.SequenceId == "N1-first-page")
            .ThresholdBExceeded.Should().BeTrue();
        report.Sequences.Single(sequence => sequence.SequenceId == "P1-first-page")
            .ThresholdBExceeded.Should().BeFalse(
                "exactly 20% additional blocks must not cross threshold B");
        report.ThresholdAExceedanceCount.Should().Be(1);
        report.ThresholdBExceedanceCount.Should().Be(1);

        Action act = () => CrossMajorCompatibilityReportBuilder.ValidateForPublication(report);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*requires owner review*");
    }

    private static bool HasTopologyDifference(CrossMajorSequenceComparison sequence) =>
        sequence.PostgreSql16OnlyTopology.Count != 0 ||
        sequence.PostgreSql184OnlyTopology.Count != 0;

    private static string BuildSummary(
        CrossMajorComparisonReport report,
        BaselineMeasurementsRaw measurements) =>
        BaselineEvidenceWriter.BuildSuccessorPermanentSummary(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId),
            measurements,
            CreateProfileEvidence(),
            CreateCaptureIdentity(
                "18.4 (Debian 18.4-1.pgdg13+1)",
                "180004"),
            report);

    private static BaselineMeasurementsRaw CreateMeasurements(bool postgreSql184)
    {
        var commands = new List<CommandMeasurementSummary>();
        var sequences = new List<SequenceMeasurementSummary>();

        foreach (string sequenceId in CrossMajorCompatibilityReportBuilder.ExpectedSequenceIds)
        {
            string[] commandKeys = string.Equals(
                    sequenceId,
                    "Q1-first-page",
                    StringComparison.Ordinal)
                ? ["Q1-01-filtered-count", "Q1-02-page-root"]
                : [$"{sequenceId}-command"];
            decimal executionMedian = postgreSql184 ? 9m : 10m;
            long sharedBlocksMedian = postgreSql184 ? 90 : 100;
            SequenceRunMeasurement[] runs = CreateRuns(
                executionMedian,
                sharedBlocksMedian);

            sequences.Add(new SequenceMeasurementSummary(
                sequenceId,
                commandKeys,
                runs,
                new MedianSelection(1m, 3),
                new MedianSelection(executionMedian, 3),
                new MedianSelection(sharedBlocksMedian, 3),
                new MedianSelection(0m, 3),
                AnySpill: false));

            foreach (string commandKey in commandKeys)
            {
                commands.Add(CreateCommand(
                    commandKey,
                    sequenceId,
                    postgreSql184,
                    executionMedian,
                    sharedBlocksMedian));
            }
        }

        PlanSampleMeasurement[] allSamples = commands
            .SelectMany(command => command.Samples)
            .ToArray();

        return new BaselineMeasurementsRaw(
            BaselineRunId: postgreSql184 ? "pg184-test-run" : "pg16-test-run",
            VerifiedAtUtc: DateTime.UnixEpoch,
            CommandCount: commands.Count,
            SampleCount: allSamples.Length,
            WarmUpSampleCount: commands.Count,
            MeasuredSampleCount: commands.Count * 5,
            allSamples,
            commands,
            sequences,
            new Q1GateResult(
                Passed: true,
                FilteredCountMedianMilliseconds: 1m,
                FirstPageSequenceMedianMilliseconds: 1m,
                AnyWarmUpOrMeasuredSpill: false,
                Reasons: []),
            Anomalies: []);
    }

    private static CommandMeasurementSummary CreateCommand(
        string commandKey,
        string sequenceId,
        bool postgreSql184,
        decimal executionMedian,
        long sharedBlocksMedian)
    {
        var nodes = new List<PlanNodeMeasurement>
        {
            CreateNode(
                "Index Scan",
                relation: "Listings",
                indexName: "PK_Listings")
        };

        if (!postgreSql184 &&
            (string.Equals(sequenceId, "L1-first-page", StringComparison.Ordinal) ||
             string.Equals(sequenceId, "Q1-first-page", StringComparison.Ordinal)))
        {
            nodes.Add(CreateNode("Seq Scan", relation: "Listings"));
        }

        if (string.Equals(sequenceId, "Q1-first-page", StringComparison.Ordinal))
        {
            nodes.Add(CreateNode(
                "Bitmap Index Scan",
                relation: "ListingTranslations",
                indexName: "IX_ListingTranslations_Q_Trigram"));

            if (postgreSql184)
            {
                nodes.Add(CreateNode(
                    "Hash Join",
                    relation: null,
                    joinType: "Inner"));
            }
            else
            {
                nodes.Add(CreateNode(
                    "Index Scan",
                    relation: "Agencies",
                    indexName: "IX_Agencies_Test"));
            }
        }

        var samples = new List<PlanSampleMeasurement>
        {
            CreateSample(commandKey, "warmup", 0, 999m, 999, nodes)
        };
        SequenceRunMeasurement[] measuredRuns = CreateRuns(
            executionMedian,
            sharedBlocksMedian);
        samples.AddRange(measuredRuns.Select(run =>
            CreateSample(
                commandKey,
                "measured",
                run.RunNumber,
                run.ExecutionTimeMilliseconds,
                run.SharedAccessBlocks,
                nodes)));

        return new CommandMeasurementSummary(
            commandKey,
            sequenceId,
            ShapeSequence: 1,
            CommandRole: "page-root",
            samples,
            new MedianSelection(1m, 3),
            new MedianSelection(executionMedian, 3),
            new MedianSelection(sharedBlocksMedian, 3),
            new MedianSelection(0m, 3),
            ExecutionMedianPlanPath: $"raw-plans/{commandKey}/run-3.json",
            ExecutionMedianPlanSha256: new string('a', 64),
            AnySpill: false,
            ScanTypes: nodes
                .Where(node => node.NodeType.EndsWith("Scan", StringComparison.Ordinal))
                .Select(node => node.NodeType)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            JoinTypes: [],
            SortMethods: [],
            IndexNames: nodes
                .Where(node => node.IndexName is not null)
                .Select(node => node.IndexName!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static SequenceRunMeasurement[] CreateRuns(
        decimal median,
        long sharedBlocks) =>
    [
        new(1, 1m, median - 2m, sharedBlocks, 0, false),
        new(2, 1m, median - 1m, sharedBlocks, 0, false),
        new(3, 1m, median, sharedBlocks, 0, false),
        new(4, 1m, median + 1m, sharedBlocks, 0, false),
        new(5, 1m, median + 2m, sharedBlocks, 0, false)
    ];

    private static PlanSampleMeasurement CreateSample(
        string commandKey,
        string runKind,
        int runNumber,
        decimal executionTime,
        long sharedBlocks,
        IReadOnlyList<PlanNodeMeasurement> nodes) =>
        new(
            commandKey,
            ShapeId: commandKey,
            ShapeSequence: 1,
            CommandRole: "page-root",
            runKind,
            runNumber,
            RelativePlanPath: $"raw-plans/{commandKey}/{runKind}-{runNumber}.json",
            RawPlanSha256: new string('1', 64),
            SqlSha256: new string('2', 64),
            ParameterSha256: new string('3', 64),
            StructuralPlanSha256: new string('4', 64),
            PlanningTimeMilliseconds: 1m,
            ExecutionTimeMilliseconds: executionTime,
            ActualRows: 1m,
            ActualLoops: 1,
            TopLevelBuffers: new PlanBufferMetrics(
                SharedHit: sharedBlocks,
                SharedRead: 0,
                SharedDirtied: 0,
                SharedWritten: 0,
                LocalHit: 0,
                LocalRead: 0,
                LocalDirtied: 0,
                LocalWritten: 0,
                TempRead: 0,
                TempWritten: 0),
            Settings: new Dictionary<string, string>(StringComparer.Ordinal),
            Spilled: false,
            SpillReasons: [],
            ScanTypes: nodes
                .Where(node => node.NodeType.EndsWith("Scan", StringComparison.Ordinal))
                .Select(node => node.NodeType)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            JoinTypes: [],
            SortMethods: [],
            IndexNames: nodes
                .Where(node => node.IndexName is not null)
                .Select(node => node.IndexName!)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            TotalRowsRemoved: 0,
            MaximumPeakMemoryUsageKilobytes: 0,
            nodes);

    private static PlanNodeMeasurement CreateNode(
        string nodeType,
        string? relation,
        string? indexName = null,
        string? alias = null,
        string? joinType = null,
        int depth = 0) =>
        new(
            Path: $"Plan/{depth}",
            Depth: depth,
            nodeType,
            ParentRelationship: depth == 0 ? null : "Outer",
            relation,
            Schema: "public",
            Alias: alias ?? relation,
            ScanDirection: null,
            indexName,
            JoinType: joinType,
            StartupCost: 0m,
            TotalCost: 1m,
            PlanRows: 1,
            PlanWidth: 1,
            ActualStartupTimeMilliseconds: 0m,
            ActualTotalTimeMilliseconds: 1m,
            ActualRows: 1m,
            ActualLoops: 1,
            RowsRemovedByFilter: 0,
            RowsRemovedByIndexRecheck: 0,
            RowsRemovedByJoinFilter: 0,
            Filter: null,
            IndexCondition: null,
            RecheckCondition: null,
            JoinFilter: null,
            HashCondition: null,
            MergeCondition: null,
            SortKeys: [],
            SortMethod: null,
            SortSpaceUsedKilobytes: null,
            SortSpaceType: null,
            HashBatches: null,
            PeakMemoryUsageKilobytes: null,
            Buffers: new PlanBufferMetrics(
                SharedHit: 1,
                SharedRead: 0,
                SharedDirtied: 0,
                SharedWritten: 0,
                LocalHit: 0,
                LocalRead: 0,
                LocalDirtied: 0,
                LocalWritten: 0,
                TempRead: 0,
                TempWritten: 0));

    private static BaselineMeasurementsRaw MutateSequence(
        BaselineMeasurementsRaw measurements,
        string sequenceId,
        Func<SequenceMeasurementSummary, SequenceMeasurementSummary> mutate)
    {
        SequenceMeasurementSummary[] sequences = measurements.Sequences.ToArray();
        int index = Array.FindIndex(sequences, sequence =>
            string.Equals(sequence.SequenceId, sequenceId, StringComparison.Ordinal));
        sequences[index] = mutate(sequences[index]);
        return measurements with { Sequences = sequences };
    }

    private static BaselineMeasurementsRaw SetSequenceMedians(
        BaselineMeasurementsRaw measurements,
        string sequenceId,
        decimal executionTime,
        long sharedBlocks) =>
        MutateSequence(
            measurements,
            sequenceId,
            sequence => sequence with
            {
                Runs = CreateRuns(executionTime, sharedBlocks),
                ExecutionTimeMedian = new MedianSelection(executionTime, 3),
                SharedAccessBlocksMedian = new MedianSelection(sharedBlocks, 3)
            });

    private static BaselineMeasurementsRaw AddNodeToCommand(
        BaselineMeasurementsRaw measurements,
        string commandKey,
        PlanNodeMeasurement node) =>
        MutateCommand(
            measurements,
            commandKey,
            command => command with
            {
                Samples = command.Samples
                    .Select(sample => sample with
                    {
                        Nodes = [.. sample.Nodes, node]
                    })
                    .ToArray()
            });

    private static BaselineMeasurementsRaw MutateCommand(
        BaselineMeasurementsRaw measurements,
        string commandKey,
        Func<CommandMeasurementSummary, CommandMeasurementSummary> mutate)
    {
        CommandMeasurementSummary[] commands = measurements.Commands.ToArray();
        int index = Array.FindIndex(commands, command =>
            string.Equals(command.CommandKey, commandKey, StringComparison.Ordinal));
        commands[index] = mutate(commands[index]);
        return measurements with { Commands = commands };
    }

    private static CommandMeasurementSummary RemoveIndex(
        CommandMeasurementSummary command,
        string indexName)
    {
        PlanSampleMeasurement[] samples = command.Samples
            .Select(sample => sample with
            {
                Nodes = sample.Nodes
                    .Where(node => !string.Equals(
                        node.IndexName,
                        indexName,
                        StringComparison.Ordinal))
                    .ToArray(),
                IndexNames = sample.IndexNames
                    .Where(value => !string.Equals(
                        value,
                        indexName,
                        StringComparison.Ordinal))
                    .ToArray()
            })
            .ToArray();

        return command with
        {
            Samples = samples,
            IndexNames = command.IndexNames
                .Where(value => !string.Equals(value, indexName, StringComparison.Ordinal))
                .ToArray()
        };
    }

    private static ProfileVerificationEvidence CreateProfileEvidence() =>
        new(
            QueryReviewGenerations.FourRootDiscoveryId,
            ListingCount: 100_000,
            TranslationCount: 200_000,
            InvariantTotal: 179,
            InvariantPassed: 179,
            InvariantFailed: 0,
            Passed: true,
            SuccessorPermanentExportManifest.AcceptedProfileSha256,
            SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256,
            SuccessorPermanentExportManifest.AcceptedInvariantResultSha256);

    private static CaptureIdentityEvidence CreateCaptureIdentity(
        string version,
        string versionNumber) =>
        new(
            GitCommit: new string('a', 40),
            GitBranch: "test",
            PostgreSqlVersion: version,
            TrigramIndex: new TrigramIndexEvidence(
                "pg_trgm",
                "1.6",
                "IX_ListingTranslations_Q_Trigram",
                "gin",
                ["Title", "City", "Municipality", "Neighborhood"],
                ["gin_trgm_ops", "gin_trgm_ops", "gin_trgm_ops", "gin_trgm_ops"],
                IsValid: true,
                IsReady: true,
                IsLive: true,
                SizeBytes: 1),
            CommandCount: 83,
            TypedParameterCount: 190,
            RawPlanCount: 498,
            WarmUpRounds: 1,
            MeasuredRounds: 5,
            SpillCount: 0,
            PlanSwitchCount: 0,
            AnomalyCount: 0,
            CredentialFindingCount: 0,
            PostgreSqlVersionNumber: versionNumber);
}
