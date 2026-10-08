namespace RealEstate.QueryReview;

internal static class CrossMajorCompatibilityReportBuilder
{
    internal const decimal ExecutionTimeReviewPercent = 25m;
    internal const decimal ExecutionTimeReviewMilliseconds = 2m;
    internal const decimal SharedAccessBlocksReviewPercent = 20m;
    internal const string TrigramIndexName = "IX_ListingTranslations_Q_Trigram";

    internal static readonly IReadOnlyList<string> ExpectedSequenceIds =
    [
        "N1-first-page",
        "P1-first-page",
        "P2-first-page",
        "A1-first-page",
        "A1-endpoint-supplementary",
        "R1-first-page",
        "L1-first-page",
        "Q1-first-page",
        "C1-candidate-page",
        "C1-endpoint-supplementary",
        "commercial-unknown-first-page-page",
        "commercial-office-first-page-page",
        "commercial-office-root-first-page-page",
        "commercial-shop-location-page",
        "commercial-other-deep-page-page",
        "land-unknown-first-page-page",
        "land-building-plot-first-page-page",
        "land-building-plot-root-first-page-page",
        "land-agricultural-location-page",
        "land-other-deep-page-page",
        "agency-commercial-shop-first-page-page",
        "agency-land-agricultural-deep-page-page",
        "cross-family-subtypes-empty-page"
    ];

    internal static CrossMajorComparisonReport Build(
        BaselineMeasurementsRaw postgreSql184,
        BaselineMeasurementsRaw postgreSql16)
    {
        ArgumentNullException.ThrowIfNull(postgreSql184);
        ArgumentNullException.ThrowIfNull(postgreSql16);

        ValidateSequenceInventory(postgreSql184, QueryReviewGenerations.PostgreSql184LaneId);
        ValidateSequenceInventory(postgreSql16, QueryReviewGenerations.PostgreSql16LaneId);
        BaselineEvidenceWriter.ValidateQ1Evidence(postgreSql184);
        BaselineEvidenceWriter.ValidateQ1Evidence(postgreSql16);

        var sequences = new List<CrossMajorSequenceComparison>(ExpectedSequenceIds.Count);

        foreach (string sequenceId in ExpectedSequenceIds)
        {
            SequenceMeasurementSummary primary = GetSequence(postgreSql184, sequenceId);
            SequenceMeasurementSummary comparison = GetSequence(postgreSql16, sequenceId);
            (decimal primaryTime, long primaryBlocks) =
                ValidateAndGetMeasuredMedians(primary, QueryReviewGenerations.PostgreSql184LaneId);
            (decimal comparisonTime, long comparisonBlocks) =
                ValidateAndGetMeasuredMedians(comparison, QueryReviewGenerations.PostgreSql16LaneId);

            decimal timeDelta = primaryTime - comparisonTime;
            decimal timeDeltaPercent = Percentage(timeDelta, comparisonTime, sequenceId, "execution time");
            long blockDelta = primaryBlocks - comparisonBlocks;
            decimal blockDeltaPercent = comparisonBlocks == 0
                ? primaryBlocks == 0
                    ? 0m
                    : throw new BaselinePlanValidationException(
                        $"Cross-major sequence '{sequenceId}' cannot compute a shared-block " +
                        "percentage from a zero PostgreSQL 16 median and nonzero PostgreSQL 18.4 median.")
                : 100m * blockDelta / comparisonBlocks;

            IReadOnlyList<string> primaryTopology =
                BuildMedianTopology(postgreSql184, primary, QueryReviewGenerations.PostgreSql184LaneId);
            IReadOnlyList<string> comparisonTopology =
                BuildMedianTopology(postgreSql16, comparison, QueryReviewGenerations.PostgreSql16LaneId);
            string[] comparisonOnly = comparisonTopology
                .Except(primaryTopology, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] primaryOnly = primaryTopology
                .Except(comparisonTopology, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            sequences.Add(new CrossMajorSequenceComparison(
                sequenceId,
                comparisonTime,
                primaryTime,
                timeDelta,
                timeDeltaPercent,
                comparisonBlocks,
                primaryBlocks,
                blockDelta,
                blockDeltaPercent,
                ThresholdAExceeded:
                    timeDeltaPercent > ExecutionTimeReviewPercent &&
                    timeDelta > ExecutionTimeReviewMilliseconds,
                ThresholdBExceeded:
                    blockDeltaPercent > SharedAccessBlocksReviewPercent,
                comparisonOnly,
                primaryOnly));
        }

        return new CrossMajorComparisonReport(
            sequences,
            sequences.Count(sequence => sequence.ThresholdAExceeded),
            sequences.Count(sequence => sequence.ThresholdBExceeded),
            Q1TrigramIndexPreserved: true);
    }

    internal static void ValidateForPublication(CrossMajorComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Sequences.Count != ExpectedSequenceIds.Count ||
            !report.Sequences.Select(sequence => sequence.SequenceId)
                .SequenceEqual(ExpectedSequenceIds, StringComparer.Ordinal) ||
            report.ThresholdAExceedanceCount !=
                report.Sequences.Count(sequence => sequence.ThresholdAExceeded) ||
            report.ThresholdBExceedanceCount !=
                report.Sequences.Count(sequence => sequence.ThresholdBExceeded) ||
            !report.Q1TrigramIndexPreserved)
        {
            throw new BaselinePlanValidationException(
                "The PostgreSQL 18.4 cross-major compatibility report is incomplete or inconsistent.");
        }

        if (report.ThresholdAExceedanceCount != 0 ||
            report.ThresholdBExceedanceCount != 0)
        {
            throw new BaselinePlanValidationException(
                "PostgreSQL 18.4 permanent publication requires owner review because a locked " +
                "cross-major execution-time or shared-block threshold was exceeded.");
        }
    }

    private static void ValidateSequenceInventory(
        BaselineMeasurementsRaw measurements,
        string laneId)
    {
        string[] actual = measurements.Sequences
            .Select(sequence => sequence.SequenceId)
            .ToArray();

        if (actual.Length != ExpectedSequenceIds.Count ||
            !actual.SequenceEqual(ExpectedSequenceIds, StringComparer.Ordinal) ||
            actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
        {
            throw new BaselinePlanValidationException(
                $"The {laneId} comparison evidence does not contain the exact locked " +
                $"{ExpectedSequenceIds.Count}-sequence inventory.");
        }
    }

    private static SequenceMeasurementSummary GetSequence(
        BaselineMeasurementsRaw measurements,
        string sequenceId)
    {
        SequenceMeasurementSummary[] matches = measurements.Sequences
            .Where(sequence => string.Equals(
                sequence.SequenceId,
                sequenceId,
                StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new BaselinePlanValidationException(
                $"Cross-major sequence '{sequenceId}' is missing or duplicated.");
        }

        return matches[0];
    }

    private static (decimal ExecutionTime, long SharedAccessBlocks)
        ValidateAndGetMeasuredMedians(
            SequenceMeasurementSummary sequence,
            string laneId)
    {
        SequenceRunMeasurement[] runs = sequence.Runs
            .OrderBy(run => run.RunNumber)
            .ToArray();

        if (runs.Length != 5 ||
            !runs.Select(run => run.RunNumber).SequenceEqual([1, 2, 3, 4, 5]) ||
            runs.Any(run =>
                run.ExecutionTimeMilliseconds <= 0 ||
                run.SharedAccessBlocks < 0 ||
                run.TempAccessBlocks < 0 ||
                run.Spilled))
        {
            throw new BaselinePlanValidationException(
                $"Cross-major sequence '{sequence.SequenceId}' has incomplete or invalid " +
                $"{laneId} measured-sample evidence.");
        }

        decimal executionMedian = runs
            .Select(run => run.ExecutionTimeMilliseconds)
            .OrderBy(value => value)
            .ElementAt(2);
        long sharedBlocksMedian = runs
            .Select(run => run.SharedAccessBlocks)
            .OrderBy(value => value)
            .ElementAt(2);

        if (sequence.ExecutionTimeMedian.Value != executionMedian ||
            sequence.SharedAccessBlocksMedian.Value != sharedBlocksMedian)
        {
            throw new BaselinePlanValidationException(
                $"Cross-major sequence '{sequence.SequenceId}' has a stored median that does " +
                "not match its five measured samples.");
        }

        return (executionMedian, sharedBlocksMedian);
    }

    private static IReadOnlyList<string> BuildMedianTopology(
        BaselineMeasurementsRaw measurements,
        SequenceMeasurementSummary sequence,
        string laneId)
    {
        var topology = new HashSet<string>(StringComparer.Ordinal);

        if (sequence.CommandKeys.Count == 0)
        {
            throw new BaselinePlanValidationException(
                $"Cross-major sequence '{sequence.SequenceId}' has no command topology.");
        }

        foreach (string commandKey in sequence.CommandKeys)
        {
            CommandMeasurementSummary command = GetCommand(measurements, commandKey, laneId);
            PlanSampleMeasurement sample = GetExecutionMedianSample(command, laneId);

            if (sample.Nodes.Count == 0)
            {
                throw new BaselinePlanValidationException(
                    $"Cross-major command '{commandKey}' has no median-plan topology nodes.");
            }

            foreach (PlanNodeMeasurement node in sample.Nodes)
            {
                if (node.NodeType.EndsWith("Scan", StringComparison.Ordinal))
                {
                    if (string.Equals(node.NodeType, "Seq Scan", StringComparison.Ordinal) &&
                        string.IsNullOrWhiteSpace(node.Relation))
                    {
                        throw new BaselinePlanValidationException(
                            $"Cross-major command '{commandKey}' has an ambiguous sequential scan.");
                    }

                    topology.Add(
                        $"scan:{node.NodeType};relation:{node.Relation ?? "-"};" +
                        $"index:{node.IndexName ?? "-"}");
                }

                if (!string.IsNullOrWhiteSpace(node.JoinType))
                {
                    topology.Add($"join:{node.NodeType};type:{node.JoinType}");
                }

                if (!string.IsNullOrWhiteSpace(node.SortMethod))
                {
                    topology.Add($"sort:{node.SortMethod}");
                }
            }
        }

        return topology.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static CommandMeasurementSummary GetCommand(
        BaselineMeasurementsRaw measurements,
        string commandKey,
        string laneId)
    {
        CommandMeasurementSummary[] matches = measurements.Commands
            .Where(command => string.Equals(
                command.CommandKey,
                commandKey,
                StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new BaselinePlanValidationException(
                $"Cross-major {laneId} command '{commandKey}' is missing or duplicated.");
        }

        return matches[0];
    }

    private static PlanSampleMeasurement GetExecutionMedianSample(
        CommandMeasurementSummary command,
        string laneId)
    {
        PlanSampleMeasurement[] matches = command.Samples
            .Where(sample =>
                string.Equals(sample.RunKind, "measured", StringComparison.Ordinal) &&
                sample.RunNumber == command.ExecutionTimeMedian.RunNumber)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new BaselinePlanValidationException(
                $"Cross-major {laneId} command '{command.CommandKey}' is missing its exact " +
                "measured execution-median plan.");
        }

        return matches[0];
    }

    private static decimal Percentage(
        decimal delta,
        decimal baseline,
        string sequenceId,
        string metric)
    {
        if (baseline <= 0)
        {
            throw new BaselinePlanValidationException(
                $"Cross-major sequence '{sequenceId}' cannot compute {metric} percentage " +
                "from a nonpositive PostgreSQL 16 median.");
        }

        return 100m * delta / baseline;
    }
}
