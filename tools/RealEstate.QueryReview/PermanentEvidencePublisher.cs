using System.Security.Cryptography;
using System.Text;

namespace RealEstate.QueryReview;

internal enum PermanentEvidencePublicationFailurePoint
{
    AfterDestinationPublished,
    FinalSelectedInventory,
    FinalGuardValidation,
    AfterPreCommitValidation,
    AfterPublicationCommitted,
    DuringBackupCleanup
}

internal enum PermanentEvidencePublicationState
{
    PreCommit,
    Committed
}

internal sealed record DirectoryTreeInventory(
    bool Exists,
    int DirectoryCount,
    int FileCount,
    string Sha256)
{
    private static readonly string AbsentSha256 = ComputeTextSha256("ABSENT\n");

    public static async Task<DirectoryTreeInventory> CaptureAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(directory);

        if (!Directory.Exists(root))
        {
            if (File.Exists(root))
            {
                throw new BaselinePlanValidationException(
                    $"Expected evidence directory '{root}', but a file occupies that path.");
            }

            return new DirectoryTreeInventory(false, 0, 0, AbsentSha256);
        }

        RejectReparsePoint(new DirectoryInfo(root));

        var entries = new List<string>();
        var caseInsensitivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int directoryCount = 0;
        int fileCount = 0;

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            string currentDirectory = pendingDirectories.Pop();

            foreach (string childDirectory in Directory.EnumerateDirectories(
                         currentDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                var info = new DirectoryInfo(childDirectory);
                RejectReparsePoint(info);
                string relative = NormalizeRelativePath(
                    Path.GetRelativePath(root, childDirectory));
                AddUniquePath(caseInsensitivePaths, relative);
                entries.Add($"D|{relative}");
                directoryCount++;
                pendingDirectories.Push(childDirectory);
            }

            foreach (string file in Directory.EnumerateFiles(
                         currentDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(file);
                RejectReparsePoint(info);
                string relative = NormalizeRelativePath(Path.GetRelativePath(root, file));
                AddUniquePath(caseInsensitivePaths, relative);
                string hash = await ComputeFileSha256Async(file, cancellationToken);
                entries.Add($"F|{relative}|{info.Length}|{hash}");
                fileCount++;
            }
        }

        entries.Sort(StringComparer.Ordinal);
        string inventory = string.Join('\n', entries) + "\n";
        return new DirectoryTreeInventory(
            true,
            directoryCount,
            fileCount,
            ComputeTextSha256(inventory));
    }

    private static void AddUniquePath(HashSet<string> paths, string relativePath)
    {
        if (!paths.Add(relativePath))
        {
            throw new BaselinePlanValidationException(
                $"Evidence tree contains a Windows case-alias collision at '{relativePath}'.");
        }
    }

    private static void RejectReparsePoint(FileSystemInfo info)
    {
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new BaselinePlanValidationException(
                $"Evidence tree contains unsupported reparse point '{info.FullName}'.");
        }
    }

    private static async Task<string> ComputeFileSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static string ComputeTextSha256(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/');
}

internal sealed record PermanentEvidencePublicationRoute(
    string Root,
    string HistoricalDestination,
    string SuccessorRoot,
    string SelectedDestination,
    IReadOnlyList<string> SiblingDestinations,
    QueryReviewGenerationDefinition Generation,
    QueryReviewLaneDefinition Lane);

internal sealed record PermanentEvidencePublicationResult(
    string DestinationDirectory,
    DirectoryTreeInventory SelectedBefore,
    DirectoryTreeInventory SelectedAfter,
    IReadOnlyDictionary<string, DirectoryTreeInventory> SiblingInventories,
    DirectoryTreeInventory HistoricalInventory,
    PermanentEvidencePublicationState State);

internal sealed class PermanentEvidencePostCommitException(
    string message,
    Exception innerException) : IOException(message, innerException);

internal static class PermanentEvidencePublicationRoutes
{
    public static PermanentEvidencePublicationRoute CreateProduction(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane)
    {
        RequireExactSuccessorCatalogPair(generation, lane);
        string repositoryRoot = QueryReviewGenerations.GetRepositoryRoot();
        return CreateUnderRoot(repositoryRoot, generation, lane);
    }

    internal static PermanentEvidencePublicationRoute CreateIsolatedForTests(
        string isolatedRoot,
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane)
    {
        RequireExactSuccessorCatalogPair(generation, lane);
        string root = Path.GetFullPath(isolatedRoot);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath());

        if (!IsStrictDescendant(temporaryRoot, root) ||
            !PathsEqual(Path.GetDirectoryName(root)!, temporaryRoot) ||
            !Path.GetFileName(root).StartsWith(
                "queryreview-publication-tests-",
                StringComparison.Ordinal) ||
            !Directory.Exists(root) ||
            (new DirectoryInfo(root).Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new BaselinePlanValidationException(
                "The isolated publication seam accepts only an existing, non-reparse, " +
                "dedicated queryreview-publication-tests-* direct child of the " +
                "operating-system temporary root.");
        }

        return CreateUnderRoot(root, generation, lane);
    }

    internal static string ResolveRelativeUnderRootForTests(
        string root,
        string relativePath) => ResolveRelativeUnderRoot(root, relativePath);

    internal static void ValidateForTests(PermanentEvidencePublicationRoute route) =>
        Validate(route);

    internal static void ValidateMutationPathsForTests(
        PermanentEvidencePublicationRoute route,
        string stagingDirectory,
        string backupDirectory) =>
        ValidateMutationPaths(route, stagingDirectory, backupDirectory);

    internal static void ValidateExistingAncestryForTests(
        PermanentEvidencePublicationRoute route,
        params string[] additionalDirectories) =>
        ValidateExistingAncestry(route, additionalDirectories);

    internal static void ValidateMutationPaths(
        PermanentEvidencePublicationRoute route,
        string stagingDirectory,
        string backupDirectory)
    {
        Validate(route);
        string staging = NormalizeFullPath(stagingDirectory);
        string backup = NormalizeFullPath(backupDirectory);

        if (!PathsEqual(Path.GetDirectoryName(staging)!, route.SuccessorRoot) ||
            !PathsEqual(Path.GetDirectoryName(backup)!, route.SuccessorRoot) ||
            !Path.GetFileName(staging).StartsWith(
                $".evidence-{route.Lane.Id}-staging-",
                StringComparison.Ordinal) ||
            !Path.GetFileName(backup).StartsWith(
                $".evidence-{route.Lane.Id}-backup-",
                StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Publication staging and backup paths must be controlled direct children of " +
                "the selected successor generation root.");
        }

        var protectedPaths = route.SiblingDestinations
            .Append(route.SelectedDestination)
            .Append(route.HistoricalDestination)
            .ToArray();

        if (PathsEqual(staging, backup) ||
            protectedPaths.Any(path => PathsEqual(staging, path) || PathsEqual(backup, path)))
        {
            throw new BaselinePlanValidationException(
                "Publication staging or backup path collides with a protected evidence path.");
        }
    }

    internal static void ValidateRollbackPath(
        PermanentEvidencePublicationRoute route,
        string rollbackDirectory)
    {
        Validate(route);
        string rollback = NormalizeFullPath(rollbackDirectory);

        if (!PathsEqual(Path.GetDirectoryName(rollback)!, route.SuccessorRoot) ||
            !Path.GetFileName(rollback).StartsWith(
                $".evidence-{route.Lane.Id}-rollback-",
                StringComparison.Ordinal) ||
            route.SiblingDestinations
                .Append(route.SelectedDestination)
                .Append(route.HistoricalDestination)
                .Any(path => PathsEqual(path, rollback)))
        {
            throw new BaselinePlanValidationException(
                "Publication rollback path must be a controlled direct child of the " +
                "selected successor generation root and cannot collide with evidence.");
        }
    }

    internal static void ValidateBackupPath(
        PermanentEvidencePublicationRoute route,
        string backupDirectory)
    {
        Validate(route);
        string backup = NormalizeFullPath(backupDirectory);

        if (!PathsEqual(Path.GetDirectoryName(backup)!, route.SuccessorRoot) ||
            !Path.GetFileName(backup).StartsWith(
                $".evidence-{route.Lane.Id}-backup-",
                StringComparison.Ordinal) ||
            route.SiblingDestinations
                .Append(route.SelectedDestination)
                .Append(route.HistoricalDestination)
                .Any(path => PathsEqual(path, backup)))
        {
            throw new BaselinePlanValidationException(
                "Publication backup path must be a controlled direct child of the " +
                "selected successor generation root and cannot collide with evidence.");
        }
    }

    private static PermanentEvidencePublicationRoute CreateUnderRoot(
        string root,
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane)
    {
        string fullRoot = NormalizeFullPath(root);
        string historical = ResolveRelativeUnderRoot(
            fullRoot,
            QueryReviewGenerations.FrozenHistorical.Lanes.Single()
                .PermanentEvidenceRelativePath!);
        string successorRoot = ResolveRelativeUnderRoot(
            fullRoot,
            QueryReviewGenerations.FourRootDiscovery.PermanentEvidenceRootRelativePath);
        string selected = ResolveRelativeUnderRoot(
            fullRoot,
            lane.PermanentEvidenceRelativePath!);
        string[] siblings = QueryReviewGenerations.FourRootDiscovery.Lanes
            .Where(candidate => !ReferenceEquals(candidate, lane))
            .Select(candidate => ResolveRelativeUnderRoot(
                fullRoot,
                candidate.PermanentEvidenceRelativePath!))
            .ToArray();

        var route = new PermanentEvidencePublicationRoute(
            fullRoot,
            historical,
            successorRoot,
            selected,
            siblings,
            generation,
            lane);
        Validate(route);
        ValidateExistingAncestry(route);
        return route;
    }

    private static void Validate(PermanentEvidencePublicationRoute route)
    {
        RequireExactSuccessorCatalogPair(route.Generation, route.Lane);
        string root = NormalizeFullPath(route.Root);
        string historical = NormalizeFullPath(route.HistoricalDestination);
        string successorRoot = NormalizeFullPath(route.SuccessorRoot);
        string selected = NormalizeFullPath(route.SelectedDestination);
        string expectedSelected = Path.Combine(successorRoot, route.Lane.Id);

        if (!string.Equals(selected, expectedSelected, StringComparison.Ordinal))
        {
            throw new BaselinePlanValidationException(
                "Selected lane destination is not the exact case-preserving allowlisted path.");
        }

        if (!IsStrictDescendant(root, historical) ||
            !IsStrictDescendant(root, successorRoot) ||
            !IsStrictDescendant(successorRoot, selected) ||
            IsSameOrDescendant(successorRoot, historical) ||
            IsSameOrDescendant(historical, successorRoot))
        {
            throw new BaselinePlanValidationException(
                "Historical and successor evidence roots are not safely isolated.");
        }

        var allDestinations = route.SiblingDestinations
            .Append(selected)
            .Append(historical)
            .Select(NormalizeFullPath)
            .ToArray();

        if (allDestinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            allDestinations.Length ||
            route.SiblingDestinations.Any(path =>
                !IsStrictDescendant(successorRoot, path) ||
                !string.Equals(
                    NormalizeFullPath(path),
                    Path.Combine(successorRoot, Path.GetFileName(path)),
                    StringComparison.Ordinal)))
        {
            throw new BaselinePlanValidationException(
                "Permanent evidence destinations collide or are not exact direct lane paths.");
        }
    }

    private static void RequireExactSuccessorCatalogPair(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane)
    {
        if (!ReferenceEquals(generation, QueryReviewGenerations.FourRootDiscovery) ||
            !generation.Lanes.Any(candidate => ReferenceEquals(candidate, lane)))
        {
            throw new BaselinePlanValidationException(
                "Permanent publication accepts only the exact catalog successor generation/lane pair.");
        }
    }

    internal static void ValidateExistingAncestry(
        PermanentEvidencePublicationRoute route,
        params string[] additionalDirectories)
    {
        Validate(route);
        var paths = route.SiblingDestinations
            .Append(route.HistoricalDestination)
            .Append(route.SuccessorRoot)
            .Append(route.SelectedDestination)
            .Concat(additionalDirectories)
            .Select(NormalizeFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string path in paths)
        {
            if (!IsSameOrDescendant(route.Root, path))
            {
                throw new BaselinePlanValidationException(
                    $"Evidence path '{path}' is outside the trusted publication root.");
            }

            RejectExistingReparseComponents(route.Root, path);
        }
    }

    private static void RejectExistingReparseComponents(string root, string target)
    {
        string trustedRoot = NormalizeFullPath(root);
        string fullTarget = NormalizeFullPath(target);
        string relative = Path.GetRelativePath(trustedRoot, fullTarget);
        string current = trustedRoot;

        if (!InspectExistingDirectoryComponent(current))
        {
            throw new BaselinePlanValidationException(
                $"Trusted publication root '{current}' does not exist.");
        }

        if (relative == ".")
        {
            return;
        }

        foreach (string component in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);

            if (!InspectExistingDirectoryComponent(current))
            {
                break;
            }
        }
    }

    private static bool InspectExistingDirectoryComponent(string path)
    {
        FileAttributes attributes;

        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or
            DirectoryNotFoundException)
        {
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new BaselinePlanValidationException(
                $"Evidence routing rejects reparse-point path component '{path}'.");
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new BaselinePlanValidationException(
                $"Evidence routing expected directory path component '{path}'.");
        }

        return true;
    }

    private static string ResolveRelativeUnderRoot(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathFullyQualified(relativePath) ||
            Path.IsPathRooted(relativePath))
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence path '{relativePath}' must be repository-relative.");
        }

        string fullRoot = NormalizeFullPath(root);
        string resolved = NormalizeFullPath(Path.Combine(
            fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)));

        if (!IsStrictDescendant(fullRoot, resolved))
        {
            throw new BaselinePlanValidationException(
                $"Permanent evidence path '{relativePath}' escapes its approved root.");
        }

        return resolved;
    }

    private static bool IsStrictDescendant(string root, string candidate) =>
        !PathsEqual(root, candidate) && IsSameOrDescendant(root, candidate);

    private static bool IsSameOrDescendant(string root, string candidate)
    {
        string relative = Path.GetRelativePath(
            NormalizeFullPath(root),
            NormalizeFullPath(candidate));
        return relative == "." ||
               (relative != ".." &&
                !relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) &&
                !Path.IsPathRooted(relative));
    }

    internal static bool PathsEqual(string left, string right) =>
        string.Equals(
            NormalizeFullPath(left),
            NormalizeFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
}

internal static class PermanentEvidencePublisher
{
    public static Task<PermanentEvidencePublicationResult> PublishAsync(
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        Func<string, CancellationToken, Task> prepareStaging,
        Func<string, CancellationToken, Task> validatePublished,
        CancellationToken cancellationToken = default)
    {
        generation.EnsureOfflineCommandAvailable(
            QueryReviewCommand.BaselineExport,
            QueryReviewArtifactKind.RawRun);
        PermanentEvidencePublicationRoute route =
            PermanentEvidencePublicationRoutes.CreateProduction(generation, lane);
        return PublishCoreAsync(
            route,
            prepareStaging,
            validatePublished,
            failureInjector: null,
            cancellationToken);
    }

    internal static Task<PermanentEvidencePublicationResult> PublishIsolatedForTestsAsync(
        string isolatedRoot,
        QueryReviewGenerationDefinition generation,
        QueryReviewLaneDefinition lane,
        Func<string, CancellationToken, Task> prepareStaging,
        Func<string, CancellationToken, Task> validatePublished,
        Func<PermanentEvidencePublicationFailurePoint, Task>? failureInjector = null,
        CancellationToken cancellationToken = default)
    {
        PermanentEvidencePublicationRoute route =
            PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                isolatedRoot,
                generation,
                lane);
        return PublishCoreAsync(
            route,
            prepareStaging,
            validatePublished,
            failureInjector,
            cancellationToken);
    }

    private static async Task<PermanentEvidencePublicationResult> PublishCoreAsync(
        PermanentEvidencePublicationRoute route,
        Func<string, CancellationToken, Task> prepareStaging,
        Func<string, CancellationToken, Task> validatePublished,
        Func<PermanentEvidencePublicationFailurePoint, Task>? failureInjector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepareStaging);
        ArgumentNullException.ThrowIfNull(validatePublished);
        PermanentEvidencePublicationRoutes.ValidateForTests(route);
        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route);

        DirectoryTreeInventory selectedBefore = await DirectoryTreeInventory.CaptureAsync(
            route.SelectedDestination,
            cancellationToken);
        IReadOnlyDictionary<string, DirectoryTreeInventory> siblingBefore =
            await CaptureGuardedTreesAsync(route.SiblingDestinations, cancellationToken);
        DirectoryTreeInventory historicalBefore = await DirectoryTreeInventory.CaptureAsync(
            route.HistoricalDestination,
            cancellationToken);

        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route);
        Directory.CreateDirectory(route.SuccessorRoot);
        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route);
        string nonce = Guid.NewGuid().ToString("N");
        string stagingDirectory = Path.Combine(
            route.SuccessorRoot,
            $".evidence-{route.Lane.Id}-staging-{nonce}");
        string backupDirectory = Path.Combine(
            route.SuccessorRoot,
            $".evidence-{route.Lane.Id}-backup-{nonce}");
        string rollbackDirectory = Path.Combine(
            route.SuccessorRoot,
            $".evidence-{route.Lane.Id}-rollback-{nonce}");
        PermanentEvidencePublicationRoutes.ValidateMutationPaths(
            route,
            stagingDirectory,
            backupDirectory);
        PermanentEvidencePublicationRoutes.ValidateRollbackPath(route, rollbackDirectory);

        bool backupCreated = false;
        bool replacementPublished = false;
        var state = PermanentEvidencePublicationState.PreCommit;
        DirectoryTreeInventory? selectedAfter = null;

        try
        {
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                stagingDirectory,
                backupDirectory,
                rollbackDirectory);
            Directory.CreateDirectory(stagingDirectory);
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                stagingDirectory,
                backupDirectory,
                rollbackDirectory);
            await prepareStaging(stagingDirectory, cancellationToken);
            _ = await DirectoryTreeInventory.CaptureAsync(
                stagingDirectory,
                cancellationToken);
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                stagingDirectory,
                backupDirectory,
                rollbackDirectory);
            await AssertGuardedTreesUnchangedAsync(
                route,
                siblingBefore,
                historicalBefore,
                cancellationToken);

            if (Directory.Exists(route.SelectedDestination))
            {
                PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                    route,
                    stagingDirectory,
                    backupDirectory,
                    rollbackDirectory);
                Directory.Move(route.SelectedDestination, backupDirectory);
                backupCreated = true;
            }

            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                stagingDirectory,
                backupDirectory,
                rollbackDirectory);
            Directory.Move(stagingDirectory, route.SelectedDestination);
            replacementPublished = true;
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                backupDirectory,
                rollbackDirectory);

            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.AfterDestinationPublished);

            await validatePublished(route.SelectedDestination, cancellationToken);
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                backupDirectory,
                rollbackDirectory);
            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.FinalSelectedInventory);
            selectedAfter = await DirectoryTreeInventory.CaptureAsync(
                route.SelectedDestination,
                cancellationToken);
            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.FinalGuardValidation);
            await AssertGuardedTreesUnchangedAsync(
                route,
                siblingBefore,
                historicalBefore,
                cancellationToken);
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                backupDirectory,
                rollbackDirectory);
            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.AfterPreCommitValidation);

            state = PermanentEvidencePublicationState.Committed;
            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.AfterPublicationCommitted);
            if (backupCreated)
            {
                await DeleteBackupAfterCommitAsync(
                    route,
                    backupDirectory,
                    failureInjector);
                backupCreated = false;
            }

            return new PermanentEvidencePublicationResult(
                route.SelectedDestination,
                selectedBefore,
                selectedAfter!,
                siblingBefore,
                historicalBefore,
                state);
        }
        catch (Exception exception)
        {
            if (state == PermanentEvidencePublicationState.Committed)
            {
                throw new PermanentEvidencePostCommitException(
                    "Permanent evidence publication committed successfully, but post-commit " +
                    "backup cleanup did not complete. The committed destination remains " +
                    "authoritative and no rollback was attempted.",
                    exception);
            }

            if (backupCreated)
            {
                if (Directory.Exists(route.SelectedDestination))
                {
                    PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                        route,
                        backupDirectory,
                        rollbackDirectory);
                    Directory.Move(route.SelectedDestination, rollbackDirectory);
                }

                PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                    route,
                    backupDirectory,
                    rollbackDirectory);
                Directory.Move(backupDirectory, route.SelectedDestination);
                backupCreated = false;
            }
            else if (replacementPublished && Directory.Exists(route.SelectedDestination))
            {
                PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                    route,
                    rollbackDirectory);
                Directory.Move(route.SelectedDestination, rollbackDirectory);
            }

            if (Directory.Exists(stagingDirectory))
            {
                DeleteControlledDirectory(route, stagingDirectory);
            }

            if (Directory.Exists(rollbackDirectory))
            {
                DeleteControlledDirectory(route, rollbackDirectory);
            }

            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route);
            await AssertGuardedTreesUnchangedAsync(
                route,
                siblingBefore,
                historicalBefore,
                CancellationToken.None);
            DirectoryTreeInventory selectedRestored = await DirectoryTreeInventory.CaptureAsync(
                route.SelectedDestination,
                CancellationToken.None);

            if (selectedRestored != selectedBefore)
            {
                throw new BaselinePlanValidationException(
                    "Failed permanent publication did not restore the selected lane exactly.");
            }

            throw;
        }
    }

    private static Task InvokeFailureInjectorAsync(
        Func<PermanentEvidencePublicationFailurePoint, Task>? failureInjector,
        PermanentEvidencePublicationFailurePoint point) =>
        failureInjector is null ? Task.CompletedTask : failureInjector(point);

    private static async Task DeleteBackupAfterCommitAsync(
        PermanentEvidencePublicationRoute route,
        string backupDirectory,
        Func<PermanentEvidencePublicationFailurePoint, Task>? failureInjector)
    {
        PermanentEvidencePublicationRoutes.ValidateBackupPath(route, backupDirectory);
        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
            route,
            backupDirectory);
        await DeleteBackupDirectoryContentsAsync(
            route,
            backupDirectory,
            failureInjector);
        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
            route,
            backupDirectory);
        Directory.Delete(backupDirectory, recursive: false);
    }

    private static async Task DeleteBackupDirectoryContentsAsync(
        PermanentEvidencePublicationRoute route,
        string directory,
        Func<PermanentEvidencePublicationFailurePoint, Task>? failureInjector)
    {
        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route, directory);

        foreach (string file in Directory.EnumerateFiles(directory)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route, directory);
            RejectReparsePointForMutation(file);
            File.Delete(file);
            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.DuringBackupCleanup);
        }

        foreach (string childDirectory in Directory.EnumerateDirectories(directory)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                childDirectory);
            await DeleteBackupDirectoryContentsAsync(
                route,
                childDirectory,
                failureInjector);
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                childDirectory);
            Directory.Delete(childDirectory, recursive: false);
            await InvokeFailureInjectorAsync(
                failureInjector,
                PermanentEvidencePublicationFailurePoint.DuringBackupCleanup);
        }
    }

    private static void RejectReparsePointForMutation(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new BaselinePlanValidationException(
                $"Refusing to mutate reparse-point evidence path '{path}'.");
        }
    }

    private static async Task<IReadOnlyDictionary<string, DirectoryTreeInventory>>
        CaptureGuardedTreesAsync(
            IEnumerable<string> paths,
            CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, DirectoryTreeInventory>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            result.Add(
                path,
                await DirectoryTreeInventory.CaptureAsync(path, cancellationToken));
        }

        return result;
    }

    private static async Task AssertGuardedTreesUnchangedAsync(
        PermanentEvidencePublicationRoute route,
        IReadOnlyDictionary<string, DirectoryTreeInventory> siblingBefore,
        DirectoryTreeInventory historicalBefore,
        CancellationToken cancellationToken)
    {
        foreach ((string path, DirectoryTreeInventory expected) in siblingBefore)
        {
            DirectoryTreeInventory actual = await DirectoryTreeInventory.CaptureAsync(
                path,
                cancellationToken);

            if (actual != expected)
            {
                throw new BaselinePlanValidationException(
                    $"Unselected successor lane '{path}' changed during publication.");
            }
        }

        DirectoryTreeInventory historicalAfter = await DirectoryTreeInventory.CaptureAsync(
            route.HistoricalDestination,
            cancellationToken);

        if (historicalAfter != historicalBefore)
        {
            throw new BaselinePlanValidationException(
                "Frozen historical evidence changed during successor publication.");
        }
    }

    private static void DeleteControlledDirectory(
        PermanentEvidencePublicationRoute route,
        string directory)
    {
        string fullPath = Path.GetFullPath(directory);
        bool isSelected = PermanentEvidencePublicationRoutes.PathsEqual(
            fullPath,
            route.SelectedDestination);
        bool isTemporary =
            PermanentEvidencePublicationRoutes.PathsEqual(
                Path.GetDirectoryName(fullPath)!,
                route.SuccessorRoot) &&
            (Path.GetFileName(fullPath).StartsWith(
                 $".evidence-{route.Lane.Id}-staging-",
                 StringComparison.Ordinal) ||
             Path.GetFileName(fullPath).StartsWith(
                 $".evidence-{route.Lane.Id}-backup-",
                 StringComparison.Ordinal) ||
             Path.GetFileName(fullPath).StartsWith(
                 $".evidence-{route.Lane.Id}-rollback-",
                 StringComparison.Ordinal));

        if ((!isSelected && !isTemporary) ||
            route.SiblingDestinations.Any(path =>
                PermanentEvidencePublicationRoutes.PathsEqual(path, fullPath)) ||
            PermanentEvidencePublicationRoutes.PathsEqual(
                route.HistoricalDestination,
                fullPath))
        {
            throw new BaselinePlanValidationException(
                $"Refusing to delete unselected or historical evidence path '{fullPath}'.");
        }

        DeleteControlledTreeWithoutFollowingReparsePoints(route, fullPath);
    }

    private static void DeleteControlledTreeWithoutFollowingReparsePoints(
        PermanentEvidencePublicationRoute route,
        string directory)
    {
        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route, directory);

        foreach (string file in Directory.EnumerateFiles(directory)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route, directory);
            RejectReparsePointForMutation(file);
            File.Delete(file);
        }

        foreach (string childDirectory in Directory.EnumerateDirectories(directory)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            PermanentEvidencePublicationRoutes.ValidateExistingAncestry(
                route,
                childDirectory);
            DeleteControlledTreeWithoutFollowingReparsePoints(route, childDirectory);
        }

        PermanentEvidencePublicationRoutes.ValidateExistingAncestry(route, directory);
        Directory.Delete(directory, recursive: false);
    }
}
