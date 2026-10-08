using System.Security.Cryptography;
using FluentAssertions;
using Npgsql;
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
    [InlineData((int)QueryReviewCommand.CaptureSql)]
    [InlineData((int)QueryReviewCommand.BaselineRun)]
    public void SuccessorCaptureOperations_AreProvisioned(int commandValue)
    {
        var command = (QueryReviewCommand)commandValue;
        Action act = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOnlineCommandAvailable(command);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData((int)QueryReviewCommand.ProfileCreate)]
    [InlineData((int)QueryReviewCommand.ProfileVerify)]
    public void SuccessorProfileOperations_AreProvisioned(int commandValue)
    {
        var command = (QueryReviewCommand)commandValue;

        Action act = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOnlineCommandAvailable(command);

        act.Should().NotThrow();
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
    [InlineData((int)QueryReviewArtifactKind.RawRun)]
    [InlineData((int)QueryReviewArtifactKind.ExperimentalBundle)]
    public void SuccessorRawAndExperimentalVerification_IsProvisioned(int artifactKindValue)
    {
        var artifactKind = (QueryReviewArtifactKind)artifactKindValue;

        Action act = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOfflineCommandAvailable(QueryReviewCommand.BaselineVerify, artifactKind);

        act.Should().NotThrow();
    }

    [Fact]
    public void SuccessorPermanentVerificationAndExport_AreFinalizedForSealedRawRuns()
    {
        Action verify = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOfflineCommandAvailable(
                QueryReviewCommand.BaselineVerify,
                QueryReviewArtifactKind.PermanentEvidence);
        Action export = () => QueryReviewGenerations.FourRootDiscovery
            .EnsureOfflineCommandAvailable(
                QueryReviewCommand.BaselineExport,
                QueryReviewArtifactKind.RawRun);

        verify.Should().NotThrow();
        export.Should().NotThrow();
    }

    [Fact]
    public void ExperimentalArtifact_CanNeverBecomePermanentExportInput()
    {
        Action act = () => QueryReviewGenerations.FourRootDiscovery.EnsureOfflineCommandAvailable(
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
                "--confirm-disposable", "--container-name", "queryreview-disposable-test"
            ],
            out _,
            out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("requires explicit '--profile'");
    }

    [Theory]
    [InlineData((int)QueryReviewCommand.ProfileCreate, QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData((int)QueryReviewCommand.ProfileVerify, QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData((int)QueryReviewCommand.BaselineRun, QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData((int)QueryReviewCommand.ProfileCreate, QueryReviewGenerations.PostgreSql184LaneId)]
    [InlineData((int)QueryReviewCommand.ProfileVerify, QueryReviewGenerations.PostgreSql184LaneId)]
    [InlineData((int)QueryReviewCommand.BaselineRun, QueryReviewGenerations.PostgreSql184LaneId)]
    public void Cli_ResolvesExplicitSuccessorOnlineLane(
        int commandValue,
        string laneId)
    {
        QueryReviewCommand command = (QueryReviewCommand)commandValue;
        string[] commandTokens = command switch
        {
            QueryReviewCommand.ProfileCreate => ["profile", "create"],
            QueryReviewCommand.ProfileVerify => ["profile", "verify"],
            QueryReviewCommand.BaselineRun => ["baseline", "run"],
            _ => throw new InvalidOperationException()
        };
        string[] args =
        [
            .. commandTokens,
            "--profile", QueryReviewGenerations.FourRootDiscoveryId,
            "--lane", laneId,
            "--connection-string",
            "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
            "--confirm-disposable",
            "--container-name", "queryreview-disposable-test"
        ];

        bool parsed = QueryReviewOptions.TryParse(
            args,
            out QueryReviewOptions? options,
            out string? error);

        parsed.Should().BeTrue(error);
        QueryReviewLaneDefinition lane = QueryReviewGenerations.ResolveOnlineLane(
            QueryReviewGenerations.FourRootDiscovery,
            command,
            options!.Lane);
        lane.Id.Should().Be(laneId);
    }

    [Fact]
    public void Cli_OmittedOnlineLanePreservesPostgreSql16Compatibility()
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "profile", "verify",
                "--profile", QueryReviewGenerations.FourRootDiscoveryId,
                "--connection-string",
                "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
                "--confirm-disposable",
                "--container-name", "queryreview-disposable-test"
            ],
            out QueryReviewOptions? options,
            out string? error);

        parsed.Should().BeTrue(error);
        options!.Lane.Should().BeNull();
        QueryReviewGenerations.ResolveOnlineLane(
                QueryReviewGenerations.FourRootDiscovery,
                QueryReviewCommand.ProfileVerify,
                options.Lane)
            .Id.Should().Be(QueryReviewGenerations.PostgreSql16LaneId);
    }

    [Fact]
    public void Cli_RejectsDuplicateLaneArguments()
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "baseline", "run",
                "--profile", QueryReviewGenerations.FourRootDiscoveryId,
                "--lane", QueryReviewGenerations.PostgreSql16LaneId,
                "--lane", QueryReviewGenerations.PostgreSql184LaneId,
                "--connection-string",
                "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
                "--confirm-disposable",
                "--container-name", "queryreview-disposable-test"
            ],
            out _,
            out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("may be supplied only once");
    }

    [Theory]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData(QueryReviewGenerations.PostgreSql184LaneId)]
    public async Task BaselineOrchestration_PropagatesParsedLaneAfterContainerVerification(
        string laneId)
    {
        QueryReviewOptions options = ParseBaselineOptions(laneId);
        QueryReviewGenerationDefinition generation =
            QueryReviewGenerations.ResolveOrThrow(options.Profile);
        generation.EnsureOnlineCommandAvailable(options.Command);
        QueryReviewLaneDefinition lane = QueryReviewGenerations.ResolveOnlineLane(
            generation,
            options.Command,
            options.Lane);
        var order = new List<string>();

        (BaselineEnvironmentSnapshot Environment, RawBaselineManifest Manifest) result =
            await RealEstate.QueryReview.Program.ExecuteAfterContainerVerificationAsync(
                options,
                new NpgsqlConnectionStringBuilder(options.ConnectionString),
                lane,
                (_, containerName, verifiedLane, _) =>
                {
                    order.Add("container-verification");
                    containerName.Should().Be(options.ContainerName);
                    verifiedLane.Should().BeSameAs(lane);
                    return Task.CompletedTask;
                },
                () =>
                {
                    order.Add("database-access");
                    BaselineEnvironmentSnapshot environment =
                        RealEstate.QueryReview.Program.BindBaselineEnvironmentIdentity(
                            CreateEnvironment(),
                            generation,
                            lane);
                    RawBaselineManifest manifest = ExplainRunner.CreateRawBaselineManifest(
                        "lane-orchestration-test",
                        DateTime.UnixEpoch,
                        DateTime.UnixEpoch.AddSeconds(1),
                        generation,
                        lane,
                        environment,
                        new string('a', 64),
                        CreateShapeContract(generation),
                        [],
                        CreateProfileVerification(),
                        new string('b', 64));
                    return Task.FromResult((environment, manifest));
                });

        order.Should().Equal("container-verification", "database-access");
        result.Environment.GenerationId.Should().Be(generation.Id);
        result.Environment.ProfileVersion.Should().Be(generation.ProfileIdentity);
        result.Environment.LaneId.Should().Be(laneId);
        result.Manifest.GenerationId.Should().Be(generation.Id);
        result.Manifest.ProfileVersion.Should().Be(generation.ProfileIdentity);
        result.Manifest.LaneId.Should().Be(laneId);
    }

    [Theory]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData(QueryReviewGenerations.PostgreSql184LaneId)]
    public async Task BaselineOrchestration_ContainerFailurePreventsDatabaseAccess(
        string laneId)
    {
        QueryReviewOptions options = ParseBaselineOptions(laneId);
        QueryReviewGenerationDefinition generation =
            QueryReviewGenerations.ResolveOrThrow(options.Profile);
        QueryReviewLaneDefinition lane = QueryReviewGenerations.ResolveOnlineLane(
            generation,
            options.Command,
            options.Lane);
        var databaseAccessed = false;

        Func<Task> act = async () =>
            await RealEstate.QueryReview.Program.ExecuteAfterContainerVerificationAsync(
                options,
                new NpgsqlConnectionStringBuilder(options.ConnectionString),
                lane,
                (_, _, _, _) => throw new BaselinePlanValidationException(
                    "Injected exact-container verification failure."),
                () =>
                {
                    databaseAccessed = true;
                    return Task.FromResult(0);
                });

        await act.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*exact-container verification failure*");
        databaseAccessed.Should().BeFalse();
    }

    [Theory]
    [InlineData("postgresql-19")]
    [InlineData("PostgreSQL-18.4")]
    [InlineData(" ")]
    public void Cli_RejectsUnsupportedOrMalformedLane(string laneId)
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "profile", "verify",
                "--profile", QueryReviewGenerations.FourRootDiscoveryId,
                "--lane", laneId,
                "--connection-string",
                "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
                "--confirm-disposable",
                "--container-name", "queryreview-disposable-test"
            ],
            out _,
            out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("--lane");
    }

    [Theory]
    [InlineData((int)QueryReviewCommand.ProfileCreate)]
    [InlineData((int)QueryReviewCommand.ProfileVerify)]
    [InlineData((int)QueryReviewCommand.BaselineRun)]
    public void LaneAwareOnlineCommands_RequirePreDatabaseContainerVerification(
        int commandValue)
    {
        RealEstate.QueryReview.Program.RequiresVerifiedContainer(
                (QueryReviewCommand)commandValue)
            .Should().BeTrue();
    }

    [Fact]
    public void NonLaneAwareCaptureCommand_DoesNotBypassIntoLaneAwareContainerPath()
    {
        RealEstate.QueryReview.Program.RequiresVerifiedContainer(QueryReviewCommand.CaptureSql)
            .Should().BeFalse();
    }

    [Fact]
    public void Cli_RejectsLaneSelectionForNonLaneAwareCommand()
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "capture-sql",
                "--profile", QueryReviewGenerations.FourRootDiscoveryId,
                "--lane", QueryReviewGenerations.PostgreSql184LaneId,
                "--connection-string",
                "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
                "--confirm-disposable"
            ],
            out _,
            out string? error);

        parsed.Should().BeFalse();
        error.Should().Contain("valid only");
    }

    [Fact]
    public void FrozenHistoricalGeneration_RemainsUnavailableForOnlineLaneSelection()
    {
        Action act = () =>
            QueryReviewGenerations.FrozenHistorical.EnsureOnlineCommandAvailable(
                QueryReviewCommand.ProfileVerify);

        act.Should().Throw<QueryReviewGenerationNotReadyException>()
            .WithMessage("*frozen and verify-only*");
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

    private static QueryReviewOptions ParseBaselineOptions(string laneId)
    {
        bool parsed = QueryReviewOptions.TryParse(
            [
                "baseline", "run",
                "--profile", QueryReviewGenerations.FourRootDiscoveryId,
                "--lane", laneId,
                "--connection-string",
                "Host=localhost;Database=realestate_queryreview_test;Username=postgres;Password=test",
                "--confirm-disposable",
                "--container-name", "queryreview-disposable-test"
            ],
            out QueryReviewOptions? options,
            out string? error);

        parsed.Should().BeTrue(error);
        return options!;
    }

    private static BaselineEnvironmentSnapshot CreateEnvironment()
    {
        return new BaselineEnvironmentSnapshot(
            DateTime.UnixEpoch,
            new GitEnvironmentSnapshot("source-commit", "test", []),
            null!,
            null!,
            new PostgreSqlEnvironmentSnapshot(
                "PostgreSQL test",
                "18.4",
                "180004",
                "realestate_queryreview_test",
                0,
                0,
                [],
                [],
                [],
                [],
                []),
            "ef",
            "npgsql",
            "tool",
            "unbound",
            DeterministicProfileSeeder.CSharpSeed,
            DeterministicProfileSeeder.PostgreSqlSeed,
            TimeSpan.Zero);
    }

    private static QueryShapeContractDefinition CreateShapeContract(
        QueryReviewGenerationDefinition generation)
    {
        return new QueryShapeContractDefinition(
            generation.Id,
            generation.ProfileIdentity,
            "lane-orchestration-test",
            ShapeCount: 21,
            CommandCount: 83,
            TypedParameterCount: 190,
            PlanCount: 498,
            ProfileInvariantCount: 179,
            CommandKeys: [],
            ExpectedManifestSha256: null);
    }

    private static DeterministicProfileVerificationSnapshot CreateProfileVerification()
    {
        return new DeterministicProfileVerificationSnapshot(
            QueryReviewGenerations.FourRootDiscoveryId,
            ListingCount: 100_000,
            TranslationCount: 200_000,
            InvariantTotal: 179,
            InvariantPassed: 179,
            InvariantFailed: 0,
            ProfileSha256: new string('c', 64),
            InvariantManifestSha256: new string('d', 64),
            InvariantResultSha256: new string('d', 64));
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
