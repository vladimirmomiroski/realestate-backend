using System.Security.Cryptography;
using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class QueryReviewGenerationIsolationTests
{
    [Fact]
    public void Catalog_DefinesOnlyFrozenHistoricalAndFourRootDiscovery()
    {
        QueryReviewGenerations.All.Should().HaveCount(2);
        QueryReviewGenerations.All.Select(definition => definition.Id).Should().Equal(
            QueryReviewGenerations.FrozenHistoricalId,
            QueryReviewGenerations.FourRootDiscoveryId);

        QueryReviewGenerations.FrozenHistorical.IsFrozenHistorical.Should().BeTrue();
        QueryReviewGenerations.FourRootDiscovery.IsFrozenHistorical.Should().BeFalse();
        QueryReviewGenerations.FourRootDiscovery.Lanes.Select(lane => lane.Id).Should().Equal(
            QueryReviewGenerations.PostgreSql16LaneId,
            QueryReviewGenerations.PostgreSql184LaneId);
        QueryReviewLaneDefinition pg16 = QueryReviewGenerations.FourRootDiscovery.RequireLane(
            QueryReviewGenerations.PostgreSql16LaneId);
        QueryReviewLaneDefinition pg184 = QueryReviewGenerations.FourRootDiscovery.RequireLane(
            QueryReviewGenerations.PostgreSql184LaneId);
        QueryReviewGenerations.FourRootDiscovery.Lanes.Should().OnlyContain(lane =>
            lane.RequireImageDeclaredAnonymousVolume);
        pg16.RequiredContainerImage.Should().Be("postgres:16-alpine");
        pg16.PostgreSqlDataPath.Should().Be("/var/lib/postgresql/data");
        pg184.RequiredContainerImage.Should().Be("postgres:18.4");
        pg184.PostgreSqlDataPath.Should().Be("/var/lib/postgresql");
    }

    [Fact]
    public void PermanentDestinations_AreFixedDistinctAndContained()
    {
        string historical = QueryReviewGenerations.GetPermanentEvidencePath(
            QueryReviewGenerations.FrozenHistorical,
            QueryReviewGenerations.FrozenHistorical.Lanes.Single());
        string successor16 = QueryReviewGenerations.GetPermanentEvidencePath(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId));
        string successor18 = QueryReviewGenerations.GetPermanentEvidencePath(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId));

        historical.Should().EndWith(Path.Combine("docs", "benchmarks", "chapter-10f", "evidence"));
        successor16.Should().EndWith(Path.Combine(
            "docs", "benchmarks", "four-root-discovery-v1", "evidence", "postgresql-16"));
        successor18.Should().EndWith(Path.Combine(
            "docs", "benchmarks", "four-root-discovery-v1", "evidence", "postgresql-18.4"));
        new[] { historical, successor16, successor18 }.Distinct(
            StringComparer.OrdinalIgnoreCase).Should().HaveCount(3);
    }

    [Fact]
    public void PermanentDestination_RejectsLaneFromAnotherDefinition()
    {
        Action act = () => QueryReviewGenerations.GetPermanentEvidencePath(
            QueryReviewGenerations.FrozenHistorical,
            QueryReviewGenerations.FourRootDiscovery.Lanes[0]);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*exact catalog generation/lane definition pair*");
    }

    [Theory]
    [InlineData("../chapter-10f/evidence")]
    [InlineData("C:\\arbitrary\\evidence")]
    [InlineData("docs/benchmarks/FOUR-ROOT-DISCOVERY-V1/evidence/postgresql-18.4")]
    public void PermanentDestination_RejectsForgedTraversalRootedAndCaseAliasRoutes(
        string forgedPath)
    {
        QueryReviewLaneDefinition forgedLane =
            QueryReviewGenerations.FourRootDiscovery.Lanes[0] with
            {
                PermanentEvidenceRelativePath = forgedPath
            };
        QueryReviewGenerationDefinition forgedGeneration =
            QueryReviewGenerations.FourRootDiscovery with
            {
                Lanes = [forgedLane],
                PermanentExportFinalized = true
            };

        Action act = () => QueryReviewGenerations.GetPermanentEvidencePath(
            forgedGeneration,
            forgedLane);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*exact catalog generation/lane definition pair*");
    }

    [Theory]
    [InlineData((int)QueryReviewCommand.ProfileCreate)]
    [InlineData((int)QueryReviewCommand.ProfileVerify)]
    [InlineData((int)QueryReviewCommand.CaptureSql)]
    [InlineData((int)QueryReviewCommand.BaselineRun)]
    public void SuccessorOnlineOperations_FailAsNotProvisioned(int commandValue)
    {
        var command = (QueryReviewCommand)commandValue;
        Action act = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOnlineCommandAvailable(command);

        act.Should().Throw<QueryReviewGenerationNotReadyException>()
            .WithMessage("*not provisioned yet*");
    }

    [Fact]
    public void HistoricalGeneration_IsVerifyOnly()
    {
        Action online = () => QueryReviewGenerations.FrozenHistorical
            .EnsureOnlineCommandAvailable(QueryReviewCommand.ProfileVerify);
        Action export = () => QueryReviewGenerations.FrozenHistorical
            .EnsureOfflineCommandAvailable(
                QueryReviewCommand.BaselineExport,
                QueryReviewArtifactKind.RawRun);

        online.Should().Throw<QueryReviewGenerationNotReadyException>()
            .WithMessage("*frozen and verify-only*");
        export.Should().Throw<QueryReviewGenerationNotReadyException>()
            .WithMessage("*cannot replace its accepted evidence*");
    }

    [Theory]
    [InlineData((int)QueryReviewCommand.BaselineVerify)]
    [InlineData((int)QueryReviewCommand.BaselineExport)]
    public void SuccessorOfflineOperations_FailAsNotProvisioned(int commandValue)
    {
        var command = (QueryReviewCommand)commandValue;

        Action act = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOfflineCommandAvailable(command, QueryReviewArtifactKind.RawRun);

        act.Should().Throw<QueryReviewGenerationNotReadyException>()
            .WithMessage("*not*provisioned yet*");
    }

    [Fact]
    public void ExperimentalArtifact_CanNeverBecomePermanentExportInput()
    {
        QueryReviewGenerationDefinition exportReady =
            QueryReviewGenerations.FourRootDiscovery with
            {
                PermanentExportFinalized = true
            };

        Action act = () => exportReady.EnsureOfflineCommandAvailable(
            QueryReviewCommand.BaselineExport,
            QueryReviewArtifactKind.ExperimentalBundle);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*only a sealed raw run*");
    }

    [Fact]
    public async Task HistoricalWriterGate_FailsBeforeAnyExportInputIsRead()
    {
        Func<Task> act = () => BaselineEvidenceWriter.ExportAsync(
            QueryReviewGenerations.FrozenHistorical,
            QueryReviewGenerations.FrozenHistorical.Lanes.Single(),
            null!);

        await act.Should().ThrowAsync<QueryReviewGenerationNotReadyException>()
            .WithMessage("*cannot replace its accepted evidence*");
    }

    [Fact]
    public async Task HistoricalPermanentEvidence_VerifiesFromCommittedBytes()
    {
        string evidence = QueryReviewGenerations.GetPermanentEvidencePath(
            QueryReviewGenerations.FrozenHistorical,
            QueryReviewGenerations.FrozenHistorical.Lanes.Single());
        QueryReviewArtifactDescriptor descriptor =
            await QueryReviewArtifactRouter.InspectAsync(evidence);
        OfflineEvidenceVerificationResult result =
            await BaselineEvidenceWriter.VerifyPermanentAsync(descriptor);

        descriptor.Kind.Should().Be(QueryReviewArtifactKind.PermanentEvidence);
        descriptor.Generation.Should().BeSameAs(QueryReviewGenerations.FrozenHistorical);
        result.FileCount.Should().Be(69);
        string measurementHash = await ComputeRawSha256Async(
            Path.Combine(evidence, "baseline-measurements.json"));
        measurementHash.Should().Be(
            "d6dac6f58245f7ecd65b626ca1c3b85df2a2d39808a350e8b1536b82779f5a17");
    }

    [Fact]
    public async Task RequestedSuccessor_RejectsHistoricalRecordedGeneration()
    {
        string evidence = QueryReviewGenerations.GetPermanentEvidencePath(
            QueryReviewGenerations.FrozenHistorical,
            QueryReviewGenerations.FrozenHistorical.Lanes.Single());
        QueryReviewArtifactDescriptor descriptor =
            await QueryReviewArtifactRouter.InspectAsync(evidence);

        Action act = () => QueryReviewArtifactRouter.ValidateRequestedGeneration(
            QueryReviewGenerations.FourRootDiscoveryId,
            descriptor);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*does not match recorded generation*");
    }

    [Fact]
    public void UnknownGeneration_IsRejectedWithoutFallback()
    {
        Action act = () => QueryReviewGenerations.ResolveOrThrow("future-generation");

        act.Should().Throw<QueryReviewGenerationNotReadyException>()
            .WithMessage("*Unknown QueryReview profile/generation*");
    }

    [Fact]
    public void Cli_ParsesRunDirAliasAndExplicitProfileDeterministically()
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "baseline", "verify", "--profile", QueryReviewGenerations.FourRootDiscoveryId,
                "--run-dir", "."
            ],
            out QueryReviewOptions? options,
            out string? error);

        parsed.Should().BeTrue(error);
        options!.Profile.Should().Be(QueryReviewGenerations.FourRootDiscoveryId);
        options.RunDirectory.Should().Be(Path.GetFullPath("."));
    }

    [Fact]
    public void Cli_RequiresExplicitGenerationForOnlineOperations()
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "profile", "verify", "--connection-string",
                "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
                "--confirm-disposable"
            ],
            out _,
            out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("requires explicit '--profile'");
    }

    [Fact]
    public void ComparisonRun_IsRequiredOnlyForFuturePostgreSql184Export()
    {
        string primary = Path.Combine(Path.GetTempPath(), "queryreview-primary");
        string comparison = Path.Combine(Path.GetTempPath(), "queryreview-comparison");
        bool parsed = QueryReviewOptions.TryParse(
            [
                "baseline", "export", "--run-dir", primary,
                "--comparison-run-dir", comparison,
                "--confirm-evidence-export"
            ],
            out QueryReviewOptions? options,
            out string? error);
        parsed.Should().BeTrue(error);

        var pg16 = new QueryReviewArtifactDescriptor(
            QueryReviewGenerations.FourRootDiscovery,
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId),
            QueryReviewArtifactKind.RawRun,
            primary);
        var pg18 = pg16 with
        {
            Lane = QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql184LaneId)
        };

        Action pg16WithComparison = () => RealEstate.QueryReview.Program.ValidateComparisonOption(
            options!,
            pg16);
        Action pg18WithoutComparison = () =>
            RealEstate.QueryReview.Program.ValidateComparisonOption(
            options! with { ComparisonRunDirectory = null },
            pg18);
        Action pg18WithComparison = () =>
            RealEstate.QueryReview.Program.ValidateComparisonOption(options!, pg18);

        pg16WithComparison.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*valid only*PostgreSQL 18.4*");
        pg18WithoutComparison.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*requires --comparison-run-dir*");
        pg18WithComparison.Should().NotThrow();
    }

    [Fact]
    public async Task ExperimentalBundle_IsSelfManifestingPortableAndTamperEvident()
    {
        string source = CreateTemporaryDirectory();
        string copy = CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "summary.md"), "line one\nline two\n");
            Directory.CreateDirectory(Path.Combine(source, "plans"));
            await File.WriteAllTextAsync(Path.Combine(source, "plans", "one.json"), "{\"ok\":true}\n");

            await ExperimentalEvidenceBundle.WriteManifestAsync(
                source,
                QueryReviewGenerations.FrozenHistorical,
                QueryReviewGenerations.FrozenHistorical.Lanes.Single(),
                "portable-run",
                "chapter-10f-v2");

            CopyDirectory(source, copy);
            QueryReviewArtifactDescriptor descriptor =
                await QueryReviewArtifactRouter.InspectAsync(copy);
            OfflineEvidenceVerificationResult result =
                await ExperimentalEvidenceBundle.VerifyAsync(descriptor);
            result.FileCount.Should().Be(3);

            await File.AppendAllTextAsync(Path.Combine(copy, "summary.md"), "tamper");
            Func<Task> tampered = () => ExperimentalEvidenceBundle.VerifyAsync(descriptor);
            await tampered.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*hash mismatch*");
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            Directory.Delete(copy, recursive: true);
        }
    }

    [Fact]
    public async Task ExperimentalBundle_RejectsUnrecordedFile()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "artifact.json"), "{}\n");
            await ExperimentalEvidenceBundle.WriteManifestAsync(
                directory,
                QueryReviewGenerations.FrozenHistorical,
                QueryReviewGenerations.FrozenHistorical.Lanes.Single(),
                "extra-file-run");
            await File.WriteAllTextAsync(Path.Combine(directory, "extra.txt"), "unexpected");
            QueryReviewArtifactDescriptor descriptor =
                await QueryReviewArtifactRouter.InspectAsync(directory);

            Func<Task> act = () => ExperimentalEvidenceBundle.VerifyAsync(descriptor);
            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*file set does not match*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExperimentalBundle_RejectsManifestTraversal()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "artifact.json"), "{}\n");
            ExperimentalEvidenceManifest manifest =
                await ExperimentalEvidenceBundle.WriteManifestAsync(
                    directory,
                    QueryReviewGenerations.FrozenHistorical,
                    QueryReviewGenerations.FrozenHistorical.Lanes.Single(),
                    "traversal-run");
            manifest = manifest with
            {
                Artifacts = [manifest.Artifacts[0] with { Path = "../artifact.json" }]
            };
            await JsonArtifactOutput.WriteAsync(
                Path.Combine(directory, "experimental-manifest.json"),
                manifest);

            QueryReviewArtifactDescriptor descriptor =
                await QueryReviewArtifactRouter.InspectAsync(directory);
            Func<Task> act = () => ExperimentalEvidenceBundle.VerifyAsync(descriptor);

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*escapes its bundle*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExperimentalBundle_RejectsGenerationProfileMismatch()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "artifact.json"), "{}\n");
            ExperimentalEvidenceManifest manifest =
                await ExperimentalEvidenceBundle.WriteManifestAsync(
                    directory,
                    QueryReviewGenerations.FrozenHistorical,
                    QueryReviewGenerations.FrozenHistorical.Lanes.Single(),
                    "mismatch-run");
            await JsonArtifactOutput.WriteAsync(
                Path.Combine(directory, "experimental-manifest.json"),
                manifest with
                {
                    GenerationId = QueryReviewGenerations.FourRootDiscoveryId
                });

            Func<Task> act = () => QueryReviewArtifactRouter.InspectAsync(directory);
            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*profile does not match its recorded generation*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExperimentalBundle_RejectsCredentialMaterial()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "unsafe.txt"),
                "Password=must-not-be-recorded");

            Func<Task> act = () => ExperimentalEvidenceBundle.WriteManifestAsync(
                directory,
                QueryReviewGenerations.FrozenHistorical,
                QueryReviewGenerations.FrozenHistorical.Lanes.Single(),
                "unsafe-run");

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("Credential scan rejected experimental evidence file*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"queryreview-generation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.EnumerateDirectories(
                     source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(
                     source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static async Task<string> ComputeRawSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }
}
