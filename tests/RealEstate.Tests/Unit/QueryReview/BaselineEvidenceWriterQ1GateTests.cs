using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class BaselineEvidenceWriterQ1GateTests
{
    private const string TrigramIndexName = "IX_ListingTranslations_Q_Trigram";

    [Fact]
    public void Q1PermanentExportGate_AllowsUnrelatedListingsSequentialScan()
    {
        BaselineMeasurementsRaw measurements = CreateMeasurements(
            countNodes:
            [
                CreateNode("Seq Scan", "Listings", alias: "listing", depth: 5),
                CreateNode("Seq Scan", "Agencies", alias: "agency", depth: 6),
                CreateNode("Bitmap Index Scan", indexName: TrigramIndexName, depth: 8)
            ],
            pageNodes:
            [
                CreateNode("Seq Scan", "Listings", alias: "listing", depth: 7),
                CreateNode("Bitmap Index Scan", indexName: TrigramIndexName, depth: 10)
            ]);

        Action act = () => BaselineEvidenceWriter.ValidateQ1Evidence(measurements);

        act.Should().NotThrow();
    }

    [Fact]
    public void Q1PermanentExportGate_RejectsNestedAliasedTranslationSequentialScan()
    {
        BaselineMeasurementsRaw measurements = CreateMeasurements(
            countNodes:
            [
                CreateNode("Seq Scan", "Listings", alias: "listing", depth: 5),
                CreateNode("Bitmap Index Scan", indexName: TrigramIndexName, depth: 8)
            ],
            pageNodes:
            [
                CreateNode("Bitmap Index Scan", indexName: TrigramIndexName, depth: 8),
                CreateNode(
                    "Seq Scan",
                    "ListingTranslations",
                    schema: "public",
                    alias: "effective_translation",
                    depth: 11)
            ]);

        Action act = () => BaselineEvidenceWriter.ValidateQ1Evidence(measurements);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*ListingTranslations sequential scan*IX_ListingTranslations_Q_Trigram*");
    }

    [Fact]
    public void Q1PermanentExportGate_StillRequiresTranslationTrigramIndex()
    {
        BaselineMeasurementsRaw measurements = CreateMeasurements(
            countNodes: [CreateNode("Seq Scan", "Listings", depth: 5)],
            pageNodes: [CreateNode("Seq Scan", "Listings", depth: 7)]);
        CommandMeasurementSummary countWithoutTrigram = measurements.Commands[0] with
        {
            IndexNames = []
        };
        measurements = measurements with
        {
            Commands = [countWithoutTrigram, measurements.Commands[1]]
        };

        Action act = () => BaselineEvidenceWriter.ValidateQ1Evidence(measurements);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*trigram-index use in count and page plans*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Q1PermanentExportGate_RejectsSequentialScanWithoutProvableRelation(
        string? relation)
    {
        BaselineMeasurementsRaw measurements = CreateMeasurements(
            countNodes:
            [
                CreateNode("Bitmap Index Scan", indexName: TrigramIndexName, depth: 8),
                CreateNode(
                    "Seq Scan",
                    relation,
                    schema: "public",
                    alias: "Listings",
                    depth: 11)
            ],
            pageNodes:
            [
                CreateNode("Seq Scan", "Listings", alias: "listing", depth: 7),
                CreateNode("Bitmap Index Scan", indexName: TrigramIndexName, depth: 10)
            ]);

        Action act = () => BaselineEvidenceWriter.ValidateQ1Evidence(measurements);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*missing or blank Relation Name*alias cannot prove*");
    }

    private static BaselineMeasurementsRaw CreateMeasurements(
        IReadOnlyList<PlanNodeMeasurement> countNodes,
        IReadOnlyList<PlanNodeMeasurement> pageNodes)
    {
        CommandMeasurementSummary count = CreateCommand(
            "Q1-01-filtered-count",
            "filtered-count",
            countNodes);
        CommandMeasurementSummary page = CreateCommand(
            "Q1-02-page-root",
            "page-root",
            pageNodes);
        var sequenceRun = new SequenceRunMeasurement(
            RunNumber: 1,
            PlanningTimeMilliseconds: 1m,
            ExecutionTimeMilliseconds: 2m,
            SharedAccessBlocks: 2,
            TempAccessBlocks: 0,
            Spilled: false);
        var firstPage = new SequenceMeasurementSummary(
            SequenceId: "Q1-first-page",
            CommandKeys: [count.CommandKey, page.CommandKey],
            Runs: [sequenceRun],
            PlanningTimeMedian: new MedianSelection(1m, 1),
            ExecutionTimeMedian: new MedianSelection(2m, 1),
            SharedAccessBlocksMedian: new MedianSelection(2m, 1),
            TempAccessBlocksMedian: new MedianSelection(0m, 1),
            AnySpill: false);

        return new BaselineMeasurementsRaw(
            BaselineRunId: "q1-plan-gate-test",
            VerifiedAtUtc: DateTime.UnixEpoch,
            CommandCount: 2,
            SampleCount: 2,
            WarmUpSampleCount: 0,
            MeasuredSampleCount: 2,
            Samples: [count.Samples[0], page.Samples[0]],
            Commands: [count, page],
            Sequences: [firstPage],
            Q1Gate: new Q1GateResult(true, 1m, 2m, false, []),
            Anomalies: []);
    }

    private static CommandMeasurementSummary CreateCommand(
        string commandKey,
        string commandRole,
        IReadOnlyList<PlanNodeMeasurement> nodes)
    {
        var sample = new PlanSampleMeasurement(
            CommandKey: commandKey,
            ShapeId: "Q1",
            ShapeSequence: commandRole == "filtered-count" ? 1 : 2,
            CommandRole: commandRole,
            RunKind: "measured",
            RunNumber: 1,
            RelativePlanPath: $"raw-plans/{commandKey}/run-1.json",
            RawPlanSha256: new string('a', 64),
            SqlSha256: new string('b', 64),
            ParameterSha256: new string('c', 64),
            StructuralPlanSha256: new string('d', 64),
            PlanningTimeMilliseconds: 1m,
            ExecutionTimeMilliseconds: 1m,
            ActualRows: 1,
            ActualLoops: 1,
            TopLevelBuffers: EmptyBuffers(),
            Settings: new Dictionary<string, string>(StringComparer.Ordinal),
            Spilled: false,
            SpillReasons: [],
            ScanTypes: nodes.Select(node => node.NodeType).Distinct(StringComparer.Ordinal).ToArray(),
            JoinTypes: [],
            SortMethods: [],
            IndexNames: [TrigramIndexName],
            TotalRowsRemoved: 0,
            MaximumPeakMemoryUsageKilobytes: 0,
            Nodes: nodes);

        return new CommandMeasurementSummary(
            CommandKey: commandKey,
            ShapeId: "Q1",
            ShapeSequence: sample.ShapeSequence,
            CommandRole: commandRole,
            Samples: [sample],
            PlanningTimeMedian: new MedianSelection(1m, 1),
            ExecutionTimeMedian: new MedianSelection(1m, 1),
            SharedAccessBlocksMedian: new MedianSelection(1m, 1),
            TempAccessBlocksMedian: new MedianSelection(0m, 1),
            ExecutionMedianPlanPath: sample.RelativePlanPath,
            ExecutionMedianPlanSha256: sample.RawPlanSha256,
            AnySpill: false,
            ScanTypes: sample.ScanTypes,
            JoinTypes: [],
            SortMethods: [],
            IndexNames: [TrigramIndexName]);
    }

    private static PlanNodeMeasurement CreateNode(
        string nodeType,
        string? relation = null,
        string? schema = null,
        string? alias = null,
        string? indexName = null,
        int depth = 0) =>
        new(
            Path: $"Plan/{depth}",
            Depth: depth,
            NodeType: nodeType,
            ParentRelationship: depth == 0 ? null : "Outer",
            Relation: relation,
            Schema: schema,
            Alias: alias,
            ScanDirection: null,
            IndexName: indexName,
            JoinType: null,
            StartupCost: 0m,
            TotalCost: 1m,
            PlanRows: 1,
            PlanWidth: 1,
            ActualStartupTimeMilliseconds: 0m,
            ActualTotalTimeMilliseconds: 1m,
            ActualRows: 1,
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
            Buffers: EmptyBuffers());

    private static PlanBufferMetrics EmptyBuffers() =>
        new(
            SharedHit: 0,
            SharedRead: 0,
            SharedDirtied: 0,
            SharedWritten: 0,
            LocalHit: 0,
            LocalRead: 0,
            LocalDirtied: 0,
            LocalWritten: 0,
            TempRead: 0,
            TempWritten: 0);
}
