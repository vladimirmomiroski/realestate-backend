using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class BaselineEvidenceWriterPostgreSqlVersionTests
{
    [Theory]
    [InlineData("18.4", "180004")]
    [InlineData("18.4 (Debian 18.4-1.pgdg13+1)", "180004")]
    public void PermanentExportVersionGate_AcceptsExactPostgreSql184Identity(
        string serverVersion,
        string serverVersionNumber)
    {
        PostgreSqlEnvironmentSnapshot environment = CreateEnvironment(
            serverVersion,
            serverVersionNumber);

        Action act = () => BaselineEvidenceWriter.ValidatePostgreSqlVersionForPermanentEvidence(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId),
            environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void PermanentExportVersionGate_PreservesAcceptedPostgreSql16Identity()
    {
        PostgreSqlEnvironmentSnapshot environment = CreateEnvironment("16.14", "160014");

        Action act = () => BaselineEvidenceWriter.ValidatePostgreSqlVersionForPermanentEvidence(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId),
            environment);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("18.5", "180005")]
    [InlineData("18.3", "180003")]
    [InlineData("19.0", "190000")]
    [InlineData("18.4 (Debian 18.4-1.pgdg13+1)", "180005")]
    [InlineData("18.5 (Debian 18.5-1.pgdg13+1)", "180004")]
    [InlineData("18.4 Debian 18.4-1.pgdg13+1", "180004")]
    [InlineData("18.4", "not-numeric")]
    [InlineData("18.4", null)]
    [InlineData(null, "180004")]
    [InlineData("", "180004")]
    public void PermanentExportVersionGate_RejectsWrongContradictoryOrMalformedIdentity(
        string? serverVersion,
        string? serverVersionNumber)
    {
        PostgreSqlEnvironmentSnapshot environment = CreateEnvironment(
            serverVersion!,
            serverVersionNumber!);

        Action act = () => BaselineEvidenceWriter.ValidatePostgreSqlVersionForPermanentEvidence(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId),
            environment);

        act.Should().Throw<BaselinePlanValidationException>();
    }

    private static PostgreSqlEnvironmentSnapshot CreateEnvironment(
        string serverVersion,
        string serverVersionNumber) =>
        new(
            Version: $"PostgreSQL {serverVersion}",
            ServerVersion: serverVersion,
            ServerVersionNumber: serverVersionNumber,
            Database: "realestate_queryreview_version_gate",
            DatabaseSizeBytes: 1,
            ActiveVacuumCount: 0,
            Settings: [],
            Extensions: [],
            Relations: [],
            Indexes: [],
            TableStatistics: []);
}
