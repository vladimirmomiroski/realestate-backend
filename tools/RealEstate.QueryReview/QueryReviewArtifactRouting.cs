using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealEstate.QueryReview;

internal sealed record QueryReviewArtifactDescriptor(
    QueryReviewGenerationDefinition Generation,
    QueryReviewLaneDefinition Lane,
    QueryReviewArtifactKind Kind,
    string Directory);

internal static class QueryReviewArtifactRouter
{
    public static async Task<QueryReviewArtifactDescriptor> InspectAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        string fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!Directory.Exists(fullDirectory))
        {
            throw new BaselinePlanValidationException(
                $"QueryReview artifact directory '{fullDirectory}' does not exist.");
        }

        QueryReviewArtifactDescriptor? permanent =
            TryResolvePermanentDestination(fullDirectory);

        if (permanent is not null)
        {
            await ValidateRecordedPermanentGenerationAsync(
                permanent,
                cancellationToken);
            return permanent;
        }

        string experimentalManifestPath = Path.Combine(
            fullDirectory,
            "experimental-manifest.json");

        if (File.Exists(experimentalManifestPath))
        {
            ExperimentalEvidenceManifest manifest =
                await ReadRequiredJsonAsync<ExperimentalEvidenceManifest>(
                    experimentalManifestPath,
                    cancellationToken);
            QueryReviewGenerationDefinition generation =
                QueryReviewGenerations.ResolveOrThrow(manifest.GenerationId);
            QueryReviewLaneDefinition lane = generation.RequireLane(
                manifest.LaneId);

            if (!generation.MatchesRecordedProfile(manifest.ProfileIdentity))
            {
                throw new BaselinePlanValidationException(
                    "Experimental manifest profile does not match its recorded generation.");
            }

            return new QueryReviewArtifactDescriptor(
                generation,
                lane,
                QueryReviewArtifactKind.ExperimentalBundle,
                fullDirectory);
        }

        string rawManifestPath = Path.Combine(fullDirectory, "manifest.json");

        if (File.Exists(rawManifestPath))
        {
            RawBaselineManifest manifest =
                await ReadRequiredJsonAsync<RawBaselineManifest>(
                    rawManifestPath,
                    cancellationToken);
            QueryReviewGenerationDefinition generation =
                ResolveRecordedGeneration(
                    manifest.GenerationId,
                    manifest.ProfileVersion);
            QueryReviewLaneDefinition lane = ResolveRecordedLane(
                generation,
                manifest.LaneId,
                manifest.PostgreSqlVersion);

            return new QueryReviewArtifactDescriptor(
                generation,
                lane,
                QueryReviewArtifactKind.RawRun,
                fullDirectory);
        }

        throw new BaselinePlanValidationException(
            $"Directory '{fullDirectory}' is not a recognized raw, experimental, or " +
            "fixed permanent QueryReview artifact.");
    }

    public static void ValidateRequestedGeneration(
        string? requestedProfile,
        QueryReviewArtifactDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(requestedProfile))
        {
            return;
        }

        QueryReviewGenerationDefinition requested =
            QueryReviewGenerations.ResolveOrThrow(requestedProfile);

        if (!ReferenceEquals(requested, descriptor.Generation))
        {
            throw new BaselinePlanValidationException(
                $"Requested generation '{requested.Id}' does not match recorded generation " +
                $"'{descriptor.Generation.Id}'.");
        }
    }

    private static QueryReviewArtifactDescriptor? TryResolvePermanentDestination(
        string fullDirectory)
    {
        foreach (QueryReviewGenerationDefinition generation in
                 QueryReviewGenerations.All)
        {
            foreach (QueryReviewLaneDefinition lane in generation.Lanes)
            {
                string expected = QueryReviewGenerations.GetPermanentEvidencePath(
                    generation,
                    lane);

                if (PathsEqual(fullDirectory, expected))
                {
                    return new QueryReviewArtifactDescriptor(
                        generation,
                        lane,
                        QueryReviewArtifactKind.PermanentEvidence,
                        fullDirectory);
                }
            }
        }

        return null;
    }

    private static async Task ValidateRecordedPermanentGenerationAsync(
        QueryReviewArtifactDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        string measurementsPath = Path.Combine(
            descriptor.Directory,
            "baseline-measurements.json");
        CuratedBaselineMeasurements measurements =
            await ReadRequiredJsonAsync<CuratedBaselineMeasurements>(
                measurementsPath,
                cancellationToken);
        string recordedProfile = measurements.PermanentEvidence?
            .ProfileVerification.ProfileIdentity
            ?? throw new BaselinePlanValidationException(
                "Permanent evidence is missing its recorded profile identity.");

        if (!descriptor.Generation.MatchesRecordedProfile(recordedProfile))
        {
            throw new BaselinePlanValidationException(
                $"Permanent destination generation '{descriptor.Generation.Id}' does not " +
                $"match recorded profile '{recordedProfile}'.");
        }
    }

    private static QueryReviewGenerationDefinition ResolveRecordedGeneration(
        string? generationId,
        string profileIdentity)
    {
        QueryReviewGenerationDefinition generation =
            string.IsNullOrWhiteSpace(generationId)
                ? QueryReviewGenerations.ResolveRecordedProfileOrThrow(profileIdentity)
                : QueryReviewGenerations.ResolveOrThrow(generationId);

        if (!generation.MatchesRecordedProfile(profileIdentity))
        {
            throw new BaselinePlanValidationException(
                $"Recorded generation '{generation.Id}' and profile '{profileIdentity}' " +
                "do not match.");
        }

        return generation;
    }

    private static QueryReviewLaneDefinition ResolveRecordedLane(
        QueryReviewGenerationDefinition generation,
        string? laneId,
        string postgreSqlVersion)
    {
        string majorText = postgreSqlVersion.Split('.', 2)[0];

        if (!int.TryParse(majorText, out int majorVersion))
        {
            throw new BaselinePlanValidationException(
                $"Recorded PostgreSQL version '{postgreSqlVersion}' is invalid.");
        }

        QueryReviewLaneDefinition lane = string.IsNullOrWhiteSpace(laneId)
            ? generation.RequireLaneForPostgreSqlMajor(majorVersion)
            : generation.RequireLane(laneId);

        if (lane.PostgreSqlMajorVersion != majorVersion)
        {
            throw new BaselinePlanValidationException(
                $"Recorded lane '{lane.Id}' requires PostgreSQL " +
                $"{lane.PostgreSqlMajorVersion}, but the artifact records " +
                $"'{postgreSqlVersion}'.");
        }

        return lane;
    }

    private static bool PathsEqual(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            comparison);
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new BaselinePlanValidationException(
                $"Required generation manifest '{path}' is missing.");
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(
                       stream,
                       JsonArtifactOutput.SerializerOptions,
                       cancellationToken)
                   ?? throw new BaselinePlanValidationException(
                       $"Generation manifest '{path}' deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new BaselinePlanValidationException(
                $"Generation manifest '{path}' is malformed: {exception.Message}");
        }
    }
}

internal static class ExperimentalEvidenceBundle
{
    public const string ArtifactClass = "queryreview-experimental";
    private const int ManifestSchemaVersion = 1;
    private const string ManifestFileName = "experimental-manifest.json";

    private static readonly Regex ConnectionAssignmentPattern = new(
        @"\b(?:Host|Server|Username|User\s+ID|Password|Pwd)\s*=",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JsonCredentialPropertyPattern = new(
        "\"(?:Host|Username|Password|ConnectionString|ClientSecret|AccessToken|RefreshToken)\"\\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveAssignmentPattern = new(
        @"\b(?:Password|Pwd|Secret|Credential|Api[_-]?Key|Access[_-]?Token|Refresh[_-]?Token)\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LocalUserPathPattern = new(
        "(?:[A-Za-z]:\\\\Users\\\\|/home/)[^\\s\\\"']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static async Task<ExperimentalEvidenceManifest> WriteManifestAsync(
        string directory,
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        string baselineRunId,
        string? recordedProfileIdentity = null,
        CancellationToken cancellationToken = default)
    {
        string fullDirectory = Path.GetFullPath(directory);
        string manifestPath = Path.Combine(fullDirectory, ManifestFileName);

        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }

        string[] files = Directory.EnumerateFiles(
                fullDirectory,
                "*",
                SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(
                Path.GetRelativePath(fullDirectory, path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        ValidateRelativePaths(files);
        var artifacts = new List<ArtifactHashEvidence>(files.Length);

        foreach (string relativePath in files)
        {
            artifacts.Add(new ArtifactHashEvidence(
                relativePath,
                await ComputeCanonicalTextSha256Async(
                    ResolveWithin(fullDirectory, relativePath),
                    cancellationToken)));
        }

        var manifest = new ExperimentalEvidenceManifest(
            ManifestSchemaVersion,
            ArtifactClass,
            generation.Id,
            recordedProfileIdentity ?? generation.ProfileIdentity,
            lane.Id,
            baselineRunId,
            artifacts.Count,
            artifacts);
        await JsonArtifactOutput.WriteAsync(
            manifestPath,
            manifest,
            cancellationToken);
        ScanForCredentials(fullDirectory);
        return manifest;
    }

    public static async Task<OfflineEvidenceVerificationResult> VerifyAsync(
        QueryReviewArtifactDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        if (descriptor.Kind != QueryReviewArtifactKind.ExperimentalBundle)
        {
            throw new BaselinePlanValidationException(
                "Experimental verifier received a non-experimental artifact.");
        }

        string manifestPath = Path.Combine(
            descriptor.Directory,
            ManifestFileName);
        await using FileStream stream = File.OpenRead(manifestPath);
        ExperimentalEvidenceManifest manifest =
            await JsonSerializer.DeserializeAsync<ExperimentalEvidenceManifest>(
                stream,
                JsonArtifactOutput.SerializerOptions,
                cancellationToken)
            ?? throw new BaselinePlanValidationException(
                "Experimental manifest deserialized to null.");

        if (manifest.SchemaVersion != ManifestSchemaVersion ||
            !string.Equals(
                manifest.ArtifactClass,
                ArtifactClass,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.GenerationId,
                descriptor.Generation.Id,
                StringComparison.Ordinal) ||
            !descriptor.Generation.MatchesRecordedProfile(
                manifest.ProfileIdentity) ||
            !string.Equals(
                manifest.LaneId,
                descriptor.Lane.Id,
                StringComparison.Ordinal) ||
            manifest.ArtifactCount != manifest.Artifacts.Count)
        {
            throw new BaselinePlanValidationException(
                "Experimental manifest identity, class, lane, or count is invalid.");
        }

        string[] recordedPaths = manifest.Artifacts
            .Select(artifact => artifact.Path)
            .ToArray();
        ValidateRelativePaths(recordedPaths);

        string[] actualPaths = Directory.EnumerateFiles(
                descriptor.Directory,
                "*",
                SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(
                Path.GetRelativePath(descriptor.Directory, path)))
            .Where(path => !string.Equals(
                path,
                ManifestFileName,
                StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (!recordedPaths.OrderBy(path => path, StringComparer.Ordinal)
                .SequenceEqual(actualPaths, StringComparer.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Experimental bundle file set does not match its manifest.");
        }

        foreach (ArtifactHashEvidence artifact in manifest.Artifacts)
        {
            string actual = await ComputeCanonicalTextSha256Async(
                ResolveWithin(descriptor.Directory, artifact.Path),
                cancellationToken);

            if (!string.Equals(actual, artifact.Sha256, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Experimental artifact hash mismatch for '{artifact.Path}'.");
            }
        }

        ScanForCredentials(descriptor.Directory);
        return new OfflineEvidenceVerificationResult(
            descriptor.Generation.Id,
            descriptor.Lane.Id,
            descriptor.Kind,
            descriptor.Directory,
            actualPaths.Length + 1);
    }

    private static void ValidateRelativePaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 ||
            paths.Distinct(StringComparer.Ordinal).Count() != paths.Count)
        {
            throw new BaselinePlanValidationException(
                "Experimental manifest paths are empty or duplicated.");
        }

        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                Path.IsPathFullyQualified(path) ||
                string.Equals(path, ManifestFileName, StringComparison.Ordinal))
            {
                throw new BaselinePlanValidationException(
                    $"Experimental artifact path '{path}' is invalid.");
            }

            _ = ResolveWithin(Path.GetTempPath(), path);
        }
    }

    private static string ResolveWithin(string root, string relativePath)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(fullRoot, fullPath);

        if (relative == ".." ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                $"Experimental artifact path '{relativePath}' escapes its bundle.");
        }

        return fullPath;
    }

    private static async Task<string> ComputeCanonicalTextSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        string text = await File.ReadAllTextAsync(path, cancellationToken);
        string canonical = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    private static void ScanForCredentials(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string contents = File.ReadAllText(file);

            if (ConnectionAssignmentPattern.IsMatch(contents) ||
                JsonCredentialPropertyPattern.IsMatch(contents) ||
                SensitiveAssignmentPattern.IsMatch(contents) ||
                LocalUserPathPattern.IsMatch(contents))
            {
                throw new BaselinePlanValidationException(
                    $"Credential scan rejected experimental evidence file '{file}'.");
            }
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }
}
