using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealEstate.QueryReview;

internal static partial class BaselineEvidenceWriter
{
    private const int ExpectedCommandCount = 33;
    private const int ExpectedParameterCount = 80;
    private const int ExpectedPlanCount = 198;
    private const int ExpectedInvariantCount = 61;
    private const long ExpectedListingCount = 100_000;
    private const long ExpectedTranslationCount = 200_000;
    private const int RequiredPostgreSqlMajorVersion = 16;
    private const string RequiredPostgreSqlVersion = "16.14";
    private const string ExpectedResultSha256 =
        "7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36";
    private const string TrigramExtensionName = "pg_trgm";
    private const string TrigramExtensionVersion = "1.6";
    private const string TrigramIndexName = "IX_ListingTranslations_Q_Trigram";
    private const decimal Q1CountMaximumMilliseconds = 211.486m;
    private const decimal Q1FirstPageMaximumMilliseconds = 227.000m;
    private const long Q1CountMaximumSharedAccessBlocks = 4_844;
    private const long Q1FirstPageMaximumSharedAccessBlocks = 5_160;
    private const string EvidenceRelativePath = "docs/benchmarks/chapter-10f/evidence";

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

    public static async Task<BaselineEvidenceExportResult> ExportAsync(
        BaselineVerificationResult verification,
        CancellationToken cancellationToken = default)
    {
        ValidateVerificationResult(verification);

        var repositoryRoot = FindRepositoryRoot()
            ?? throw new BaselinePlanValidationException(
                "Unable to locate the repository root for permanent evidence export.");
        var destinationDirectory = Path.GetFullPath(
            Path.Combine(repositoryRoot, EvidenceRelativePath));
        var expectedDestination = Path.GetFullPath(
            Path.Combine(repositoryRoot, "docs", "benchmarks", "chapter-10f", "evidence"));

        if (!string.Equals(destinationDirectory, expectedDestination, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence destination must be exactly '{expectedDestination}'.");
        }

        var manifest = await ReadRequiredJsonAsync<RawBaselineManifest>(
            Path.Combine(verification.RunDirectory, "manifest.json"),
            cancellationToken);
        var environment = await ReadRequiredJsonAsync<BaselineEnvironmentSnapshot>(
            Path.Combine(verification.RunDirectory, "environment-raw.json"),
            cancellationToken);
        var captureRun = await ReadRequiredJsonAsync<SqlCaptureRun>(
            ResolveWithin(verification.RunDirectory, manifest.CapturedCommandsPath),
            cancellationToken);

        ValidateIdentity(manifest, environment, verification.Measurements);
        var evidenceCore = BuildEvidenceCore(
            verification,
            manifest,
            environment,
            captureRun);

        var commandKeys = verification.Measurements.Commands
            .Select(command => command.CommandKey)
            .ToArray();
        ValidateCommandKeys(commandKeys);

        var expectedFiles = BuildExpectedFileSet(commandKeys);
        ValidateExactFileSet(verification.CuratedDirectory, expectedFiles);
        await ValidateCuratedMeasurementsAsync(verification, cancellationToken);
        await ValidateEnvironmentArtifactAsync(verification, cancellationToken);
        await ValidateMedianPlansAsync(verification, cancellationToken);
        ScanForCredentials(verification.CuratedDirectory);

        var destinationParent = Path.GetDirectoryName(destinationDirectory)
            ?? throw new BaselinePlanValidationException(
                "Permanent evidence destination has no parent directory.");
        Directory.CreateDirectory(destinationParent);

        var stagingDirectory = Path.Combine(
            destinationParent,
            $".evidence-export-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(
            destinationParent,
            $".evidence-backup-{Guid.NewGuid():N}");
        var publishedHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var backupCreated = false;
        var published = false;

        try
        {
            Directory.CreateDirectory(stagingDirectory);

            foreach (var relativePath in expectedFiles
                         .Where(path => path is not "baseline-measurements.json" and
                             not "baseline-summary.md")
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var sourcePath = ResolveWithin(verification.CuratedDirectory, relativePath);
                var stagingPath = ResolveWithin(stagingDirectory, relativePath);
                var stagingParent = Path.GetDirectoryName(stagingPath)!;
                Directory.CreateDirectory(stagingParent);

                var sourceHash = await ComputeSha256Async(sourcePath, cancellationToken);
                File.Copy(sourcePath, stagingPath, overwrite: false);
                var stagingHash = await ComputeSha256Async(stagingPath, cancellationToken);

                if (!string.Equals(sourceHash, stagingHash, StringComparison.Ordinal))
                {
                    throw new BaselinePlanValidationException(
                        $"Exported hash mismatch for '{relativePath}'.");
                }

            }

            var summaryPath = Path.Combine(stagingDirectory, "baseline-summary.md");
            await File.WriteAllTextAsync(
                summaryPath,
                BuildPermanentSummary(verification.Measurements, evidenceCore),
                cancellationToken);

            var artifactIntegrity = await BuildArtifactIntegrityAsync(
                stagingDirectory,
                commandKeys,
                cancellationToken);
            var permanentMeasurements = BuildPermanentMeasurements(
                verification.Measurements,
                evidenceCore,
                artifactIntegrity);
            await JsonArtifactOutput.WriteAsync(
                Path.Combine(stagingDirectory, "baseline-measurements.json"),
                permanentMeasurements,
                cancellationToken);

            ValidateExactFileSet(stagingDirectory, expectedFiles);
            await ValidatePermanentEvidenceAsync(
                stagingDirectory,
                permanentMeasurements,
                cancellationToken);
            ScanForCredentials(stagingDirectory);

            foreach (var relativePath in expectedFiles.OrderBy(path => path, StringComparer.Ordinal))
            {
                publishedHashes.Add(
                    relativePath,
                    await ComputeSha256Async(
                        ResolveWithin(stagingDirectory, relativePath),
                        cancellationToken));
            }

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Move(destinationDirectory, backupDirectory);
                backupCreated = true;
            }

            Directory.Move(stagingDirectory, destinationDirectory);
            published = true;

            ValidateExactFileSet(destinationDirectory, expectedFiles);
            ScanForCredentials(destinationDirectory);

            foreach (var expected in publishedHashes)
            {
                var destinationPath = ResolveWithin(destinationDirectory, expected.Key);
                var destinationHash = await ComputeSha256Async(
                    destinationPath,
                    cancellationToken);

                if (!string.Equals(expected.Value, destinationHash, StringComparison.Ordinal))
                {
                    throw new BaselinePlanValidationException(
                        $"Permanent evidence hash mismatch for '{expected.Key}'.");
                }
            }

            if (backupCreated)
            {
                Directory.Delete(backupDirectory, recursive: true);
                backupCreated = false;
            }

            return new BaselineEvidenceExportResult(
                destinationDirectory,
                publishedHashes.Count,
                publishedHashes);
        }
        catch
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }

            if (backupCreated)
            {
                if (Directory.Exists(destinationDirectory))
                {
                    Directory.Delete(destinationDirectory, recursive: true);
                }

                Directory.Move(backupDirectory, destinationDirectory);
            }
            else if (published && Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }

            throw;
        }
    }

    private static void ValidateVerificationResult(BaselineVerificationResult verification)
    {
        var measurements = verification.Measurements;

        if (!verification.CredentialScanPassed ||
            !measurements.Q1Gate.Passed ||
            measurements.CommandCount != ExpectedCommandCount ||
            measurements.SampleCount != ExpectedPlanCount ||
            measurements.WarmUpSampleCount != ExpectedCommandCount ||
            measurements.MeasuredSampleCount != ExpectedCommandCount * 5 ||
            measurements.Commands.Count != ExpectedCommandCount ||
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
        BaselineMeasurementsRaw measurements)
    {
        if (!string.Equals(
                manifest.BaselineRunId,
                measurements.BaselineRunId,
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Verified measurements do not match the raw manifest run identity.");
        }

        if (manifest.CommandCount != ExpectedCommandCount ||
            manifest.ParameterCount != ExpectedParameterCount ||
            manifest.PlanCount != ExpectedPlanCount ||
            manifest.WarmUpRunsPerCommand != 1 ||
            manifest.MeasuredRunsPerCommand != 5 ||
            manifest.Samples.Count != ExpectedPlanCount ||
            !manifest.CredentialScanPassed)
        {
            throw new BaselinePlanValidationException(
                "Raw manifest totals or credential status are not eligible for export.");
        }

        if (!string.Equals(manifest.ProfileVersion, DeterministicProfileSeeder.ProfileVersion,
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

        if (!int.TryParse(
                environment.PostgreSql.ServerVersionNumber,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var serverVersionNumber) ||
            serverVersionNumber / 10_000 != RequiredPostgreSqlMajorVersion)
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence requires PostgreSQL {RequiredPostgreSqlMajorVersion}.");
        }
    }

    private static EvidenceCore BuildEvidenceCore(
        BaselineVerificationResult verification,
        RawBaselineManifest manifest,
        BaselineEnvironmentSnapshot environment,
        SqlCaptureRun captureRun)
    {
        var profile = BuildProfileEvidence(manifest);
        var semanticIdentity = BuildSemanticIdentity(manifest, captureRun);
        var lockedResults = BuildLockedResultEvidence(captureRun);
        ValidateQ1Evidence(verification.Measurements);
        var a1Exception = BuildA1ExceptionEvidence(verification.Measurements);
        var captureIdentity = BuildCaptureIdentity(
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

    private static void ValidateQ1Evidence(BaselineMeasurementsRaw measurements)
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
            !page.IndexNames.Contains(TrigramIndexName, StringComparer.Ordinal) ||
            count.ScanTypes.Contains("Seq Scan", StringComparer.Ordinal) ||
            page.ScanTypes.Contains("Seq Scan", StringComparer.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence requires Q1 timing/buffer gates, trigram-index use in count " +
                "and page plans, and absence of the old translation sequential scan.");
        }
    }

    private static ProfileVerificationEvidence BuildProfileEvidence(
        RawBaselineManifest manifest)
    {
        var profile = manifest.ProfileVerification;

        if (profile is null ||
            !string.Equals(
                profile.ProfileIdentity,
                DeterministicProfileSeeder.ProfileVersion,
                StringComparison.Ordinal) ||
            profile.ListingCount != ExpectedListingCount ||
            profile.TranslationCount != ExpectedTranslationCount ||
            profile.InvariantTotal != ExpectedInvariantCount ||
            profile.InvariantPassed != ExpectedInvariantCount ||
            profile.InvariantFailed != 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent export requires the persisted successful chapter-10f-v1 " +
                "100,000-listing, 200,000-translation, 61/61 profile verification.");
        }

        return new ProfileVerificationEvidence(
            profile.ProfileIdentity,
            profile.ListingCount,
            profile.TranslationCount,
            profile.InvariantTotal,
            profile.InvariantPassed,
            profile.InvariantFailed,
            Passed: true);
    }

    private static SemanticResultIdentityEvidence BuildSemanticIdentity(
        RawBaselineManifest manifest,
        SqlCaptureRun captureRun)
    {
        var actualResultSha256 = ComputeSha256(JsonSerializer.Serialize(
            captureRun.ShapeResults,
            JsonArtifactOutput.SerializerOptions));
        var comparisonPassed = string.Equals(
                                   actualResultSha256,
                                   manifest.ResultSha256,
                                   StringComparison.Ordinal) &&
                               string.Equals(
                                   actualResultSha256,
                                   ExpectedResultSha256,
                                   StringComparison.Ordinal);

        if (!comparisonPassed)
        {
            throw new BaselinePlanValidationException(
                "Verified production semantic results do not match the locked Chapter 10F result hash.");
        }

        return new SemanticResultIdentityEvidence(
            ExpectedResultSha256,
            actualResultSha256,
            comparisonPassed);
    }

    private static IReadOnlyList<LockedResultComparisonEvidence> BuildLockedResultEvidence(
        SqlCaptureRun captureRun)
    {
        var expectations = QueryShapeDefinitions.GetLockedResultExpectations();

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
        BaselineVerificationResult verification,
        RawBaselineManifest manifest,
        BaselineEnvironmentSnapshot environment)
    {
        if (!string.Equals(
                environment.PostgreSql.ServerVersion,
                RequiredPostgreSqlVersion,
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence requires PostgreSQL {RequiredPostgreSqlVersion}.");
        }

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
            credentialFindingCount);
    }

    private static CuratedBaselineMeasurements BuildPermanentMeasurements(
        BaselineMeasurementsRaw measurements,
        EvidenceCore evidenceCore,
        ArtifactIntegrityEvidence artifactIntegrity)
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
            SchemaVersion: 1,
            evidenceCore.ProfileVerification,
            evidenceCore.SemanticResultIdentity,
            evidenceCore.LockedResults,
            evidenceCore.A1ApprovedException,
            evidenceCore.CaptureIdentity,
            artifactIntegrity);

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

    private static string BuildPermanentSummary(
        BaselineMeasurementsRaw measurements,
        EvidenceCore evidenceCore)
    {
        var q1Count = measurements.Commands.Single(command =>
            string.Equals(command.CommandKey, "Q1-01-filtered-count", StringComparison.Ordinal));
        var q1FirstPage = measurements.Sequences.Single(sequence =>
            string.Equals(sequence.SequenceId, "Q1-first-page", StringComparison.Ordinal));
        var q1Locked = evidenceCore.LockedResults.Single(result =>
            string.Equals(result.ShapeId, "Q1", StringComparison.Ordinal));
        var a1 = evidenceCore.A1ApprovedException;
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
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "baseline-measurements.json");
        var persisted = await ReadRequiredJsonAsync<CuratedBaselineMeasurements>(path, cancellationToken);
        var metadata = persisted.PermanentEvidence;

        if (metadata is null || metadata.SchemaVersion != 1 ||
            !metadata.ProfileVerification.Passed ||
            !metadata.SemanticResultIdentity.ComparisonPassed ||
            metadata.LockedResults.Count != QueryShapeDefinitions.GetLockedResultExpectations().Count ||
            metadata.LockedResults.Any(result => !result.Passed) ||
            !metadata.A1ApprovedException.Accepted ||
            metadata.CaptureIdentity.SpillCount != 0 ||
            metadata.CaptureIdentity.PlanSwitchCount != 0 ||
            metadata.CaptureIdentity.AnomalyCount != 0 ||
            metadata.CaptureIdentity.CredentialFindingCount != 0)
        {
            throw new BaselinePlanValidationException(
                "Permanent measurements are missing required successful completeness metadata.");
        }

        var integrity = metadata.ArtifactIntegrity;
        var allArtifacts = integrity.RootArtifacts
            .Concat(integrity.NormalizedSqlArtifacts)
            .Concat(integrity.MedianPlanArtifacts)
            .ToArray();

        if (!string.Equals(integrity.Algorithm, "SHA-256", StringComparison.Ordinal) ||
            !string.Equals(integrity.ManifestPath, "baseline-measurements.json", StringComparison.Ordinal) ||
            integrity.RootArtifacts.Count != 2 ||
            integrity.NormalizedSqlArtifacts.Count != ExpectedCommandCount ||
            integrity.MedianPlanArtifacts.Count != ExpectedCommandCount ||
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

        if (!summary.StartsWith(
                "# Authoritative permanent Chapter 10F baseline summary",
                StringComparison.Ordinal) ||
            summary.Contains("temporary baseline summary", StringComparison.OrdinalIgnoreCase))
        {
            throw new BaselinePlanValidationException(
                "Permanent summary wording is not authoritative or still claims to be temporary.");
        }
    }

    private static void ValidateCommandKeys(IReadOnlyList<string> commandKeys)
    {
        if (commandKeys.Count != ExpectedCommandCount ||
            commandKeys.Distinct(StringComparer.Ordinal).Count() != ExpectedCommandCount)
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

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));

            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
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
        A1ApprovedExceptionEvidence A1ApprovedException,
        CaptureIdentityEvidence CaptureIdentity);

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
