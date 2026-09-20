using System.Diagnostics;
using FluentAssertions;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class PermanentEvidencePublisherTests
{
    [Theory]
    [InlineData(QueryReviewGenerations.PostgreSql16LaneId)]
    [InlineData(QueryReviewGenerations.PostgreSql184LaneId)]
    public async Task Publication_ReplacesOnlySelectedLane_AndPreservesSiblingAndHistorical(
        string selectedLaneId)
    {
        string root = CreateTemporaryRoot();

        try
        {
            QueryReviewLaneDefinition lane =
                QueryReviewGenerations.FourRootDiscovery.RequireLane(selectedLaneId);
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane);
            await SeedProtectedTreesAsync(route);
            DirectoryTreeInventory siblingBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SiblingDestinations.Single());
            DirectoryTreeInventory historicalBefore = await DirectoryTreeInventory.CaptureAsync(
                route.HistoricalDestination);
            Path.GetRelativePath(route.SuccessorRoot, route.HistoricalDestination)
                .Should().StartWith("..");
            var publicationEvents = new List<PermanentEvidencePublicationFailurePoint>();

            PermanentEvidencePublicationResult result =
                await PermanentEvidencePublisher.PublishIsolatedForTestsAsync(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane,
                    PrepareReplacementAsync,
                    ValidateReplacementAsync,
                    point =>
                    {
                        publicationEvents.Add(point);
                        return Task.CompletedTask;
                    });

            result.DestinationDirectory.Should().Be(route.SelectedDestination);
            result.State.Should().Be(PermanentEvidencePublicationState.Committed);
            File.Exists(Path.Combine(route.SelectedDestination, "replacement.txt"))
                .Should().BeTrue();
            File.Exists(Path.Combine(route.SelectedDestination, "original.txt"))
                .Should().BeFalse();
            (await DirectoryTreeInventory.CaptureAsync(route.SiblingDestinations.Single()))
                .Should().Be(siblingBefore);
            (await DirectoryTreeInventory.CaptureAsync(route.HistoricalDestination))
                .Should().Be(historicalBefore);
            FindPublicationTemporaryDirectories(route).Should().BeEmpty();
            publicationEvents.IndexOf(
                    PermanentEvidencePublicationFailurePoint.AfterPreCommitValidation)
                .Should().BeLessThan(publicationEvents.IndexOf(
                    PermanentEvidencePublicationFailurePoint.AfterPublicationCommitted));
            publicationEvents.IndexOf(
                    PermanentEvidencePublicationFailurePoint.AfterPublicationCommitted)
                .Should().BeLessThan(publicationEvents.IndexOf(
                    PermanentEvidencePublicationFailurePoint.DuringBackupCleanup));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InjectedFailureAfterReplacement_RestoresSelectedLaneExactly()
    {
        string root = CreateTemporaryRoot();

        try
        {
            QueryReviewLaneDefinition lane =
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql16LaneId);
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane);
            await SeedProtectedTreesAsync(route);
            DirectoryTreeInventory selectedBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SelectedDestination);
            DirectoryTreeInventory siblingBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SiblingDestinations.Single());
            DirectoryTreeInventory historicalBefore = await DirectoryTreeInventory.CaptureAsync(
                route.HistoricalDestination);

            Func<Task> act = () =>
                PermanentEvidencePublisher.PublishIsolatedForTestsAsync(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane,
                    PrepareReplacementAsync,
                    ValidateReplacementAsync,
                    point => throw new InjectedPublicationFailureException(point.ToString()));

            await act.Should().ThrowAsync<InjectedPublicationFailureException>()
                .WithMessage("AfterDestinationPublished");
            (await DirectoryTreeInventory.CaptureAsync(route.SelectedDestination))
                .Should().Be(selectedBefore);
            (await DirectoryTreeInventory.CaptureAsync(route.SiblingDestinations.Single()))
                .Should().Be(siblingBefore);
            (await DirectoryTreeInventory.CaptureAsync(route.HistoricalDestination))
                .Should().Be(historicalBefore);
            FindPublicationTemporaryDirectories(route).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FinalSelectedValidationFailureBeforeCommit_RestoresOriginalExactly()
    {
        await AssertPreCommitFailureRestoresAsync(
            (_, _) => throw new InjectedPublicationFailureException(
                "final selected validation"),
            failureInjector: null,
            expectedException: typeof(InjectedPublicationFailureException));
    }

    [Fact]
    public async Task FinalSelectedValidationCancellationBeforeCommit_RestoresOriginalExactly()
    {
        using var cancellation = new CancellationTokenSource();

        await AssertPreCommitFailureRestoresAsync(
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            failureInjector: null,
            expectedException: typeof(OperationCanceledException),
            cancellationToken: cancellation.Token);
    }

    [Fact]
    public async Task FinalGuardFailureBeforeCommit_RestoresOriginalExactly()
    {
        await AssertPreCommitFailureRestoresAsync(
            ValidateReplacementAsync,
            point => point == PermanentEvidencePublicationFailurePoint.FinalGuardValidation
                ? throw new InjectedPublicationFailureException("final guard validation")
                : Task.CompletedTask,
            typeof(InjectedPublicationFailureException));
    }

    [Fact]
    public async Task PartialBackupCleanupFailureAfterCommit_PreservesCommittedDestination()
    {
        string root = CreateTemporaryRoot();

        try
        {
            QueryReviewLaneDefinition lane =
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql16LaneId);
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane);
            await SeedProtectedTreesAsync(route);
            DirectoryTreeInventory siblingBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SiblingDestinations.Single());
            DirectoryTreeInventory historicalBefore = await DirectoryTreeInventory.CaptureAsync(
                route.HistoricalDestination);
            var expectedReplacement = new DirectoryTreeInventory(false, 0, 0, string.Empty);
            int cleanupMutations = 0;

            Func<Task> act = async () =>
            {
                try
                {
                    await PermanentEvidencePublisher.PublishIsolatedForTestsAsync(
                        root,
                        QueryReviewGenerations.FourRootDiscovery,
                        lane,
                        PrepareReplacementAsync,
                        async (destination, cancellationToken) =>
                        {
                            await ValidateReplacementAsync(destination, cancellationToken);
                            expectedReplacement = await DirectoryTreeInventory.CaptureAsync(
                                destination,
                                cancellationToken);
                        },
                        point =>
                        {
                            if (point ==
                                PermanentEvidencePublicationFailurePoint.DuringBackupCleanup)
                            {
                                cleanupMutations++;
                                throw new InjectedPublicationFailureException(
                                    "partial backup cleanup");
                            }

                            return Task.CompletedTask;
                        });
                }
                catch (PermanentEvidencePostCommitException)
                {
                    throw;
                }
            };

            PermanentEvidencePostCommitException exception =
                (await act.Should().ThrowAsync<PermanentEvidencePostCommitException>()).Which;
            exception.InnerException.Should().BeOfType<InjectedPublicationFailureException>();
            cleanupMutations.Should().Be(1);
            (await DirectoryTreeInventory.CaptureAsync(route.SelectedDestination))
                .Should().Be(expectedReplacement);
            File.Exists(Path.Combine(route.SelectedDestination, "replacement.txt"))
                .Should().BeTrue();
            File.Exists(Path.Combine(route.SelectedDestination, "original.txt"))
                .Should().BeFalse();
            (await DirectoryTreeInventory.CaptureAsync(route.SiblingDestinations.Single()))
                .Should().Be(siblingBefore);
            (await DirectoryTreeInventory.CaptureAsync(route.HistoricalDestination))
                .Should().Be(historicalBefore);
            string[] temporaryDirectories = FindPublicationTemporaryDirectories(route);
            temporaryDirectories.Where(path => Path.GetFileName(path).Contains("-backup-"))
                .Should().ContainSingle();
            string backup = temporaryDirectories.Single(
                path => Path.GetFileName(path).Contains("-backup-"));
            Directory.EnumerateFileSystemEntries(backup).Should().NotBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationAfterCommit_DoesNotRollbackCommittedDestination()
    {
        string root = CreateTemporaryRoot();

        try
        {
            QueryReviewLaneDefinition lane =
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql16LaneId);
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane);
            await SeedProtectedTreesAsync(route);

            Func<Task> act = () =>
                PermanentEvidencePublisher.PublishIsolatedForTestsAsync(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane,
                    PrepareReplacementAsync,
                    ValidateReplacementAsync,
                    point => point ==
                        PermanentEvidencePublicationFailurePoint.AfterPublicationCommitted
                        ? throw new OperationCanceledException("post-commit cancellation")
                        : Task.CompletedTask);

            PermanentEvidencePostCommitException exception =
                (await act.Should().ThrowAsync<PermanentEvidencePostCommitException>()).Which;
            exception.InnerException.Should().BeOfType<OperationCanceledException>();
            File.Exists(Path.Combine(route.SelectedDestination, "replacement.txt"))
                .Should().BeTrue();
            File.Exists(Path.Combine(route.SelectedDestination, "original.txt"))
                .Should().BeFalse();
            FindPublicationTemporaryDirectories(route)
                .Should().ContainSingle(path => Path.GetFileName(path).Contains("-backup-"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SiblingMutationDuringStaging_IsDetectedBeforeReplacement()
    {
        string root = CreateTemporaryRoot();

        try
        {
            QueryReviewLaneDefinition lane =
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql16LaneId);
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane);
            await SeedProtectedTreesAsync(route);
            DirectoryTreeInventory selectedBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SelectedDestination);

            Func<Task> act = () =>
                PermanentEvidencePublisher.PublishIsolatedForTestsAsync(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane,
                    async (staging, cancellationToken) =>
                    {
                        await PrepareReplacementAsync(staging, cancellationToken);
                        await File.AppendAllTextAsync(
                            Path.Combine(route.SiblingDestinations.Single(), "sibling.txt"),
                            "unexpected",
                            cancellationToken);
                    },
                    ValidateReplacementAsync);

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*Unselected successor lane*changed*");
            (await DirectoryTreeInventory.CaptureAsync(route.SelectedDestination))
                .Should().Be(selectedBefore);
            FindPublicationTemporaryDirectories(route).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    public void RelativeResolver_RejectsTraversalBeforeMutation(string relativePath)
    {
        string root = CreateTemporaryRoot();

        try
        {
            Action act = () =>
                PermanentEvidencePublicationRoutes.ResolveRelativeUnderRootForTests(
                    root,
                    relativePath);

            act.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*escapes its approved root*");
            Directory.EnumerateFileSystemEntries(root).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RelativeResolver_RejectsRootedPathBeforeMutation()
    {
        string root = CreateTemporaryRoot();
        string rootedPath = Path.Combine(Path.GetPathRoot(root)!, "queryreview-outside");

        try
        {
            Action act = () =>
                PermanentEvidencePublicationRoutes.ResolveRelativeUnderRootForTests(
                    root,
                    rootedPath);

            act.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*must be repository-relative*");
            Directory.EnumerateFileSystemEntries(root).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RouteValidation_RejectsCaseAliasAndDestinationCollisions()
    {
        string root = CreateTemporaryRoot();

        try
        {
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    QueryReviewGenerations.FourRootDiscovery.RequireLane(
                        QueryReviewGenerations.PostgreSql16LaneId));
            PermanentEvidencePublicationRoute caseAlias = route with
            {
                SelectedDestination = route.SelectedDestination.ToUpperInvariant()
            };
            PermanentEvidencePublicationRoute siblingCollision = route with
            {
                SiblingDestinations = [route.SelectedDestination.ToUpperInvariant()]
            };
            PermanentEvidencePublicationRoute historicalCollision = route with
            {
                HistoricalDestination = route.SelectedDestination
            };

            Action caseAct = () =>
                PermanentEvidencePublicationRoutes.ValidateForTests(caseAlias);
            Action siblingAct = () =>
                PermanentEvidencePublicationRoutes.ValidateForTests(siblingCollision);
            Action historicalAct = () =>
                PermanentEvidencePublicationRoutes.ValidateForTests(historicalCollision);

            caseAct.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*exact case-preserving allowlisted path*");
            siblingAct.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*collide*");
            historicalAct.Should().Throw<BaselinePlanValidationException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MutationPathValidation_RejectsSelectedSiblingHistoricalAndArbitraryPaths()
    {
        string root = CreateTemporaryRoot();

        try
        {
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    QueryReviewGenerations.FourRootDiscovery.RequireLane(
                        QueryReviewGenerations.PostgreSql16LaneId));
            string validStaging = Path.Combine(
                route.SuccessorRoot,
                ".evidence-postgresql-16-staging-test");
            string validBackup = Path.Combine(
                route.SuccessorRoot,
                ".evidence-postgresql-16-backup-test");

            Action selected = () =>
                PermanentEvidencePublicationRoutes.ValidateMutationPathsForTests(
                    route,
                    route.SelectedDestination,
                    validBackup);
            Action sibling = () =>
                PermanentEvidencePublicationRoutes.ValidateMutationPathsForTests(
                    route,
                    validStaging,
                    route.SiblingDestinations.Single());
            Action historical = () =>
                PermanentEvidencePublicationRoutes.ValidateMutationPathsForTests(
                    route,
                    route.HistoricalDestination,
                    validBackup);
            Action arbitrary = () =>
                PermanentEvidencePublicationRoutes.ValidateMutationPathsForTests(
                    route,
                    Path.Combine(root, "arbitrary"),
                    validBackup);
            Action stagingBackupCollision = () =>
                PermanentEvidencePublicationRoutes.ValidateMutationPathsForTests(
                    route,
                    validStaging,
                    validStaging);

            selected.Should().Throw<BaselinePlanValidationException>();
            sibling.Should().Throw<BaselinePlanValidationException>();
            historical.Should().Throw<BaselinePlanValidationException>();
            arbitrary.Should().Throw<BaselinePlanValidationException>();
            stagingBackupCollision.Should().Throw<BaselinePlanValidationException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("docs/benchmarks/four-root-discovery-v1")]
    [InlineData("docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16")]
    [InlineData("docs/benchmarks/four-root-discovery-v1/evidence/postgresql-18.4")]
    [InlineData("docs/benchmarks/chapter-10f")]
    public void Windows_ProductionShapedRoute_RejectsReparsePointInProtectedAncestry(
        string relativeLinkPath)
    {
        string root = CreateTemporaryRoot();
        string target = Path.Combine(root, "reparse-target");
        string link = Path.Combine(
            root,
            relativeLinkPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        File.WriteAllText(Path.Combine(target, "sentinel.txt"), "outside-route-sentinel");

        try
        {
            CreateWindowsDirectoryLinkOrSkip(link, target);
            Action act = () =>
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    QueryReviewGenerations.FourRootDiscovery.RequireLane(
                        QueryReviewGenerations.PostgreSql16LaneId));

            act.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*reparse-point path component*");
            File.ReadAllText(Path.Combine(target, "sentinel.txt"))
                .Should().Be("outside-route-sentinel");
        }
        finally
        {
            DeleteDirectoryLinkIfPresent(link);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Windows_StagingAndBackupParentReparse_IsRejectedAtMutationBoundary()
    {
        string root = CreateTemporaryRoot();
        string target = Path.Combine(root, "mutation-reparse-target");
        Directory.CreateDirectory(target);
        PermanentEvidencePublicationRoute route =
            PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                root,
                QueryReviewGenerations.FourRootDiscovery,
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql16LaneId));
        Directory.CreateDirectory(route.SuccessorRoot);
        Directory.Delete(route.SuccessorRoot);
        string staging = Path.Combine(
            route.SuccessorRoot,
            ".evidence-postgresql-16-staging-reparse-test");
        string backup = Path.Combine(
            route.SuccessorRoot,
            ".evidence-postgresql-16-backup-reparse-test");

        try
        {
            CreateWindowsDirectoryLinkOrSkip(route.SuccessorRoot, target);
            Action act = () =>
                PermanentEvidencePublicationRoutes.ValidateExistingAncestryForTests(
                    route,
                    staging,
                    backup);

            act.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*reparse-point path component*");
            Directory.EnumerateFileSystemEntries(target).Should().BeEmpty();
        }
        finally
        {
            DeleteDirectoryLinkIfPresent(route.SuccessorRoot);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProductionShapedAncestryValidation_AcceptsNormalNonReparseTree()
    {
        string root = CreateTemporaryRoot();

        try
        {
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    QueryReviewGenerations.FourRootDiscovery.RequireLane(
                        QueryReviewGenerations.PostgreSql16LaneId));
            Directory.CreateDirectory(route.HistoricalDestination);
            Directory.CreateDirectory(route.SelectedDestination);
            Directory.CreateDirectory(route.SiblingDestinations.Single());
            string staging = Path.Combine(
                route.SuccessorRoot,
                ".evidence-postgresql-16-staging-normal-test");
            string backup = Path.Combine(
                route.SuccessorRoot,
                ".evidence-postgresql-16-backup-normal-test");

            Action act = () =>
                PermanentEvidencePublicationRoutes.ValidateExistingAncestryForTests(
                    route,
                    staging,
                    backup);

            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsolatedSeam_RejectsArbitraryTemporaryDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arbitrary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            Action act = () =>
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    QueryReviewGenerations.FourRootDiscovery.Lanes[0]);

            act.Should().Throw<BaselinePlanValidationException>()
                .WithMessage("*queryreview-publication-tests-*");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProductionPublisher_ReadinessAndCatalogIdentityCannotBeBypassed()
    {
        bool prepareCalled = false;
        QueryReviewLaneDefinition lane =
            QueryReviewGenerations.FourRootDiscovery.RequireLane(
                QueryReviewGenerations.PostgreSql16LaneId);

        Func<Task> notReady = () => PermanentEvidencePublisher.PublishAsync(
            QueryReviewGenerations.FourRootDiscovery,
            lane,
            (_, _) =>
            {
                prepareCalled = true;
                return Task.CompletedTask;
            },
            (_, _) => Task.CompletedTask);
        await notReady.Should().ThrowAsync<QueryReviewGenerationNotReadyException>()
            .WithMessage("*not finalized or provisioned yet*");
        prepareCalled.Should().BeFalse();

        QueryReviewGenerationDefinition forgedGeneration =
            QueryReviewGenerations.FourRootDiscovery with
            {
                PermanentExportFinalized = true
            };
        Func<Task> forged = () => PermanentEvidencePublisher.PublishAsync(
            forgedGeneration,
            lane,
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);
        await forged.Should().ThrowAsync<BaselinePlanValidationException>()
            .WithMessage("*exact catalog successor generation/lane pair*");
    }

    private static async Task AssertPreCommitFailureRestoresAsync(
        Func<string, CancellationToken, Task> validatePublished,
        Func<PermanentEvidencePublicationFailurePoint, Task>? failureInjector,
        Type expectedException,
        CancellationToken cancellationToken = default)
    {
        string root = CreateTemporaryRoot();

        try
        {
            QueryReviewLaneDefinition lane =
                QueryReviewGenerations.FourRootDiscovery.RequireLane(
                    QueryReviewGenerations.PostgreSql16LaneId);
            PermanentEvidencePublicationRoute route =
                PermanentEvidencePublicationRoutes.CreateIsolatedForTests(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane);
            await SeedProtectedTreesAsync(route);
            DirectoryTreeInventory selectedBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SelectedDestination);
            DirectoryTreeInventory siblingBefore = await DirectoryTreeInventory.CaptureAsync(
                route.SiblingDestinations.Single());
            DirectoryTreeInventory historicalBefore = await DirectoryTreeInventory.CaptureAsync(
                route.HistoricalDestination);

            Func<Task> act = () =>
                PermanentEvidencePublisher.PublishIsolatedForTestsAsync(
                    root,
                    QueryReviewGenerations.FourRootDiscovery,
                    lane,
                    PrepareReplacementAsync,
                    validatePublished,
                    failureInjector,
                    cancellationToken);

            Exception observed = expectedException == typeof(OperationCanceledException)
                ? (await act.Should().ThrowAsync<OperationCanceledException>()).Which
                : (await act.Should().ThrowAsync<InjectedPublicationFailureException>()).Which;
            observed.Should().BeOfType(expectedException);
            (await DirectoryTreeInventory.CaptureAsync(route.SelectedDestination))
                .Should().Be(selectedBefore);
            (await DirectoryTreeInventory.CaptureAsync(route.SiblingDestinations.Single()))
                .Should().Be(siblingBefore);
            (await DirectoryTreeInventory.CaptureAsync(route.HistoricalDestination))
                .Should().Be(historicalBefore);
            FindPublicationTemporaryDirectories(route).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task SeedProtectedTreesAsync(
        PermanentEvidencePublicationRoute route)
    {
        Directory.CreateDirectory(route.SelectedDestination);
        Directory.CreateDirectory(route.SiblingDestinations.Single());
        Directory.CreateDirectory(route.HistoricalDestination);
        await File.WriteAllTextAsync(
            Path.Combine(route.SelectedDestination, "original.txt"),
            "selected-original");
        await File.WriteAllTextAsync(
            Path.Combine(route.SelectedDestination, "original-second.txt"),
            "selected-original-second");
        await File.WriteAllTextAsync(
            Path.Combine(route.SiblingDestinations.Single(), "sibling.txt"),
            "sibling-original");
        await File.WriteAllTextAsync(
            Path.Combine(route.HistoricalDestination, "historical.txt"),
            "historical-original");
    }

    private static async Task PrepareReplacementAsync(
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(stagingDirectory, "nested"));
        await File.WriteAllTextAsync(
            Path.Combine(stagingDirectory, "replacement.txt"),
            "selected-replacement",
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(stagingDirectory, "nested", "proof.txt"),
            "nested-replacement",
            cancellationToken);
    }

    private static Task ValidateReplacementAsync(
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Exists(Path.Combine(destinationDirectory, "replacement.txt"))
            .Should().BeTrue();
        return Task.CompletedTask;
    }

    private static string[] FindPublicationTemporaryDirectories(
        PermanentEvidencePublicationRoute route) =>
        Directory.Exists(route.SuccessorRoot)
            ? Directory.GetDirectories(route.SuccessorRoot, ".evidence-*", SearchOption.TopDirectoryOnly)
            : [];

    private static string CreateTemporaryRoot()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"queryreview-publication-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CreateWindowsDirectoryLinkOrSkip(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Windows reparse-point behavior is required by this safety test.");
        }

        Exception? symbolicLinkFailure = null;

        try
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            PlatformNotSupportedException or System.Security.SecurityException or IOException)
        {
            symbolicLinkFailure = exception;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.StartInfo.ArgumentList.Add("/c");
            process.StartInfo.ArgumentList.Add("mklink");
            process.StartInfo.ArgumentList.Add("/J");
            process.StartInfo.ArgumentList.Add(link);
            process.StartInfo.ArgumentList.Add(target);
            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0 && Directory.Exists(link))
            {
                return;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            symbolicLinkFailure = exception;
        }

        throw Xunit.Sdk.SkipException.ForSkip(
            $"The current Windows runner cannot create a directory reparse point: " +
            symbolicLinkFailure?.GetType().Name);
    }

    private static void DeleteDirectoryLinkIfPresent(string link)
    {
        if (!Directory.Exists(link))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(link);

        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            throw new InvalidOperationException(
                $"Refusing test cleanup because '{link}' is not a reparse point.");
        }

        Directory.Delete(link, recursive: false);
    }

    private sealed class InjectedPublicationFailureException(string message)
        : Exception(message);
}
