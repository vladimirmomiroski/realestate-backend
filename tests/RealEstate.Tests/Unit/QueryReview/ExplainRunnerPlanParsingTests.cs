using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class ExplainRunnerPlanParsingTests
{
    [Fact]
    public void ParsePlanMeasurement_PreservesPostgreSql18DecimalActualRows()
    {
        PlanSampleMeasurement measurement = ExplainRunner.ParsePlanMeasurement(
            CreateSample(),
            PostgreSql18Plan);

        measurement.ActualRows.Should().Be(1.00m);
        measurement.ActualLoops.Should().Be(1);
        measurement.Nodes.Should().HaveCount(2);
        measurement.Nodes[0].NodeType.Should().Be("Aggregate");
        measurement.Nodes[0].ActualRows.Should().Be(1.00m);
        measurement.Nodes[1].NodeType.Should().Be("Seq Scan");
        measurement.Nodes[1].Relation.Should().Be("Listings");
        measurement.Nodes[1].ActualRows.Should().Be(70000.00m);
    }

    [Fact]
    public void ParsePlanMeasurement_PreservesFractionalPerLoopActualRows()
    {
        string plan = PostgreSql18Plan
            .Replace(
                Property("Actual Rows", "70000.00"),
                Property("Actual Rows", "0.50"),
                StringComparison.Ordinal);

        PlanSampleMeasurement measurement = ExplainRunner.ParsePlanMeasurement(
            CreateSample(),
            plan);

        measurement.Nodes[1].ActualRows.Should().Be(0.50m);
        measurement.Nodes[1].ActualLoops.Should().Be(1);
    }

    [Fact]
    public void ParsePlanMeasurement_PreservesPostgreSql16IntegerSemantics()
    {
        string postgreSql16Plan = PostgreSql18Plan
            .Replace(
                Property("Actual Rows", "1.00"),
                Property("Actual Rows", "1"),
                StringComparison.Ordinal)
            .Replace(
                Property("Actual Rows", "70000.00"),
                Property("Actual Rows", "70000"),
                StringComparison.Ordinal);

        PlanSampleMeasurement postgreSql16 = ExplainRunner.ParsePlanMeasurement(
            CreateSample(),
            postgreSql16Plan);
        PlanSampleMeasurement postgreSql18 = ExplainRunner.ParsePlanMeasurement(
            CreateSample(),
            PostgreSql18Plan);

        postgreSql16.ActualRows.Should().Be(1m);
        postgreSql16.Nodes.Select(node => node.ActualRows)
            .Should().Equal(1m, 70000m);
        postgreSql16.StructuralPlanSha256.Should().Be(postgreSql18.StructuralPlanSha256);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("string")]
    public void ParsePlanMeasurement_RejectsMissingOrMalformedRequiredActualRows(string mutation)
    {
        string replacement = mutation switch
        {
            "missing" => string.Empty,
            "null" => Property("Actual Rows", "null") + ",",
            "string" => Property("Actual Rows", (char)34 + "1.00" + (char)34) + ",",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        string plan = PostgreSql18Plan.Replace(
            Property("Actual Rows", "1.00") + ",",
            replacement,
            StringComparison.Ordinal);

        Action act = () => ExplainRunner.ParsePlanMeasurement(CreateSample(), plan);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*Actual Rows*");
    }

    [Fact]
    public void ParsePlanMeasurement_RejectsMalformedActualRowsNumericToken()
    {
        string plan = PostgreSql18Plan.Replace(
            Property("Actual Rows", "1.00"),
            Property("Actual Rows", "1.0e+"),
            StringComparison.Ordinal);

        Action act = () => ExplainRunner.ParsePlanMeasurement(CreateSample(), plan);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*malformed plan JSON*");
    }

    [Fact]
    public void ParsePlanMeasurement_RejectsActualRowsOutsideDecimalRange()
    {
        string plan = PostgreSql18Plan.Replace(
            Property("Actual Rows", "1.00"),
            Property("Actual Rows", "1e100"),
            StringComparison.Ordinal);

        Action act = () => ExplainRunner.ParsePlanMeasurement(CreateSample(), plan);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*Actual Rows*not a decimal number*");
    }

    [Fact]
    public void ParsePlanMeasurement_RejectsNestedNodeWithoutActualRows()
    {
        string plan = PostgreSql18Plan.Replace(
            Property("Actual Rows", "70000.00") + ",",
            string.Empty,
            StringComparison.Ordinal);

        Action act = () => ExplainRunner.ParsePlanMeasurement(CreateSample(), plan);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*Actual Rows*");
    }

    [Fact]
    public void ParsePlanMeasurement_RejectsMissingActualLoops()
    {
        string plan = PostgreSql18Plan.Replace(
            Property("Actual Loops", "1") + ",",
            string.Empty,
            StringComparison.Ordinal);

        Action act = () => ExplainRunner.ParsePlanMeasurement(CreateSample(), plan);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*Actual Loops*");
    }

    [Fact]
    public void ParsePlanMeasurement_RejectsMissingExecutionTime()
    {
        string plan = PostgreSql18Plan.Replace(
            Property("Execution Time", "20.900"),
            string.Empty,
            StringComparison.Ordinal)
            .Replace(
                Property("Planning Time", "0.100") + ",",
                Property("Planning Time", "0.100"),
                StringComparison.Ordinal);

        Action act = () => ExplainRunner.ParsePlanMeasurement(CreateSample(), plan);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*Execution Time*");
    }

    private static RawPlanSample CreateSample() =>
        new(
            CommandKey: "N1-01-filtered-count",
            ShapeId: "N1",
            ShapeSequence: 1,
            CommandRole: "filtered-count",
            RunKind: "warmup",
            RunNumber: 0,
            RelativePlanPath: "raw-plans/N1-01-filtered-count/warmup.json",
            SqlSha256: new string('a', 64),
            ParameterSha256: new string('b', 64),
            StructuralPlanSha256: new string('c', 64),
            ActualRows: 1m,
            ActualLoops: 1);

    private static string Property(string name, string value) =>
        (char)34 + name + (char)34 + ": " + value;

    private const string PostgreSql18Plan =
        """
        [
          {
            "Plan": {
              "Node Type": "Aggregate",
              "Strategy": "Plain",
              "Startup Cost": 4812.76,
              "Total Cost": 4812.77,
              "Plan Rows": 1,
              "Plan Width": 4,
              "Actual Startup Time": 20.851,
              "Actual Total Time": 20.852,
              "Actual Rows": 1.00,
              "Actual Loops": 1,
              "Disabled": false,
              "Shared Hit Blocks": 3388,
              "Shared Read Blocks": 0,
              "Shared Dirtied Blocks": 0,
              "Shared Written Blocks": 0,
              "Temp Read Blocks": 0,
              "Temp Written Blocks": 0,
              "Plans": [
                {
                  "Node Type": "Seq Scan",
                  "Parent Relationship": "Outer",
                  "Relation Name": "Listings",
                  "Alias": "l",
                  "Startup Cost": 0.00,
                  "Total Cost": 4638.00,
                  "Plan Rows": 69903,
                  "Plan Width": 0,
                  "Actual Startup Time": 0.150,
                  "Actual Total Time": 16.680,
                  "Actual Rows": 70000.00,
                  "Actual Loops": 1,
                  "Disabled": false,
                  "Shared Hit Blocks": 3388,
                  "Shared Read Blocks": 0,
                  "Shared Dirtied Blocks": 0,
                  "Shared Written Blocks": 0,
                  "Temp Read Blocks": 0,
                  "Temp Written Blocks": 0
                }
              ]
            },
            "Settings": {},
            "Planning Time": 0.100,
            "Execution Time": 20.900
          }
        ]
        """;
}
