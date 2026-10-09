using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealEstate.QueryReview;

internal static partial class BaselineEvidenceWriter
{
    private const int HistoricalCommandCount = 33;
    private const int HistoricalParameterCount = 80;
    private const int HistoricalPlanCount = 198;
    private const int HistoricalInvariantCount = 61;
    private const long ExpectedListingCount = 100_000;
    private const long ExpectedTranslationCount = 200_000;
    private const int RequiredPostgreSqlMajorVersion = 16;
    private const string RequiredPostgreSqlVersion = "16.14";
    private const int RequiredPostgreSqlVersionNumber = 160014;
    private const string HistoricalResultSha256 =
        "7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36";
    private const string TrigramExtensionName = "pg_trgm";
    private const string TrigramExtensionVersion = "1.6";
    private const string TrigramIndexName = "IX_ListingTranslations_Q_Trigram";
    private const decimal Q1CountMaximumMilliseconds = 211.486m;
    private const decimal Q1FirstPageMaximumMilliseconds = 227.000m;
    private const long Q1CountMaximumSharedAccessBlocks = 4_844;
    private const long Q1FirstPageMaximumSharedAccessBlocks = 5_160;
    private static readonly string[] ExpectedTrigramColumns =
        ["Title", "City", "Municipality", "Neighborhood"];

    private static readonly string[] ExpectedTrigramOperatorClasses =
        ["gin_trgm_ops", "gin_trgm_ops", "gin_trgm_ops", "gin_trgm_ops"];

    private static readonly IReadOnlyDictionary<string, ExpectedA1Topology> ExpectedA1Topologies =
        new Dictionary<string, ExpectedA1Topology>(StringComparer.Ordinal)
        {
            ["A1-01-agency-existence"] = new(
                ["Index Only Scan", "Result"],
                ["Index Only Scan"],
                [],
                ["PK_Agencies"]),
            ["A1-02-filtered-count"] = new(
                ["Aggregate", "Bitmap Heap Scan", "Bitmap Index Scan"],
                ["Bitmap Heap Scan", "Bitmap Index Scan"],
                [],
                ["IX_Listings_AgencyId"]),
            ["A1-03-page-root"] = new(
                ["Bitmap Heap Scan", "Bitmap Index Scan", "Index Scan", "Limit", "Nested Loop", "Sort"],
                ["Bitmap Heap Scan", "Bitmap Index Scan", "Index Scan"],
                ["Left"],
                ["IX_Listings_AgencyId", "PK_ListingApartmentDetails", "PK_ListingHouseDetails"]),
            ["A1-04-translation-split"] = new(
                ["Incremental Sort", "Index Only Scan", "Index Scan", "Nested Loop"],
                ["Index Only Scan", "Index Scan"],
                ["Inner"],
                ["IX_ListingTranslations_ListingId_LanguageCode", "PK_Listings"]),
            ["A1-05-image-split"] = new(
                ["Incremental Sort", "Index Only Scan", "Index Scan", "Nested Loop"],
                ["Index Only Scan", "Index Scan"],
                ["Inner"],
                ["IX_ListingImages_ListingId_SortOrder", "PK_Listings"])
        };

    private static PermanentBaselineExpectations GetExpectations(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane)
    {
        if (generation.IsFrozenHistorical)
        {
            return new PermanentBaselineExpectations(
                ShapeCount: 8,
                HistoricalCommandCount,
                HistoricalParameterCount,
                HistoricalPlanCount,
                HistoricalInvariantCount,
                DeterministicProfileSeeder.ProfileVersion,
                HistoricalResultSha256,
                RequiredPostgreSqlMajorVersion,
                RequiredPostgreSqlVersion,
                RequiredPostgreSqlVersionNumber);
        }

        SuccessorPermanentExportContract contract = generation.PermanentExportContract
            ?? throw new BaselinePlanValidationException(
                "The successor generation is missing its permanent-export contract.");
        SuccessorPermanentExportManifest.ValidateContract(contract);
        QueryShapeContractDefinition shape = DiscoveryQueryShapeManifest.GetDefinition(generation);
        return new PermanentBaselineExpectations(
            shape.ShapeCount,
            shape.CommandCount,
            shape.TypedParameterCount,
            shape.PlanCount,
            shape.ProfileInvariantCount,
            contract.ProfileIdentity,
            contract.ResultOrderIdentitySha256,
            lane.PostgreSqlMajorVersion,
            lane.PostgreSqlMajorVersion == 16 ? "16.14" : "18.4",
            lane.PostgreSqlMajorVersion == 16 ? 160014 : 180004);
    }

    public static Task<BaselineEvidenceExportResult> ExportAsync(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        BaselineVerificationResult verification,
        string? comparisonRunDirectory = null,
        CancellationToken cancellationToken = default)
    {
        generation.EnsureOfflineCommandAvailable(
            QueryReviewCommand.BaselineExport,
            QueryReviewArtifactKind.RawRun);

        return ExportCoreAsync(
            generation,
            lane,
            verification,
            comparisonRunDirectory,
            cancellationToken);
    }

    public static async Task<OfflineEvidenceVerificationResult>
        VerifyPermanentAsync(
            QueryReviewArtifactDescriptor descriptor,
            CancellationToken cancellationToken = default)
    {
        if (descriptor.Kind != QueryReviewArtifactKind.PermanentEvidence)
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence verification requires a fixed catalog lane destination.");
        }

        descriptor.Generation.EnsureOfflineCommandAvailable(
            QueryReviewCommand.BaselineVerify,
            QueryReviewArtifactKind.PermanentEvidence);
        PermanentBaselineExpectations expectations = GetExpectations(
            descriptor.Generation,
            descriptor.Lane);

        string measurementsPath = Path.Combine(
            descriptor.Directory,
            "baseline-measurements.json");
        CuratedBaselineMeasurements measurements =
            await ReadRequiredJsonAsync<CuratedBaselineMeasurements>(
                measurementsPath,
                cancellationToken);
        PermanentEvidenceMetadata metadata = measurements.PermanentEvidence
            ?? throw new BaselinePlanValidationException(
                "Historical permanent evidence is missing completeness metadata.");
        ValidatePersistedPostgreSqlVersion(
            descriptor.Generation,
            descriptor.Lane,
            metadata.CaptureIdentity);

        if (!descriptor.Generation.MatchesRecordedProfile(
                metadata.ProfileVerification.ProfileIdentity) ||
            metadata.ProfileVerification.ListingCount != ExpectedListingCount ||
            metadata.ProfileVerification.TranslationCount != ExpectedTranslationCount ||
            metadata.ProfileVerification.InvariantTotal != expectations.InvariantCount ||
            metadata.ProfileVerification.InvariantPassed != expectations.InvariantCount ||
            metadata.ProfileVerification.InvariantFailed != 0 ||
            !metadata.ProfileVerification.Passed ||
            !string.Equals(
                metadata.SemanticResultIdentity.ExpectedResultSha256,
                expectations.ResultOrderIdentitySha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                metadata.SemanticResultIdentity.ActualResultSha256,
                expectations.ResultOrderIdentitySha256,
                StringComparison.Ordinal) ||
            metadata.CaptureIdentity.CommandCount != expectations.CommandCount ||
            metadata.CaptureIdentity.TypedParameterCount != expectations.ParameterCount ||
            metadata.CaptureIdentity.RawPlanCount != expectations.PlanCount ||
            !measurements.Q1Gate.Passed)
        {
            throw new BaselinePlanValidationException(
                "Frozen historical permanent identity, totals, semantic hash, PostgreSQL " +
                "version, or Q1 gate drifted.");
        }

        string[] commandKeys = measurements.Commands
            .Select(command => command.CommandKey)
            .ToArray();
        ValidateCommandKeys(commandKeys, expectations.CommandCount);
        HashSet<string> expectedFiles = BuildExpectedFileSet(commandKeys);
        AddEmbeddedComparisonFiles(expectedFiles, metadata.PostgreSql16Comparison);
        ValidateExactFileSet(descriptor.Directory, expectedFiles);
        await ValidatePermanentEvidenceAsync(
            descriptor.Directory,
            measurements,
            descriptor.Generation,
            descriptor.Lane,
            expectations,
            cancellationToken);
        ScanForCredentials(descriptor.Directory);

        return new OfflineEvidenceVerificationResult(
            descriptor.Generation.Id,
            descriptor.Lane.Id,
            descriptor.Kind,
            descriptor.Directory,
            expectedFiles.Count);
    }

    private static async Task<BaselineEvidenceExportResult> ExportCoreAsync(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        BaselineVerificationResult verification,
        string? comparisonRunDirectory,
        CancellationToken cancellationToken = default)
    {
        PermanentBaselineExpectations expectations = GetExpectations(generation, lane);
        ValidateVerificationResult(verification, expectations);

        var manifest = await ReadRequiredJsonAsync<RawBaselineManifest>(
            Path.Combine(verification.RunDirectory, "manifest.json"),
            cancellationToken);
        var environment = await ReadRequiredJsonAsync<BaselineEnvironmentSnapshot>(
            Path.Combine(verification.RunDirectory, "environment-raw.json"),
            cancellationToken);
        var captureRun = await ReadRequiredJsonAsync<SqlCaptureRun>(
            ResolveWithin(verification.RunDirectory, manifest.CapturedCommandsPath),
            cancellationToken);
        bool isPostgreSql184Successor =
            generation == QueryReviewGenerations.FourRootDiscovery &&
            lane.Id == QueryReviewGenerations.PostgreSql184LaneId;

        if (isPostgreSql184Successor && string.IsNullOrWhiteSpace(comparisonRunDirectory))
        {
            throw new BaselinePlanValidationException(
                "PostgreSQL 18.4 permanent export requires a verified PostgreSQL 16 comparison run.");
        }

        if (!isPostgreSql184Successor && comparisonRunDirectory is not null)
        {
            throw new BaselinePlanValidationException(
                "A PostgreSQL 16 comparison run is valid only for PostgreSQL 18.4 successor export.");
        }

        VerifiedSuccessorComparisonRun? comparison = isPostgreSql184Successor
            ? await SuccessorComparisonRunVerifier.VerifyAsync(
                new QueryReviewArtifactDescriptor(
                    generation,
                    lane,
                    QueryReviewArtifactKind.RawRun,
                    verification.RunDirectory),
                verification,
                comparisonRunDirectory!,
                cancellationToken)
            : null;

        if (generation == QueryReviewGenerations.FourRootDiscovery)
        {
            string recordedShapeIdentity =
                DiscoveryQueryShapeManifest.ValidateRecordedManifest(generation, captureRun);

            if (!string.Equals(
                    recordedShapeIdentity,
                    manifest.QueryShapeManifestSha256,
                    StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    "The raw manifest and captured successor query-shape identities differ.");
            }

            SuccessorPermanentExportContract contract = generation.PermanentExportContract!;
            SuccessorPermanentExportSnapshot snapshot =
                await SuccessorPermanentExportManifest.CaptureSnapshotAsync(
                    generation,
                    lane,
                    manifest,
                    environment,
                    captureRun,
                    cancellationToken);
            SuccessorPermanentExportManifest.ValidateSnapshot(contract, snapshot);
            SuccessorPermanentExportManifest.ValidateNoIndexCatalog(environment);
        }

        ValidateIdentity(
            manifest,
            environment,
            verification.Measurements,
            generation,
            lane,
            expectations);
        var evidenceCore = BuildEvidenceCore(
            generation,
            lane,
            expectations,
            verification,
            manifest,
            environment,
            captureRun);

        var commandKeys = verification.Measurements.Commands
            .Select(command => command.CommandKey)
            .ToArray();
        ValidateCommandKeys(commandKeys, expectations.CommandCount);

        var expectedFiles = BuildExpectedFileSet(commandKeys);
        await ValidateCuratedExportInputAsync(
            generation,
            lane,
            verification.CuratedDirectory,
            commandKeys,
            cancellationToken);
        await ValidateCuratedMeasurementsAsync(verification, cancellationToken);
        await ValidateEnvironmentArtifactAsync(verification, cancellationToken);
        await ValidateMedianPlansAsync(verification, cancellationToken);
        ScanForCredentials(verification.CuratedDirectory);

        EmbeddedComparisonEvidence? embeddedComparison = comparison is null
            ? null
            : await BuildEmbeddedComparisonEvidenceAsync(comparison, cancellationToken);
        var publicationFiles = new HashSet<string>(expectedFiles, StringComparer.Ordinal);

        if (embeddedComparison is not null)
        {
            foreach (ArtifactHashEvidence artifact in embeddedComparison.Artifacts)
            {
                publicationFiles.Add($"{embeddedComparison.RelativeRoot}/{artifact.Path}");
            }
        }

        if (generation == QueryReviewGenerations.FourRootDiscovery)
        {
            await SuccessorPermanentExportManifest.ValidateCurrentPublicationSourceAsync(
                generation.PermanentExportContract!,
                manifest.GitCommit,
                cancellationToken);
        }

        var publishedHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        PermanentEvidencePublicationResult publication =
            await PermanentEvidencePublisher.PublishAsync(
                generation,
                lane,
                async (stagingDirectory, publicationCancellationToken) =>
                {
                    foreach (var relativePath in expectedFiles
                                 .Where(path => path is not "baseline-measurements.json" and
                                     not "baseline-summary.md")
                                 .OrderBy(path => path, StringComparer.Ordinal))
                    {
                        var sourcePath = ResolveWithin(
                            verification.CuratedDirectory,
                            relativePath);
                        var stagingPath = ResolveWithin(stagingDirectory, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);

                        var sourceHash = await ComputeSha256Async(
                            sourcePath,
                            publicationCancellationToken);
                        File.Copy(sourcePath, stagingPath, overwrite: false);
                        var stagingHash = await ComputeSha256Async(
                            stagingPath,
                            publicationCancellationToken);

                        if (!string.Equals(sourceHash, stagingHash, StringComparison.Ordinal))
                        {
                            throw new BaselinePlanValidationException(
                                $"Exported hash mismatch for '{relativePath}'.");
                        }
                    }

                    if (comparison is not null && embeddedComparison is not null)
                    {
                        foreach (ArtifactHashEvidence artifact in embeddedComparison.Artifacts)
                        {
                            string sourcePath = ResolveWithin(
                                comparison.Verification.CuratedDirectory,
                                artifact.Path);
                            string destinationRelativePath =
                                $"{embeddedComparison.RelativeRoot}/{artifact.Path}";
                            string stagingPath = ResolveWithin(
                                stagingDirectory,
                                destinationRelativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
                            File.Copy(sourcePath, stagingPath, overwrite: false);
                            string copiedHash = await ComputeSha256Async(
                                stagingPath,
                                publicationCancellationToken);

                            if (!string.Equals(artifact.Sha256, copiedHash, StringComparison.Ordinal))
                            {
                                throw new BaselinePlanValidationException(
                                    $"PostgreSQL 16 comparison artifact drifted while copying '{artifact.Path}'.");
                            }
                        }
                    }

                    var summaryPath = Path.Combine(stagingDirectory, "baseline-summary.md");
                    await File.WriteAllTextAsync(
                        summaryPath,
                        BuildPermanentSummary(
                            generation,
                            lane,
                            verification.Measurements,
                            evidenceCore,
                            comparison?.Report),
                        publicationCancellationToken);

                    var artifactIntegrity = await BuildArtifactIntegrityAsync(
                        stagingDirectory,
                        commandKeys,
                        publicationCancellationToken);
                    var permanentMeasurements = BuildPermanentMeasurements(
                        verification.Measurements,
                        evidenceCore,
                        artifactIntegrity,
                        generation,
                        embeddedComparison);
                    await JsonArtifactOutput.WriteAsync(
                        Path.Combine(stagingDirectory, "baseline-measurements.json"),
                        permanentMeasurements,
                        publicationCancellationToken);

                    ValidateExactFileSet(stagingDirectory, publicationFiles);
                    await ValidatePermanentEvidenceAsync(
                        stagingDirectory,
                        permanentMeasurements,
                        generation,
                        lane,
                        expectations,
                        publicationCancellationToken);
                    ScanForCredentials(stagingDirectory);

                    foreach (var relativePath in publicationFiles.OrderBy(
                                 path => path,
                                 StringComparer.Ordinal))
                    {
                        publishedHashes.Add(
                            relativePath,
                            await ComputeSha256Async(
                                ResolveWithin(stagingDirectory, relativePath),
                                publicationCancellationToken));
                    }
                },
                async (destinationDirectory, publicationCancellationToken) =>
                {
                    ValidateExactFileSet(destinationDirectory, publicationFiles);
                    ScanForCredentials(destinationDirectory);

                    foreach (var expected in publishedHashes)
                    {
                        var destinationPath = ResolveWithin(
                            destinationDirectory,
                            expected.Key);
                        var destinationHash = await ComputeSha256Async(
                            destinationPath,
                            publicationCancellationToken);

                        if (!string.Equals(
                                expected.Value,
                                destinationHash,
                                StringComparison.Ordinal))
                        {
                            throw new BaselinePlanValidationException(
                                $"Permanent evidence hash mismatch for '{expected.Key}'.");
                        }
                    }
                },
                cancellationToken);

        return new BaselineEvidenceExportResult(
            publication.DestinationDirectory,
            publishedHashes.Count,
            publishedHashes);
    }

    private static void ValidateVerificationResult(
        BaselineVerificationResult verification,
        PermanentBaselineExpectations expectations)
    {
        var measurements = verification.Measurements;

        if (!verification.CredentialScanPassed ||
            !measurements.Q1Gate.Passed ||
            measurements.CommandCount != expectations.CommandCount ||
            measurements.SampleCount != expectations.PlanCount ||
            measurements.WarmUpSampleCount != expectations.CommandCount ||
            measurements.MeasuredSampleCount != expectations.CommandCount * 5 ||
            measurements.Commands.Count != expectations.CommandCount ||
            measurements.Anomalies.Count != 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence export requires a complete, anomaly-free, credential-clean " +
                "verified baseline with a passing Q1 gate.");
        }
    }

    private static void ValidateIdentity(
        RawBaselineManifest manifest,
        BaselineEnvironmentSnapshot environment,
        BaselineMeasurementsRaw measurements,
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        PermanentBaselineExpectations expectations)
    {
        if (!string.Equals(
                manifest.BaselineRunId,
                measurements.BaselineRunId,
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Verified measurements do not match the raw manifest run identity.");
        }

        if (manifest.CommandCount != expectations.CommandCount ||
            manifest.ParameterCount != expectations.ParameterCount ||
            manifest.PlanCount != expectations.PlanCount ||
            manifest.WarmUpRunsPerCommand != 1 ||
            manifest.MeasuredRunsPerCommand != 5 ||
            manifest.Samples.Count != expectations.PlanCount ||
            !manifest.CredentialScanPassed)
        {
            throw new BaselinePlanValidationException(
                "Raw manifest totals or credential status are not eligible for export.");
        }

        if (!string.Equals(manifest.ProfileVersion, expectations.ProfileIdentity,
                StringComparison.Ordinal) ||
            !string.Equals(environment.ProfileVersion, manifest.ProfileVersion, StringComparison.Ordinal) ||
            environment.CSharpSeed != manifest.CSharpSeed ||
            environment.PostgreSqlSeed != manifest.PostgreSqlSeed ||
            !string.Equals(environment.Git.Commit, manifest.GitCommit, StringComparison.Ordinal) ||
            !string.Equals(environment.PostgreSql.ServerVersion, manifest.PostgreSqlVersion,
                StringComparison.Ordinal) ||
            !environment.PostgreSql.Database.StartsWith("realestate_queryreview", StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Commit, profile, seed, database, or PostgreSQL identity mismatch prevents export.");
        }

        ValidatePostgreSqlVersionForPermanentEvidence(
            generation,
            lane,
            environment.PostgreSql);
    }

    private static EvidenceCore BuildEvidenceCore(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        PermanentBaselineExpectations expectations,
        BaselineVerificationResult verification,
        RawBaselineManifest manifest,
        BaselineEnvironmentSnapshot environment,
        SqlCaptureRun captureRun)
    {
        var profile = BuildProfileEvidence(manifest, expectations);
        var semanticIdentity = BuildSemanticIdentity(
            generation,
            manifest,
            captureRun,
            expectations);
        var lockedResults = BuildLockedResultEvidence(generation, captureRun);
        ValidateQ1Evidence(verification.Measurements);
        A1ApprovedExceptionEvidence? a1Exception = generation.IsFrozenHistorical
            ? BuildA1ExceptionEvidence(verification.Measurements)
            : null;
        var captureIdentity = BuildCaptureIdentity(
            generation,
            lane,
            expectations,
            verification,
            manifest,
            environment);

        return new EvidenceCore(
            profile,
            semanticIdentity,
            lockedResults,
            a1Exception,
            captureIdentity);
    }

    internal static void ValidateQ1Evidence(BaselineMeasurementsRaw measurements)
    {
        var count = measurements.Commands.Single(command =>
            string.Equals(command.CommandKey, "Q1-01-filtered-count", StringComparison.Ordinal));
        var page = measurements.Commands.Single(command =>
            string.Equals(command.CommandKey, "Q1-02-page-root", StringComparison.Ordinal));
        var firstPage = measurements.Sequences.Single(sequence =>
            string.Equals(sequence.SequenceId, "Q1-first-page", StringComparison.Ordinal));
        var countBuffers = checked((long)count.SharedAccessBlocksMedian.Value);
        var firstPageBuffers = checked((long)firstPage.SharedAccessBlocksMedian.Value);

        if (count.ExecutionTimeMedian.Value > Q1CountMaximumMilliseconds ||
            firstPage.ExecutionTimeMedian.Value > Q1FirstPageMaximumMilliseconds ||
            countBuffers > Q1CountMaximumSharedAccessBlocks ||
            firstPageBuffers > Q1FirstPageMaximumSharedAccessBlocks ||
            !count.IndexNames.Contains(TrigramIndexName, StringComparer.Ordinal) ||
            !page.IndexNames.Contains(TrigramIndexName, StringComparer.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence requires Q1 timing/buffer gates, trigram-index use in count " +
                "and page plans, and absence of the old translation sequential scan.");
        }

        ValidateQ1SequentialScans(count, page);
    }

    private static void ValidateQ1SequentialScans(
        params CommandMeasurementSummary[] commands)
    {
        PlanNodeMeasurement[] sequentialScans = commands
            .SelectMany(command => command.Samples)
            .SelectMany(sample => sample.Nodes)
            .Where(node => string.Equals(node.NodeType, "Seq Scan", StringComparison.Ordinal))
            .ToArray();

        if (sequentialScans.Any(node => string.IsNullOrWhiteSpace(node.Relation)))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence rejects a malformed Q1 sequential scan with a missing or " +
                "blank Relation Name; an alias cannot prove that the scanned relation is safe.");
        }

        if (sequentialScans.Any(node => string.Equals(
                node.Relation,
                "ListingTranslations",
                StringComparison.Ordinal)))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence rejects a Q1 ListingTranslations sequential scan; " +
                "the accepted translation path must use IX_ListingTranslations_Q_Trigram.");
        }
    }

    private static ProfileVerificationEvidence BuildProfileEvidence(
        RawBaselineManifest manifest,
        PermanentBaselineExpectations expectations)
    {
        var profile = manifest.ProfileVerification;

        bool successor = string.Equals(
            expectations.ProfileIdentity,
            QueryReviewGenerations.FourRootDiscoveryId,
            StringComparison.Ordinal);

        if (profile is null ||
            !string.Equals(
                profile.ProfileIdentity,
                expectations.ProfileIdentity,
                StringComparison.Ordinal) ||
            profile.ListingCount != ExpectedListingCount ||
            profile.TranslationCount != ExpectedTranslationCount ||
            profile.InvariantTotal != expectations.InvariantCount ||
            profile.InvariantPassed != expectations.InvariantCount ||
            profile.InvariantFailed != 0 ||
            (successor &&
             (!string.Equals(
                  profile.ProfileSha256,
                  SuccessorPermanentExportManifest.AcceptedProfileSha256,
                  StringComparison.Ordinal) ||
              !string.Equals(
                  profile.InvariantManifestSha256,
                  SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256,
                  StringComparison.Ordinal) ||
              !string.Equals(
                  profile.InvariantResultSha256,
                  SuccessorPermanentExportManifest.AcceptedInvariantResultSha256,
                  StringComparison.Ordinal))))
        {
            throw new BaselinePlanValidationException(
                "Permanent export requires the persisted successful generation-specific " +
                "100,000-listing, 200,000-translation profile verification.");
        }

        return new ProfileVerificationEvidence(
            profile.ProfileIdentity,
            profile.ListingCount,
            profile.TranslationCount,
            profile.InvariantTotal,
            profile.InvariantPassed,
            profile.InvariantFailed,
            Passed: true,
            profile.ProfileSha256,
            profile.InvariantManifestSha256,
            profile.InvariantResultSha256);
    }

    private static SemanticResultIdentityEvidence BuildSemanticIdentity(
        QueryReviewGenerationDefinition generation,
        RawBaselineManifest manifest,
        SqlCaptureRun captureRun,
        PermanentBaselineExpectations expectations)
    {
        var actualResultSha256 = generation == QueryReviewGenerations.FourRootDiscovery
            ? DiscoveryQueryShapeManifest.ComputeResultIdentitySha256(captureRun.ShapeResults)
            : ComputeSha256(JsonSerializer.Serialize(
                captureRun.ShapeResults,
                JsonArtifactOutput.SerializerOptions));
        var comparisonPassed = string.Equals(
                                   actualResultSha256,
                                   expectations.ResultOrderIdentitySha256,
                                   StringComparison.Ordinal) &&
                               (generation == QueryReviewGenerations.FourRootDiscovery ||
                                string.Equals(
                                    actualResultSha256,
                                    manifest.ResultSha256,
                                    StringComparison.Ordinal));

        if (!comparisonPassed)
        {
            throw new BaselinePlanValidationException(
                "Verified production semantic results do not match the locked generation result/order identity.");
        }

        return new SemanticResultIdentityEvidence(
            expectations.ResultOrderIdentitySha256,
            actualResultSha256,
            comparisonPassed);
    }

    private static IReadOnlyList<LockedResultComparisonEvidence> BuildLockedResultEvidence(
        QueryReviewGenerationDefinition generation,
        SqlCaptureRun captureRun)
    {
        IReadOnlyList<LockedQueryShapeExpectation> expectations =
            generation == QueryReviewGenerations.FourRootDiscovery
                ? (captureRun.QueryShapeManifest?.Results
                       ?? throw new BaselinePlanValidationException(
                           "Successor capture is missing the validated query-shape manifest."))
                    .Select(result => new LockedQueryShapeExpectation(
                        result.ShapeId,
                        result.TotalCount ?? 0,
                        result.ItemCount,
                        result.OrderedIds))
                    .ToArray()
                : QueryShapeDefinitions.GetLockedResultExpectations();

        if (captureRun.ShapeResults.Count != expectations.Count)
        {
            throw new BaselinePlanValidationException(
                "Verified production capture has a missing or extra locked result sequence.");
        }

        var results = new List<LockedResultComparisonEvidence>(expectations.Count);

        foreach (var expectation in expectations)
        {
            var actual = captureRun.ShapeResults.SingleOrDefault(result =>
                string.Equals(result.ShapeId, expectation.ShapeId, StringComparison.Ordinal));

            if (actual is null || actual.ActualTotalCount is null)
            {
                throw new BaselinePlanValidationException(
                    $"Locked result metadata is missing for '{expectation.ShapeId}'.");
            }

            var expectedIds = expectation.ExpectedOrderedIds.ToArray();
            var actualIds = actual.ResultIds.ToArray();
            var expectedIdsHash = ComputeOrderedIdsSha256(expectedIds);
            var actualIdsHash = ComputeOrderedIdsSha256(actualIds);
            var totalPassed = actual.ExpectedTotalCount == expectation.ExpectedTotalCount &&
                              actual.ActualTotalCount == expectation.ExpectedTotalCount;
            var itemPassed = actual.ExpectedItemCount == expectation.ExpectedItemCount &&
                             actual.ActualItemCount == expectation.ExpectedItemCount;
            var idsPassed = expectedIds.SequenceEqual(actualIds) &&
                            string.Equals(expectedIdsHash, actualIdsHash, StringComparison.Ordinal);
            var passed = totalPassed && itemPassed && idsPassed;

            if (!passed)
            {
                throw new BaselinePlanValidationException(
                    $"Locked totals, item counts, or ordered IDs drifted for '{expectation.ShapeId}'.");
            }

            results.Add(new LockedResultComparisonEvidence(
                expectation.ShapeId,
                expectation.ExpectedTotalCount,
                actual.ActualTotalCount.Value,
                totalPassed,
                expectation.ExpectedItemCount,
                actual.ActualItemCount,
                itemPassed,
                expectedIds,
                actualIds,
                expectedIdsHash,
                actualIdsHash,
                idsPassed,
                passed));
        }

        return results;
    }

    private static A1ApprovedExceptionEvidence BuildA1ExceptionEvidence(
        BaselineMeasurementsRaw measurements)
    {
        var firstPage = BuildA1SequenceEvidence(
            measurements,
            "A1-first-page",
            correctedPreIndexMilliseconds: 2.335m,
            expectedSharedAccessBlocks: 884);
        var supplementary = BuildA1SequenceEvidence(
            measurements,
            "A1-endpoint-supplementary",
            correctedPreIndexMilliseconds: 3.278m,
            expectedSharedAccessBlocks: 1_388);
        var topologies = new List<A1CommandTopologyEvidence>(ExpectedA1Topologies.Count);
        var scanJoinIndexTopologyUnchanged = true;
        var nodeTopologyUnchanged = true;

        foreach (var (commandKey, expected) in ExpectedA1Topologies)
        {
            var command = measurements.Commands.Single(candidate =>
                string.Equals(candidate.CommandKey, commandKey, StringComparison.Ordinal));
            var actualNodeTypes = OrderedDistinct(command.Samples
                .SelectMany(sample => sample.Nodes)
                .Select(node => node.NodeType));
            var actualScanTypes = OrderedDistinct(command.ScanTypes);
            var actualJoinTypes = OrderedDistinct(command.JoinTypes);
            var actualIndexNames = OrderedDistinct(command.IndexNames);
            var nodeTypesPassed = SetEquals(expected.NodeTypes, actualNodeTypes);
            var scansPassed = SetEquals(expected.ScanTypes, actualScanTypes);
            var joinsPassed = SetEquals(expected.JoinTypes, actualJoinTypes);
            var indexesPassed = SetEquals(expected.IndexNames, actualIndexNames);
            var comparisonPassed = nodeTypesPassed && scansPassed && joinsPassed && indexesPassed;

            nodeTopologyUnchanged &= nodeTypesPassed;
            scanJoinIndexTopologyUnchanged &= scansPassed && joinsPassed && indexesPassed;
            topologies.Add(new A1CommandTopologyEvidence(
                commandKey,
                OrderedDistinct(expected.NodeTypes),
                actualNodeTypes,
                OrderedDistinct(expected.ScanTypes),
                actualScanTypes,
                OrderedDistinct(expected.JoinTypes),
                actualJoinTypes,
                OrderedDistinct(expected.IndexNames),
                actualIndexNames,
                comparisonPassed));
        }

        var a1Samples = measurements.Commands
            .Where(command => string.Equals(command.ShapeId, "A1", StringComparison.Ordinal))
            .SelectMany(command => command.Samples)
            .ToArray();
        var noNewExpensiveNode = nodeTopologyUnchanged &&
                                 a1Samples.All(sample =>
                                     !sample.Spilled &&
                                     sample.TopLevelBuffers.TempRead == 0 &&
                                     sample.TopLevelBuffers.TempWritten == 0);
        var buffersEquivalent = firstPage.SharedAccessBlocksEquivalent &&
                                supplementary.SharedAccessBlocksEquivalent;
        var accepted = firstPage.AbsoluteDifferenceBelowOneMillisecond &&
                       supplementary.AbsoluteDifferenceBelowOneMillisecond &&
                       buffersEquivalent &&
                       scanJoinIndexTopologyUnchanged &&
                       noNewExpensiveNode;

        if (!accepted)
        {
            throw new BaselinePlanValidationException(
                "The verified A1 measurements do not satisfy the approved sub-millisecond " +
                "exception, buffer, topology, and no-new-expensive-node rules.");
        }

        return new A1ApprovedExceptionEvidence(
            firstPage,
            supplementary,
            topologies,
            buffersEquivalent,
            scanJoinIndexTopologyUnchanged,
            noNewExpensiveNode,
            accepted);
    }

    private static A1SequenceExceptionEvidence BuildA1SequenceEvidence(
        BaselineMeasurementsRaw measurements,
        string sequenceId,
        decimal correctedPreIndexMilliseconds,
        long expectedSharedAccessBlocks)
    {
        var sequence = measurements.Sequences.Single(candidate =>
            string.Equals(candidate.SequenceId, sequenceId, StringComparison.Ordinal));
        var indexedMilliseconds = sequence.ExecutionTimeMedian.Value;
        var difference = indexedMilliseconds - correctedPreIndexMilliseconds;
        var absoluteDifference = Math.Abs(difference);
        var relativeDifference = difference / correctedPreIndexMilliseconds * 100m;
        var actualSharedAccessBlocks = checked((long)sequence.SharedAccessBlocksMedian.Value);

        return new A1SequenceExceptionEvidence(
            sequenceId,
            correctedPreIndexMilliseconds,
            indexedMilliseconds,
            difference,
            absoluteDifference,
            relativeDifference,
            absoluteDifference < 1m,
            expectedSharedAccessBlocks,
            actualSharedAccessBlocks,
            actualSharedAccessBlocks == expectedSharedAccessBlocks);
    }

    private static CaptureIdentityEvidence BuildCaptureIdentity(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        PermanentBaselineExpectations expectations,
        BaselineVerificationResult verification,
        RawBaselineManifest manifest,
        BaselineEnvironmentSnapshot environment)
    {
        ValidatePostgreSqlVersionForPermanentEvidence(
            generation,
            lane,
            environment.PostgreSql);

        var extension = environment.PostgreSql.Extensions.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, TrigramExtensionName, StringComparison.Ordinal));
        var index = environment.PostgreSql.Indexes.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, TrigramIndexName, StringComparison.Ordinal));

        if (extension is null ||
            !string.Equals(extension.Version, TrigramExtensionVersion, StringComparison.Ordinal) ||
            index is null ||
            !string.Equals(index.AccessMethod, "gin", StringComparison.Ordinal) ||
            index.Columns is null ||
            !index.Columns.SequenceEqual(ExpectedTrigramColumns, StringComparer.Ordinal) ||
            index.OperatorClasses is null ||
            !index.OperatorClasses.SequenceEqual(ExpectedTrigramOperatorClasses, StringComparer.Ordinal) ||
            index.IsValid is not true ||
            index.IsReady is not true ||
            index.IsLive is not true ||
            index.SizeBytes <= 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence requires the verified pg_trgm 1.6 four-column ready, " +
                "valid, live GIN index catalog metadata.");
        }

        var spillCount = verification.Measurements.Samples.Count(sample => sample.Spilled);
        var planSwitchCount = verification.Measurements.Commands.Sum(command =>
            command.Samples
                .Select(sample => sample.StructuralPlanSha256)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Count());
        var anomalyCount = verification.Measurements.Anomalies.Count;
        var credentialFindingCount = verification.CredentialScanPassed ? 0 : 1;

        if (spillCount != 0 || planSwitchCount != 0 || anomalyCount != 0 ||
            credentialFindingCount != 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence requires zero spills, plan switches, anomalies, and " +
                "credential findings.");
        }

        return new CaptureIdentityEvidence(
            environment.Git.Commit,
            environment.Git.Branch,
            environment.PostgreSql.ServerVersion,
            new TrigramIndexEvidence(
                extension.Name,
                extension.Version,
                index.Name,
                index.AccessMethod!,
                index.Columns,
                index.OperatorClasses,
                index.IsValid.Value,
                index.IsReady.Value,
                index.IsLive.Value,
                index.SizeBytes),
            manifest.CommandCount,
            manifest.ParameterCount,
            manifest.PlanCount,
            manifest.WarmUpRunsPerCommand,
            manifest.MeasuredRunsPerCommand,
            spillCount,
            planSwitchCount,
            anomalyCount,
            credentialFindingCount,
            environment.PostgreSql.ServerVersionNumber);
    }

    internal static void ValidatePostgreSqlVersionForPermanentEvidence(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        PostgreSqlEnvironmentSnapshot postgreSql)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(postgreSql);

        ValidatePostgreSqlVersionIdentity(
            GetExpectations(generation, lane),
            postgreSql.ServerVersion,
            postgreSql.ServerVersionNumber,
            allowLegacyPostgreSql16WithoutVersionNumber: false);
    }

    private static void ValidatePersistedPostgreSqlVersion(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        CaptureIdentityEvidence captureIdentity)
    {
        PermanentBaselineExpectations expectations = GetExpectations(generation, lane);
        bool allowLegacyPostgreSql16WithoutVersionNumber =
            expectations.PostgreSqlMajorVersion == RequiredPostgreSqlMajorVersion &&
            string.IsNullOrWhiteSpace(captureIdentity.PostgreSqlVersionNumber);

        ValidatePostgreSqlVersionIdentity(
            expectations,
            captureIdentity.PostgreSqlVersion,
            captureIdentity.PostgreSqlVersionNumber,
            allowLegacyPostgreSql16WithoutVersionNumber);
    }

    private static void ValidatePostgreSqlVersionIdentity(
        PermanentBaselineExpectations expectations,
        string? serverVersion,
        string? serverVersionNumber,
        bool allowLegacyPostgreSql16WithoutVersionNumber)
    {
        (int Major, int Minor) expectedTextIdentity =
            ParsePostgreSqlServerVersionText(expectations.PostgreSqlVersion);
        (int Major, int Minor) actualTextIdentity =
            ParsePostgreSqlServerVersionText(serverVersion);

        if (actualTextIdentity != expectedTextIdentity ||
            (expectations.PostgreSqlMajorVersion == RequiredPostgreSqlMajorVersion &&
             !string.Equals(
                 serverVersion,
                 expectations.PostgreSqlVersion,
                 StringComparison.Ordinal)))
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence requires exact PostgreSQL " +
                $"{expectations.PostgreSqlVersion} version identity.");
        }

        if (string.IsNullOrWhiteSpace(serverVersionNumber))
        {
            if (allowLegacyPostgreSql16WithoutVersionNumber)
            {
                return;
            }

            throw new BaselinePlanValidationException(
                "Permanent evidence is missing PostgreSQL server_version_num metadata.");
        }

        if (!int.TryParse(
                serverVersionNumber,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int numericVersion) ||
            numericVersion != expectations.PostgreSqlVersionNumber)
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence requires PostgreSQL server_version_num " +
                $"{expectations.PostgreSqlVersionNumber}.");
        }

        int numericMajor = numericVersion / 10_000;
        int numericMinor = numericVersion % 10_000;

        if (actualTextIdentity.Major != numericMajor ||
            actualTextIdentity.Minor != numericMinor)
        {
            throw new BaselinePlanValidationException(
                "PostgreSQL server version text contradicts server_version_num metadata.");
        }
    }

    private static (int Major, int Minor) ParsePostgreSqlServerVersionText(
        string? serverVersion)
    {
        if (string.IsNullOrWhiteSpace(serverVersion) ||
            !string.Equals(serverVersion, serverVersion.Trim(), StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence has malformed PostgreSQL server version text.");
        }

        int decorationStart = serverVersion.IndexOf(' ');
        string canonicalVersion = decorationStart < 0
            ? serverVersion
            : serverVersion[..decorationStart];

        if (decorationStart >= 0)
        {
            string decoration = serverVersion[decorationStart..];

            if (decoration.Length < 4 ||
                !decoration.StartsWith(" (", StringComparison.Ordinal) ||
                decoration[^1] != ')' ||
                string.IsNullOrWhiteSpace(decoration[2..^1]))
            {
                throw new BaselinePlanValidationException(
                    "Permanent evidence has malformed PostgreSQL server version text.");
            }
        }

        string[] components = canonicalVersion.Split('.');

        if (components.Length != 2 ||
            !int.TryParse(
                components[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int major) ||
            !int.TryParse(
                components[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minor) ||
            major <= 0 ||
            minor < 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence has malformed PostgreSQL server version text.");
        }

        return (major, minor);
    }

    private static CuratedBaselineMeasurements BuildPermanentMeasurements(
        BaselineMeasurementsRaw measurements,
        EvidenceCore evidenceCore,
        ArtifactIntegrityEvidence artifactIntegrity,
        QueryReviewGenerationDefinition generation,
        EmbeddedComparisonEvidence? comparison)
    {
        var commands = measurements.Commands.Select(command =>
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
        var permanentEvidence = new PermanentEvidenceMetadata(
            SchemaVersion: generation.IsFrozenHistorical ? 1 : 2,
            evidenceCore.ProfileVerification,
            evidenceCore.SemanticResultIdentity,
            evidenceCore.LockedResults,
            evidenceCore.A1ApprovedException,
            evidenceCore.CaptureIdentity,
            artifactIntegrity,
            generation.PermanentExportContract,
            comparison);

        return new CuratedBaselineMeasurements(
            measurements.BaselineRunId,
            measurements.VerifiedAtUtc,
            measurements.CommandCount,
            measurements.SampleCount,
            commands,
            measurements.Sequences,
            measurements.Q1Gate,
            measurements.Anomalies,
            permanentEvidence);
    }

    private static async Task<EmbeddedComparisonEvidence> BuildEmbeddedComparisonEvidenceAsync(
        VerifiedSuccessorComparisonRun comparison,
        CancellationToken cancellationToken)
    {
        const string relativeRoot = "comparison/postgresql-16";
        string[] paths = Directory.EnumerateFiles(
                comparison.Verification.CuratedDirectory,
                "*",
                SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(
                comparison.Verification.CuratedDirectory,
                path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var artifacts = new List<ArtifactHashEvidence>(paths.Length);

        foreach (string path in paths)
        {
            artifacts.Add(new ArtifactHashEvidence(
                path,
                await ComputeSha256Async(
                    ResolveWithin(comparison.Verification.CuratedDirectory, path),
                    cancellationToken)));
        }

        return new EmbeddedComparisonEvidence(
            relativeRoot,
            comparison.PrimaryIdentity,
            comparison.ComparisonIdentity,
            artifacts.Count,
            artifacts);
    }

    private static string BuildPermanentSummary(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        BaselineMeasurementsRaw measurements,
        EvidenceCore evidenceCore,
        CrossMajorComparisonReport? comparisonReport)
    {
        if (!generation.IsFrozenHistorical)
        {
            return BuildSuccessorPermanentSummary(
                generation,
                lane,
                measurements,
                evidenceCore.ProfileVerification,
                evidenceCore.CaptureIdentity,
                comparisonReport);
        }
        var q1Count = measurements.Commands.Single(command =>
            string.Equals(command.CommandKey, "Q1-01-filtered-count", StringComparison.Ordinal));
        var q1FirstPage = measurements.Sequences.Single(sequence =>
            string.Equals(sequence.SequenceId, "Q1-first-page", StringComparison.Ordinal));
        var q1Locked = evidenceCore.LockedResults.Single(result =>
            string.Equals(result.ShapeId, "Q1", StringComparison.Ordinal));
        var a1 = evidenceCore.A1ApprovedException!;
        var identity = evidenceCore.CaptureIdentity;
        var builder = new StringBuilder();

        builder.AppendLine("# Authoritative permanent Chapter 10F baseline summary");
        builder.AppendLine();
        builder.AppendLine(
            "This is the concise permanent evidence exported from a separately retained and " +
            "verified temporary raw-run directory. Warm-up and nonmedian plans are not permanent evidence.");
        builder.AppendLine();
        builder.AppendLine($"Run: `{measurements.BaselineRunId}`");
        builder.AppendLine($"Benchmark commit: `{identity.GitCommit}`");
        builder.AppendLine($"Result hash: `{evidenceCore.SemanticResultIdentity.ActualResultSha256}` (PASS)");
        builder.AppendLine();
        builder.AppendLine("## Verified identity");
        builder.AppendLine();
        builder.AppendLine(
            $"- Profile: `{evidenceCore.ProfileVerification.ProfileIdentity}`; " +
            $"listings {evidenceCore.ProfileVerification.ListingCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"translations {evidenceCore.ProfileVerification.TranslationCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"invariants {evidenceCore.ProfileVerification.InvariantPassed}/" +
            $"{evidenceCore.ProfileVerification.InvariantTotal}.");
        builder.AppendLine(
            $"- PostgreSQL {identity.PostgreSqlVersion}; {identity.TrigramIndex.ExtensionName} " +
            $"{identity.TrigramIndex.ExtensionVersion}; `{identity.TrigramIndex.IndexName}` " +
            $"{identity.TrigramIndex.AccessMethod.ToUpperInvariant()}, valid/ready/live.");
        builder.AppendLine(
            $"- Capture: {identity.CommandCount} commands; {identity.TypedParameterCount} typed parameters; " +
            $"{identity.RawPlanCount} plans; {identity.WarmUpRounds} warm-up and " +
            $"{identity.MeasuredRounds} measured rounds.");
        builder.AppendLine(
            $"- Safety: spills {identity.SpillCount}; plan switches {identity.PlanSwitchCount}; " +
            $"anomalies {identity.AnomalyCount}; credential findings {identity.CredentialFindingCount}.");
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
        builder.AppendLine("## Q1 acceptance: PASS");
        builder.AppendLine();
        builder.AppendLine(
            $"- Count: {q1Count.ExecutionTimeMedian.Value.ToString(CultureInfo.InvariantCulture)} ms; " +
            $"shared buffers {q1Count.SharedAccessBlocksMedian.Value.ToString(CultureInfo.InvariantCulture)}.");
        builder.AppendLine(
            $"- First page: {q1FirstPage.ExecutionTimeMedian.Value.ToString(CultureInfo.InvariantCulture)} ms; " +
            $"shared buffers {q1FirstPage.SharedAccessBlocksMedian.Value.ToString(CultureInfo.InvariantCulture)}.");
        builder.AppendLine(
            $"- Total: expected {q1Locked.ExpectedTotalCount}, actual {q1Locked.ActualTotalCount}; " +
            $"ordered IDs: {(q1Locked.OrderedIdsComparisonPassed ? "PASS" : "FAIL")}.");
        builder.AppendLine("- Count and page plans use `IX_ListingTranslations_Q_Trigram` without the old translation search sequential scan.");
        builder.AppendLine();
        builder.AppendLine("## A1 approved exception: PASS");
        builder.AppendLine();
        AppendA1Summary(builder, a1.FirstPage);
        AppendA1Summary(builder, a1.Supplementary);
        builder.AppendLine(
            $"- Buffers equivalent: {FormatPass(a1.BuffersEquivalent)}; " +
            $"scan/join/index topology unchanged: {FormatPass(a1.ScanJoinIndexTopologyUnchanged)}; " +
            $"no new expensive node: {FormatPass(a1.NoNewExpensiveNode)}.");
        builder.AppendLine();
        builder.AppendLine("## Integrity model");
        builder.AppendLine();
        builder.AppendLine(
            "`baseline-measurements.json` carries canonical SHA-256 hashes for every SQL file, " +
            "every median plan, `environment.json`, and this summary. Its terminal trust anchor is " +
            "the committed Git blob/tree; it intentionally does not claim an impossible self-hash.");

        return builder.ToString();
    }

    internal static string BuildSuccessorPermanentSummary(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        BaselineMeasurementsRaw measurements,
        ProfileVerificationEvidence profileVerification,
        CaptureIdentityEvidence identity,
        CrossMajorComparisonReport? comparisonReport)
    {
        if (string.Equals(
                lane.Id,
                QueryReviewGenerations.PostgreSql184LaneId,
                StringComparison.Ordinal))
        {
            return BuildPostgreSql184CompatibilitySummary(
                generation,
                measurements,
                profileVerification,
                identity,
                comparisonReport);
        }

        if (!string.Equals(
                lane.Id,
                QueryReviewGenerations.PostgreSql16LaneId,
                StringComparison.Ordinal) ||
            comparisonReport is not null)
        {
            throw new BaselinePlanValidationException(
                "The authoritative PostgreSQL 16 summary received an invalid lane or comparison report.");
        }

        SuccessorPermanentExportContract contract = generation.PermanentExportContract!;
        var builder = new StringBuilder();
        builder.AppendLine("# Authoritative permanent four-root discovery baseline summary");
        builder.AppendLine();
        builder.AppendLine($"Generation/profile: `{contract.GenerationId}`.");
        builder.AppendLine($"Run: `{measurements.BaselineRunId}`.");
        builder.AppendLine($"Capture commit: `{identity.GitCommit}`.");
        builder.AppendLine(
            $"Disposition: Commercial `{contract.CommercialDisposition}`; Land " +
            $"`{contract.LandDisposition}`; migrations {contract.MigrationCount}.");
        builder.AppendLine(
            $"Profile: {profileVerification.InvariantPassed}/" +
            $"{profileVerification.InvariantTotal} invariants; " +
            $"shape identity `{contract.QueryShapeManifestSha256}`; result/order identity " +
            $"`{contract.ResultOrderIdentitySha256}`.");
        builder.AppendLine(
            $"Capture: {identity.CommandCount} commands; {identity.TypedParameterCount} typed " +
            $"parameters; {identity.RawPlanCount} plans; {identity.WarmUpRounds} warm-up and " +
            $"{identity.MeasuredRounds} measured rounds.");
        builder.AppendLine(
            $"Safety: spills {identity.SpillCount}; plan switches {identity.PlanSwitchCount}; " +
            $"anomalies {identity.AnomalyCount}; credential findings " +
            $"{identity.CredentialFindingCount}.");
        builder.AppendLine();
        builder.AppendLine(
            "All SQL, typed parameters, totals, selected IDs, and order identities match the " +
            "accepted successor manifest. The accepted NO_INDEX catalog and final migration " +
            "inventory were verified before publication.");
        return builder.ToString();
    }

    private static string BuildPostgreSql184CompatibilitySummary(
        QueryReviewGenerationDefinition generation,
        BaselineMeasurementsRaw measurements,
        ProfileVerificationEvidence profileVerification,
        CaptureIdentityEvidence identity,
        CrossMajorComparisonReport? comparisonReport)
    {
        if (comparisonReport is null)
        {
            throw new BaselinePlanValidationException(
                "PostgreSQL 18.4 permanent publication requires a complete cross-major report.");
        }

        CrossMajorCompatibilityReportBuilder.ValidateForPublication(comparisonReport);
        SuccessorPermanentExportContract contract = generation.PermanentExportContract!;
        var builder = new StringBuilder();
        builder.AppendLine("# PostgreSQL 18.4 Compatibility Evidence");
        builder.AppendLine();
        builder.AppendLine(
            "PostgreSQL 16 remains the authoritative Chapter 15 correctness lane, " +
            "historical-comparison lane, performance lane, and index-decision lane.");
        builder.AppendLine(
            "PostgreSQL 18.4 is a bounded compatibility/performance-observation lane.");
        builder.AppendLine(
            "Cross-major timing is observational and is not an SLA.");
        builder.AppendLine();
        builder.AppendLine($"Generation/profile: `{contract.GenerationId}`.");
        builder.AppendLine($"Run: `{measurements.BaselineRunId}`.");
        builder.AppendLine($"Capture commit: `{identity.GitCommit}`.");
        builder.AppendLine(
            $"Disposition: Commercial `{contract.CommercialDisposition}`; Land " +
            $"`{contract.LandDisposition}`; migrations {contract.MigrationCount}.");
        builder.AppendLine(
            $"Profile: {profileVerification.InvariantPassed}/" +
            $"{profileVerification.InvariantTotal} invariants; " +
            $"shape identity `{contract.QueryShapeManifestSha256}`; result/order identity " +
            $"`{contract.ResultOrderIdentitySha256}`.");
        builder.AppendLine(
            $"Capture: {identity.CommandCount} commands; {identity.TypedParameterCount} typed " +
            $"parameters; {identity.RawPlanCount} plans; {identity.WarmUpRounds} warm-up and " +
            $"{identity.MeasuredRounds} measured rounds.");
        builder.AppendLine(
            $"Safety: spills {identity.SpillCount}; plan switches {identity.PlanSwitchCount}; " +
            $"anomalies {identity.AnomalyCount}; credential findings " +
            $"{identity.CredentialFindingCount}.");
        builder.AppendLine();
        builder.AppendLine("## Cross-major review rules");
        builder.AppendLine();
        builder.AppendLine(
            "- Threshold A: PostgreSQL 18.4 is both more than 25% and more than 2 ms " +
            "slower than the contemporaneous PostgreSQL 16 measured median.");
        builder.AppendLine(
            "- Threshold B: PostgreSQL 18.4 uses more than 20% additional median " +
            "shared-access blocks.");
        builder.AppendLine(
            "- Medians use only the five measured samples; discarded warm-ups are excluded.");
        builder.AppendLine();
        builder.AppendLine("## Cross-major sequence comparison");
        builder.AppendLine();
        builder.AppendLine(
            "| Sequence | PG16 ms | PG18.4 ms | Delta ms | Delta % | PG16 blocks | " +
            "PG18.4 blocks | Block delta | Block % | A | B |");
        builder.AppendLine(
            "|---|---:|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|");

        foreach (CrossMajorSequenceComparison sequence in comparisonReport.Sequences)
        {
            builder.Append("| ").Append(sequence.SequenceId)
                .Append(" | ").Append(FormatDecimal(sequence.PostgreSql16ExecutionTimeMedianMilliseconds))
                .Append(" | ").Append(FormatDecimal(sequence.PostgreSql184ExecutionTimeMedianMilliseconds))
                .Append(" | ").Append(FormatSignedDecimal(sequence.ExecutionTimeDeltaMilliseconds))
                .Append(" | ").Append(FormatSignedDecimal(sequence.ExecutionTimeDeltaPercent)).Append('%')
                .Append(" | ").Append(sequence.PostgreSql16SharedAccessBlocksMedian)
                .Append(" | ").Append(sequence.PostgreSql184SharedAccessBlocksMedian)
                .Append(" | ").Append(FormatSignedLong(sequence.SharedAccessBlocksDelta))
                .Append(" | ").Append(FormatSignedDecimal(sequence.SharedAccessBlocksDeltaPercent)).Append('%')
                .Append(" | ").Append(sequence.ThresholdAExceeded ? "yes" : "no")
                .Append(" | ").Append(sequence.ThresholdBExceeded ? "yes" : "no")
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"- Threshold A exceedances: {comparisonReport.ThresholdAExceedanceCount}.");
        builder.AppendLine(
            $"- Threshold B exceedances: {comparisonReport.ThresholdBExceedanceCount}.");
        builder.AppendLine();
        builder.AppendLine("## Lane-qualified plan/topology review");
        builder.AppendLine();

        CrossMajorSequenceComparison[] topologyDifferences = comparisonReport.Sequences
            .Where(sequence =>
                sequence.PostgreSql16OnlyTopology.Count != 0 ||
                sequence.PostgreSql184OnlyTopology.Count != 0)
            .ToArray();

        if (topologyDifferences.Length == 0)
        {
            builder.AppendLine("- No lane-qualified median-plan topology differences.");
        }
        else
        {
            foreach (CrossMajorSequenceComparison sequence in topologyDifferences)
            {
                bool eliminatesListingsSequentialScan =
                    sequence.PostgreSql16OnlyTopology.Contains(
                        "scan:Seq Scan;relation:Listings;index:-",
                        StringComparer.Ordinal) &&
                    !sequence.PostgreSql184OnlyTopology.Any(value =>
                        string.Equals(
                            value,
                            "scan:Seq Scan;relation:Listings;index:-",
                            StringComparison.Ordinal));

                builder.Append("- ").Append(sequence.SequenceId)
                    .Append(": PostgreSQL 16-only [")
                    .Append(FormatTopology(sequence.PostgreSql16OnlyTopology))
                    .Append("]; PostgreSQL 18.4-only [")
                    .Append(FormatTopology(sequence.PostgreSql184OnlyTopology))
                    .AppendLine("].");

                if (eliminatesListingsSequentialScan)
                {
                    builder.Append("- ").Append(sequence.SequenceId)
                        .AppendLine(
                            ": PostgreSQL 18.4 eliminates the PostgreSQL 16 named " +
                            "`Listings` sequential scan.");
                }
            }
        }

        builder.AppendLine(
            $"- Required Q1 `{CrossMajorCompatibilityReportBuilder.TrigramIndexName}` " +
            $"behavior remains intact: {(comparisonReport.Q1TrigramIndexPreserved ? "PASS" : "FAIL")}.");
        builder.AppendLine();
        builder.AppendLine(
            "All SQL, typed parameters, totals, selected IDs, order identities, settings, " +
            "and sample protocol match the verified contemporaneous PostgreSQL 16 comparison.");

        return builder.ToString();
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatSignedDecimal(decimal value) =>
        value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);

    private static string FormatSignedLong(long value) =>
        value.ToString("+0;-0;0", CultureInfo.InvariantCulture);

    private static string FormatTopology(IReadOnlyList<string> topology) =>
        topology.Count == 0 ? "none" : string.Join(", ", topology);

    private static void AppendA1Summary(
        StringBuilder builder,
        A1SequenceExceptionEvidence sequence)
    {
        builder.Append("- ").Append(sequence.SequenceId)
            .Append(": corrected pre-index ")
            .Append(sequence.CorrectedPreIndexMilliseconds.ToString(CultureInfo.InvariantCulture))
            .Append(" ms; indexed ")
            .Append(sequence.IndexedRunMilliseconds.ToString(CultureInfo.InvariantCulture))
            .Append(" ms; difference ")
            .Append(sequence.DifferenceMilliseconds.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture))
            .Append(" ms (")
            .Append(sequence.RelativeDifferencePercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture))
            .Append("%); shared buffers ")
            .Append(sequence.ExpectedSharedAccessBlocks)
            .Append('/')
            .Append(sequence.ActualSharedAccessBlocks)
            .AppendLine(".");
    }

    private static async Task<ArtifactIntegrityEvidence> BuildArtifactIntegrityAsync(
        string stagingDirectory,
        IReadOnlyList<string> commandKeys,
        CancellationToken cancellationToken)
    {
        var roots = new[] { "environment.json", "baseline-summary.md" };
        var rootArtifacts = new List<ArtifactHashEvidence>(roots.Length);
        var sqlArtifacts = new List<ArtifactHashEvidence>(commandKeys.Count);
        var planArtifacts = new List<ArtifactHashEvidence>(commandKeys.Count);

        foreach (var path in roots)
        {
            rootArtifacts.Add(new ArtifactHashEvidence(
                path,
                await ComputeCanonicalTextSha256Async(
                    ResolveWithin(stagingDirectory, path),
                    cancellationToken)));
        }

        foreach (var commandKey in commandKeys)
        {
            var sqlPath = $"sql/{commandKey}.sql";
            var planPath = $"baseline-plans/{commandKey}.json";
            sqlArtifacts.Add(new ArtifactHashEvidence(
                sqlPath,
                await ComputeCanonicalTextSha256Async(
                    ResolveWithin(stagingDirectory, sqlPath),
                    cancellationToken)));
            planArtifacts.Add(new ArtifactHashEvidence(
                planPath,
                await ComputeCanonicalTextSha256Async(
                    ResolveWithin(stagingDirectory, planPath),
                    cancellationToken)));
        }

        return new ArtifactIntegrityEvidence(
            Algorithm: "SHA-256",
            Canonicalization: "UTF-8 text with CRLF and CR normalized to LF; no other transformation",
            ManifestPath: "baseline-measurements.json",
            ManifestTrustAnchor: "Committed Git blob and containing Git tree",
            rootArtifacts,
            sqlArtifacts,
            planArtifacts);
    }

    private static async Task ValidatePermanentEvidenceAsync(
        string directory,
        CuratedBaselineMeasurements expected,
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        PermanentBaselineExpectations expectations,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "baseline-measurements.json");
        var persisted = await ReadRequiredJsonAsync<CuratedBaselineMeasurements>(path, cancellationToken);
        var metadata = persisted.PermanentEvidence;

        bool historical = generation.IsFrozenHistorical;
        bool manifestValid = historical
            ? metadata?.SchemaVersion == 1 && metadata.SuccessorExportContract is null
            : metadata?.SchemaVersion == 2 &&
              metadata.SuccessorExportContract is not null;

        if (!manifestValid || metadata is null ||
            !metadata.ProfileVerification.Passed ||
            !metadata.SemanticResultIdentity.ComparisonPassed ||
            metadata.LockedResults.Count != expectations.ShapeCount ||
            metadata.LockedResults.Any(result => !result.Passed) ||
            (historical && metadata.A1ApprovedException?.Accepted != true) ||
            (!historical && metadata.A1ApprovedException is not null) ||
            metadata.CaptureIdentity.SpillCount != 0 ||
            metadata.CaptureIdentity.PlanSwitchCount != 0 ||
            metadata.CaptureIdentity.AnomalyCount != 0 ||
            metadata.CaptureIdentity.CredentialFindingCount != 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent measurements are missing required successful completeness metadata.");
        }

        ValidatePersistedPostgreSqlVersion(
            generation,
            lane,
            metadata.CaptureIdentity);

        if (!historical)
        {
            SuccessorPermanentExportManifest.ValidateContract(
                metadata.SuccessorExportContract!);

            ValidateSuccessorProfileEvidence(metadata.ProfileVerification);

            bool requiresComparison = expectations.PostgreSqlMajorVersion == 18;

            if (requiresComparison != (metadata.PostgreSql16Comparison is not null))
            {
                throw new BaselinePlanValidationException(
                    "Successor permanent comparison metadata does not match the selected lane.");
            }

            if (metadata.PostgreSql16Comparison is not null)
            {
                await ValidateEmbeddedComparisonAsync(
                    directory,
                    metadata.PostgreSql16Comparison,
                    cancellationToken);
            }
        }
        else if (metadata.PostgreSql16Comparison is not null)
        {
            throw new BaselinePlanValidationException(
                "Historical schema-1 evidence cannot contain successor comparison material.");
        }

        var integrity = metadata.ArtifactIntegrity;
        var allArtifacts = integrity.RootArtifacts
            .Concat(integrity.NormalizedSqlArtifacts)
            .Concat(integrity.MedianPlanArtifacts)
            .ToArray();

        if (!string.Equals(integrity.Algorithm, "SHA-256", StringComparison.Ordinal) ||
            !string.Equals(integrity.ManifestPath, "baseline-measurements.json", StringComparison.Ordinal) ||
            integrity.RootArtifacts.Count != 2 ||
            integrity.NormalizedSqlArtifacts.Count != expectations.CommandCount ||
            integrity.MedianPlanArtifacts.Count != expectations.CommandCount ||
            allArtifacts.Select(artifact => artifact.Path).Distinct(StringComparer.Ordinal).Count() !=
                allArtifacts.Length)
        {
            throw new BaselinePlanValidationException(
                "Permanent artifact integrity manifest is incomplete or duplicated.");
        }

        foreach (var artifact in allArtifacts)
        {
            var actualHash = await ComputeCanonicalTextSha256Async(
                ResolveWithin(directory, artifact.Path),
                cancellationToken);

            if (!string.Equals(actualHash, artifact.Sha256, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Permanent canonical artifact hash mismatch for '{artifact.Path}'.");
            }
        }

        foreach (var command in expected.Commands)
        {
            var planArtifact = integrity.MedianPlanArtifacts.Single(artifact =>
                string.Equals(
                    artifact.Path,
                    $"baseline-plans/{command.CommandKey}.json",
                    StringComparison.Ordinal));

            if (!string.Equals(
                    planArtifact.Sha256,
                    command.ExecutionMedianPlanSha256,
                    StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Median-plan selection hash drifted for '{command.CommandKey}'.");
            }
        }

        var summary = await File.ReadAllTextAsync(
            Path.Combine(directory, "baseline-summary.md"),
            cancellationToken);

        bool postgreSql184Compatibility =
            !historical &&
            string.Equals(
                lane.Id,
                QueryReviewGenerations.PostgreSql184LaneId,
                StringComparison.Ordinal);
        string expectedHeading = historical
            ? "# Authoritative permanent Chapter 10F baseline summary"
            : postgreSql184Compatibility
                ? "# PostgreSQL 18.4 Compatibility Evidence"
                : "# Authoritative permanent four-root discovery baseline summary";
        if (!summary.StartsWith(expectedHeading, StringComparison.Ordinal) ||
            summary.Contains("temporary baseline summary", StringComparison.OrdinalIgnoreCase) ||
            (postgreSql184Compatibility &&
             (!summary.Contains(
                  "PostgreSQL 16 remains the authoritative Chapter 15 correctness lane",
                  StringComparison.Ordinal) ||
              !summary.Contains(
                  "PostgreSQL 18.4 is a bounded compatibility/performance-observation lane",
                  StringComparison.Ordinal) ||
              !summary.Contains(
                  "Cross-major timing is observational and is not an SLA",
                  StringComparison.Ordinal) ||
              CrossMajorCompatibilityReportBuilder.ExpectedSequenceIds.Any(sequenceId =>
                  !summary.Contains($"| {sequenceId} |", StringComparison.Ordinal)))))
        {
            throw new BaselinePlanValidationException(
                "Permanent summary wording or cross-major compatibility reporting is incomplete.");
        }
    }

    private static void ValidateCommandKeys(
        IReadOnlyList<string> commandKeys,
        int expectedCommandCount)
    {
        if (commandKeys.Count != expectedCommandCount ||
            commandKeys.Distinct(StringComparer.Ordinal).Count() != expectedCommandCount)
        {
            throw new BaselinePlanValidationException(
                "Verified command keys are missing or duplicated.");
        }

        foreach (var commandKey in commandKeys)
        {
            if (string.IsNullOrWhiteSpace(commandKey) ||
                !string.Equals(Path.GetFileName(commandKey), commandKey, StringComparison.Ordinal) ||
                commandKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new BaselinePlanValidationException(
                    $"Command key '{commandKey}' cannot be exported safely.");
            }
        }
    }

    private static HashSet<string> BuildExpectedFileSet(IReadOnlyList<string> commandKeys)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "environment.json",
            "baseline-measurements.json",
            "baseline-summary.md"
        };

        foreach (var commandKey in commandKeys)
        {
            expected.Add($"sql/{commandKey}.sql");
            expected.Add($"baseline-plans/{commandKey}.json");
        }

        return expected;
    }

    internal static async Task ValidateCuratedExportInputAsync(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        string curatedDirectory,
        IReadOnlyList<string> commandKeys,
        CancellationToken cancellationToken = default)
    {
        HashSet<string> expectedCuratedFiles = BuildExpectedFileSet(commandKeys);
        expectedCuratedFiles.Add(ExperimentalEvidenceBundle.ManifestFileName);
        ValidateExactFileSet(curatedDirectory, expectedCuratedFiles);

        QueryReviewArtifactDescriptor descriptor =
            await QueryReviewArtifactRouter.InspectAsync(
                curatedDirectory,
                cancellationToken);

        if (descriptor.Kind != QueryReviewArtifactKind.ExperimentalBundle ||
            !ReferenceEquals(descriptor.Generation, generation) ||
            !string.Equals(descriptor.Lane.Id, lane.Id, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "The curated permanent-export input does not match its selected " +
                "generation or PostgreSQL lane.");
        }

        await ExperimentalEvidenceBundle.VerifyAsync(
            descriptor,
            cancellationToken);
    }

    private static void AddEmbeddedComparisonFiles(
        HashSet<string> expectedFiles,
        EmbeddedComparisonEvidence? comparison)
    {
        if (comparison is null)
        {
            return;
        }

        foreach (ArtifactHashEvidence artifact in comparison.Artifacts)
        {
            expectedFiles.Add($"{comparison.RelativeRoot}/{artifact.Path}");
        }
    }

    private static async Task ValidateEmbeddedComparisonAsync(
        string permanentDirectory,
        EmbeddedComparisonEvidence comparison,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(comparison.RelativeRoot, "comparison/postgresql-16", StringComparison.Ordinal) ||
            comparison.ArtifactCount <= 0 ||
            comparison.ArtifactCount != comparison.Artifacts.Count ||
            comparison.Artifacts.Select(artifact => artifact.Path)
                .Distinct(StringComparer.Ordinal).Count() != comparison.ArtifactCount ||
            !string.Equals(
                comparison.ComparisonIdentity.GenerationId,
                QueryReviewGenerations.FourRootDiscoveryId,
                StringComparison.Ordinal) ||
            !string.Equals(
                comparison.ComparisonIdentity.ProfileIdentity,
                QueryReviewGenerations.FourRootDiscoveryId,
                StringComparison.Ordinal) ||
            !string.Equals(
                comparison.ComparisonIdentity.LaneId,
                QueryReviewGenerations.PostgreSql16LaneId,
                StringComparison.Ordinal) ||
            !string.Equals(
                comparison.ComparisonIdentity.ProfileSha256,
                SuccessorPermanentExportManifest.AcceptedProfileSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                comparison.ComparisonIdentity.InvariantManifestSha256,
                SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                comparison.ComparisonIdentity.InvariantResultSha256,
                SuccessorPermanentExportManifest.AcceptedInvariantResultSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                comparison.ComparisonIdentity.QueryShapeManifestSha256,
                DiscoveryQueryShapeManifest.ExpectedSuccessorManifestSha256,
                StringComparison.Ordinal) ||
            comparison.ComparisonIdentity.CommandCount != 83 ||
            comparison.ComparisonIdentity.ParameterCount != 190 ||
            comparison.ComparisonIdentity.WarmUpRunsPerCommand != 1 ||
            comparison.ComparisonIdentity.MeasuredRunsPerCommand != 5 ||
            comparison.ComparisonIdentity.PlanCount != 498)
        {
            throw new BaselinePlanValidationException(
                "Embedded PostgreSQL 16 comparison identity or inventory is invalid.");
        }

        SuccessorComparisonRunVerifier.ValidateCompatible(
            comparison.PrimaryIdentity,
            comparison.ComparisonIdentity);

        string comparisonDirectory = ResolveWithin(permanentDirectory, comparison.RelativeRoot);

        foreach (ArtifactHashEvidence artifact in comparison.Artifacts)
        {
            string actual = await ComputeSha256Async(
                ResolveWithin(comparisonDirectory, artifact.Path),
                cancellationToken);

            if (!string.Equals(actual, artifact.Sha256, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Embedded PostgreSQL 16 comparison hash mismatch for '{artifact.Path}'.");
            }
        }

        QueryReviewArtifactDescriptor descriptor =
            await QueryReviewArtifactRouter.InspectAsync(comparisonDirectory, cancellationToken);
        OfflineEvidenceVerificationResult verification =
            await ExperimentalEvidenceBundle.VerifyAsync(descriptor, cancellationToken);

        if (verification.FileCount != comparison.ArtifactCount)
        {
            throw new BaselinePlanValidationException(
                "Embedded PostgreSQL 16 comparison file count is incomplete.");
        }
    }

    internal static void ValidateSuccessorProfileEvidence(
        ProfileVerificationEvidence profile)
    {
        if (!string.Equals(
                profile.ProfileSha256,
                SuccessorPermanentExportManifest.AcceptedProfileSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                profile.InvariantManifestSha256,
                SuccessorPermanentExportManifest.AcceptedInvariantManifestSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                profile.InvariantResultSha256,
                SuccessorPermanentExportManifest.AcceptedInvariantResultSha256,
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Successor schema-2 permanent evidence has missing or forged measured profile hashes.");
        }
    }

    private static async Task ValidateCuratedMeasurementsAsync(
        BaselineVerificationResult verification,
        CancellationToken cancellationToken)
    {
        var curated = await ReadRequiredJsonAsync<CuratedBaselineMeasurements>(
            Path.Combine(verification.CuratedDirectory, "baseline-measurements.json"),
            cancellationToken);
        var measurements = verification.Measurements;

        if (!string.Equals(curated.BaselineRunId, measurements.BaselineRunId, StringComparison.Ordinal) ||
            curated.CommandCount != measurements.CommandCount ||
            curated.SampleCount != measurements.SampleCount ||
            curated.Commands.Count != measurements.Commands.Count ||
            curated.Sequences.Count != measurements.Sequences.Count ||
            curated.Q1Gate.Passed != measurements.Q1Gate.Passed ||
            curated.Q1Gate.FilteredCountMedianMilliseconds !=
                measurements.Q1Gate.FilteredCountMedianMilliseconds ||
            curated.Q1Gate.FirstPageSequenceMedianMilliseconds !=
                measurements.Q1Gate.FirstPageSequenceMedianMilliseconds ||
            curated.Q1Gate.AnyWarmUpOrMeasuredSpill !=
                measurements.Q1Gate.AnyWarmUpOrMeasuredSpill ||
            !curated.Q1Gate.Reasons.SequenceEqual(
                measurements.Q1Gate.Reasons,
                StringComparer.Ordinal) ||
            curated.Anomalies.Count != measurements.Anomalies.Count)
        {
            throw new BaselinePlanValidationException(
                "Temporary curated measurements do not match the verified raw measurements.");
        }

        for (var index = 0; index < measurements.Commands.Count; index++)
        {
            var source = measurements.Commands[index];
            var candidate = curated.Commands[index];

            if (!string.Equals(candidate.CommandKey, source.CommandKey, StringComparison.Ordinal) ||
                candidate.ShapeId != source.ShapeId ||
                candidate.ShapeSequence != source.ShapeSequence ||
                candidate.CommandRole != source.CommandRole ||
                candidate.ExecutionTimeMedian != source.ExecutionTimeMedian ||
                candidate.ExecutionMedianPlanPath != source.ExecutionMedianPlanPath ||
                candidate.ExecutionMedianPlanSha256 != source.ExecutionMedianPlanSha256)
            {
                throw new BaselinePlanValidationException(
                    $"Curated measurement drift exists for '{source.CommandKey}'.");
            }
        }
    }

    private static async Task ValidateEnvironmentArtifactAsync(
        BaselineVerificationResult verification,
        CancellationToken cancellationToken)
    {
        var rawPath = Path.Combine(verification.RunDirectory, "environment-raw.json");
        var curatedPath = Path.Combine(verification.CuratedDirectory, "environment.json");
        var rawHash = await ComputeSha256Async(rawPath, cancellationToken);
        var curatedHash = await ComputeSha256Async(curatedPath, cancellationToken);

        if (!string.Equals(rawHash, curatedHash, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Curated environment hash does not match the verified raw environment artifact.");
        }
    }

    private static async Task ValidateMedianPlansAsync(
        BaselineVerificationResult verification,
        CancellationToken cancellationToken)
    {
        foreach (var command in verification.Measurements.Commands)
        {
            var rawPlanPath = ResolveWithin(
                verification.RunDirectory,
                NormalizeRelativePath(command.ExecutionMedianPlanPath));
            var curatedPlanPath = Path.Combine(
                verification.CuratedDirectory,
                "baseline-plans",
                $"{command.CommandKey}.json");
            var rawHash = await ComputeSha256Async(rawPlanPath, cancellationToken);
            var curatedHash = await ComputeSha256Async(curatedPlanPath, cancellationToken);

            if (!string.Equals(rawHash, command.ExecutionMedianPlanSha256, StringComparison.Ordinal) ||
                !string.Equals(curatedHash, command.ExecutionMedianPlanSha256, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Execution-median plan hash mismatch for '{command.CommandKey}'.");
            }
        }
    }

    private static void ValidateExactFileSet(
        string directory,
        IReadOnlySet<string> expectedFiles)
    {
        if (!Directory.Exists(directory))
        {
            throw new BaselinePlanValidationException(
                $"Required evidence directory '{directory}' does not exist.");
        }

        var actualFiles = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(directory, path)))
            .ToHashSet(StringComparer.Ordinal);

        if (!actualFiles.SetEquals(expectedFiles))
        {
            var missing = expectedFiles.Except(actualFiles, StringComparer.Ordinal);
            var extra = actualFiles.Except(expectedFiles, StringComparer.Ordinal);
            throw new BaselinePlanValidationException(
                "Evidence file set mismatch. Missing: " +
                $"{string.Join(", ", missing)}. Extra: {string.Join(", ", extra)}.");
        }
    }

    private static string ResolveWithin(string rootDirectory, string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new BaselinePlanValidationException(
                $"Evidence artifact path '{relativePath}' must be relative.");
        }

        var fullRoot = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var pathFromRoot = Path.GetRelativePath(fullRoot, fullPath);

        if (pathFromRoot == ".." ||
            pathFromRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                $"Evidence artifact path '{relativePath}' escapes its required directory.");
        }

        return fullPath;
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new BaselinePlanValidationException(
                $"Required evidence source artifact '{path}' is missing.");
        }

        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(
            stream,
            JsonArtifactOutput.SerializerOptions,
            cancellationToken);

        return value ?? throw new BaselinePlanValidationException(
            $"Required evidence source artifact '{path}' contains no JSON value.");
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task<string> ComputeCanonicalTextSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var canonical = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return ComputeSha256(canonical);
    }

    private static string ComputeOrderedIdsSha256(IReadOnlyList<Guid> ids)
    {
        return ComputeSha256(string.Join("\n", ids.Select(id => id.ToString("D"))));
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> values)
    {
        return values.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SetEquals(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        return expected.ToHashSet(StringComparer.Ordinal)
            .SetEquals(actual);
    }

    private static string FormatPass(bool passed)
    {
        return passed ? "PASS" : "FAIL";
    }

    private static void ScanForCredentials(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var contents = File.ReadAllText(file);

            if (ConnectionAssignmentPattern().IsMatch(contents) ||
                JsonCredentialPropertyPattern().IsMatch(contents) ||
                SensitiveAssignmentPattern().IsMatch(contents) ||
                LocalUserPathPattern().IsMatch(contents))
            {
                throw new BaselinePlanValidationException(
                    $"Credential scan rejected permanent evidence file '{file}'.");
            }
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }

    [GeneratedRegex(
        @"\b(?:Host|Server|Username|User\s+ID|Password|Pwd)\s*=",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionAssignmentPattern();

    [GeneratedRegex(
        "\"(?:Host|Username|Password|ConnectionString|ClientSecret|AccessToken|RefreshToken)\"\\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonCredentialPropertyPattern();

    [GeneratedRegex(
        @"\b(?:Password|Pwd|Secret|Credential|Api[_-]?Key|Access[_-]?Token|Refresh[_-]?Token)\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex(
        @"(?:[A-Za-z]:\\Users\\|/home/)[^\s\""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalUserPathPattern();

    private sealed record EvidenceCore(
        ProfileVerificationEvidence ProfileVerification,
        SemanticResultIdentityEvidence SemanticResultIdentity,
        IReadOnlyList<LockedResultComparisonEvidence> LockedResults,
        A1ApprovedExceptionEvidence? A1ApprovedException,
        CaptureIdentityEvidence CaptureIdentity);

    private sealed record PermanentBaselineExpectations(
        int ShapeCount,
        int CommandCount,
        int ParameterCount,
        int PlanCount,
        int InvariantCount,
        string ProfileIdentity,
        string ResultOrderIdentitySha256,
        int PostgreSqlMajorVersion,
        string PostgreSqlVersion,
        int PostgreSqlVersionNumber);

    private sealed record ExpectedA1Topology(
        IReadOnlyList<string> NodeTypes,
        IReadOnlyList<string> ScanTypes,
        IReadOnlyList<string> JoinTypes,
        IReadOnlyList<string> IndexNames);
}

internal sealed record BaselineEvidenceExportResult(
    string DestinationDirectory,
    int FileCount,
    IReadOnlyDictionary<string, string> Sha256ByRelativePath);
