using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealEstate.QueryReview;

internal static partial class BaselineEvidenceWriter
{
    private const int ExpectedCommandCount = 33;
    private const int ExpectedParameterCount = 80;
    private const int ExpectedPlanCount = 198;
    private const int RequiredPostgreSqlMajorVersion = 16;
    private const string EvidenceRelativePath = "docs/benchmarks/chapter-10f/evidence";

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

        ValidateIdentity(manifest, environment, verification.Measurements);

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
        var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var backupCreated = false;
        var published = false;

        try
        {
            Directory.CreateDirectory(stagingDirectory);

            foreach (var relativePath in expectedFiles.OrderBy(path => path, StringComparer.Ordinal))
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

                sourceHashes.Add(relativePath, sourceHash);
            }

            ValidateExactFileSet(stagingDirectory, expectedFiles);
            ScanForCredentials(stagingDirectory);

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Move(destinationDirectory, backupDirectory);
                backupCreated = true;
            }

            Directory.Move(stagingDirectory, destinationDirectory);
            published = true;

            ValidateExactFileSet(destinationDirectory, expectedFiles);
            ScanForCredentials(destinationDirectory);

            foreach (var expected in sourceHashes)
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
                sourceHashes.Count,
                sourceHashes);
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

    private static void ScanForCredentials(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var contents = File.ReadAllText(file);

            if (ConnectionAssignmentPattern().IsMatch(contents) ||
                JsonCredentialPropertyPattern().IsMatch(contents) ||
                SensitiveAssignmentPattern().IsMatch(contents))
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
}

internal sealed record BaselineEvidenceExportResult(
    string DestinationDirectory,
    int FileCount,
    IReadOnlyDictionary<string, string> Sha256ByRelativePath);
