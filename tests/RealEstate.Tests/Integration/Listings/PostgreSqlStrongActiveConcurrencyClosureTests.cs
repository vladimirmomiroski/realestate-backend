using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlStrongActiveConcurrencyClosureTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string IntegrityViolationMessage =
        "Active listing publication integrity violation.";
    private const string TranslationFreezeMessage =
        "Active listing translations are immutable; unpublish before editing.";
    private const string LocationFreezeMessage =
        "Active listing location is immutable; unpublish before resolving.";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlStrongActiveConcurrencyClosureTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TranslationMutationFirst_ActivationUsesCommittedStrongTranslationTruth(
        bool leavesPublishable)
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "translation-first");
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var activation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction activationTransaction =
            await activation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int mutationPid = await ReadBackendPidAsync(mutation);
        int activationPid = await ReadBackendPidAsync(activation);
        bool mutationCommitted = false;
        bool activationCompleted = false;
        Task? activationTask = null;

        try
        {
            await SetTranslationMunicipalityAsync(
                mutation,
                mutationTransaction,
                seed.PrimaryTranslationId,
                leavesPublishable ? "Karpos" : null);
            ListingSnapshot mutationView = await ReadSnapshotAsync(
                mutation,
                mutationTransaction,
                seed.ListingId);
            mutationView.Xmin.Should().NotBe(initial.Xmin);
            mutationView.ModifiedAtUtc.Should().Be(initial.ModifiedAtUtc);

            activationTask = ActivateAsync(
                activation,
                activationTransaction,
                [seed.ListingId]);
            BlockingObservation blocking = await WaitForBlockedAsync(
                observer,
                activationPid,
                mutationPid,
                activationTask,
                "UPDATE public.\"Listings\"");
            AssertParentBlock(blocking, "transaction-first activation");

            await mutationTransaction.CommitAsync();
            mutationCommitted = true;

            if (leavesPublishable)
            {
                await activationTask.WaitAsync(TestTimeout);
                await activationTransaction.CommitAsync();
                activationCompleted = true;
            }
            else
            {
                PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                    async () => await activationTask.WaitAsync(TestTimeout));
                AssertTriggerFailure(exception, IntegrityViolationMessage);
                await activationTransaction.RollbackAsync();
                activationCompleted = true;
            }
        }
        finally
        {
            if (!mutationCommitted)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }

            if (activationTask is not null && !activationTask.IsCompleted)
            {
                await DrainSafelyAsync(activationTask);
            }

            if (!activationCompleted)
            {
                await RollbackSafelyAsync(activationTransaction);
            }
        }

        ListingSnapshot final = await ReadSnapshotAsync(
            observer,
            transaction: null,
            seed.ListingId);
        final.Status.Should().Be(leavesPublishable ? "Active" : "Draft");
        final.Translations.Should().BeEquivalentTo(
            [initial.Translations[0] with
            {
                Municipality = leavesPublishable ? "Karpos" : null
            }],
            options => options.WithStrictOrdering());
        final.Root.Should().Be(initial.Root);
        final.Xmin.Should().NotBe(initial.Xmin);
        final.ModifiedAtUtc.Should().Be(initial.ModifiedAtUtc);
    }

    [Fact]
    public async Task ActivationFirst_TranslationMutationBlocksThenActiveFreezeRejects()
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "activation-translation");
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var activation = await OpenConnectionAsync(connectionString);
        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction activationTransaction =
            await activation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int activationPid = await ReadBackendPidAsync(activation);
        int mutationPid = await ReadBackendPidAsync(mutation);
        bool activationCommitted = false;
        bool mutationRolledBack = false;
        Task? mutationTask = null;

        try
        {
            await ActivateAsync(
                activation,
                activationTransaction,
                [seed.ListingId]);

            mutationTask = SetTranslationMunicipalityAsync(
                mutation,
                mutationTransaction,
                seed.PrimaryTranslationId,
                "Karpos");
            BlockingObservation blocking = await WaitForBlockedAsync(
                observer,
                mutationPid,
                activationPid,
                mutationTask,
                "UPDATE public.\"ListingTranslations\"");
            AssertParentBlock(blocking, "activation-first translation mutation");

            await activationTransaction.CommitAsync();
            activationCommitted = true;

            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                async () => await mutationTask.WaitAsync(TestTimeout));
            AssertTriggerFailure(exception, TranslationFreezeMessage);
            await mutationTransaction.RollbackAsync();
            mutationRolledBack = true;
        }
        finally
        {
            if (!activationCommitted)
            {
                await RollbackSafelyAsync(activationTransaction);
            }

            if (mutationTask is not null && !mutationTask.IsCompleted)
            {
                await DrainSafelyAsync(mutationTask);
            }

            if (!mutationRolledBack)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }
        }

        ListingSnapshot final = await ReadSnapshotAsync(
            observer,
            transaction: null,
            seed.ListingId);
        final.Status.Should().Be("Active");
        final.Translations.Should().BeEquivalentTo(
            initial.Translations,
            options => options.WithStrictOrdering());
        final.Root.Should().Be(initial.Root);
        final.Xmin.Should().NotBe(initial.Xmin);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RootMutationFirst_ActivationUsesCommittedStrongRootTruth(
        bool leavesPublishable)
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "root-first");
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var activation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction activationTransaction =
            await activation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int mutationPid = await ReadBackendPidAsync(mutation);
        int activationPid = await ReadBackendPidAsync(activation);
        bool mutationCommitted = false;
        bool activationCompleted = false;
        Task? activationTask = null;

        try
        {
            await SetRootAsync(
                mutation,
                mutationTransaction,
                seed.ListingId,
                confirmed: leavesPublishable);

            activationTask = ActivateAsync(
                activation,
                activationTransaction,
                [seed.ListingId]);
            BlockingObservation blocking = await WaitForBlockedAsync(
                observer,
                activationPid,
                mutationPid,
                activationTask,
                "UPDATE public.\"Listings\"");
            AssertParentBlock(blocking, "root-first activation");

            await mutationTransaction.CommitAsync();
            mutationCommitted = true;

            if (leavesPublishable)
            {
                await activationTask.WaitAsync(TestTimeout);
                await activationTransaction.CommitAsync();
                activationCompleted = true;
            }
            else
            {
                PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                    async () => await activationTask.WaitAsync(TestTimeout));
                AssertTriggerFailure(exception, IntegrityViolationMessage);
                await activationTransaction.RollbackAsync();
                activationCompleted = true;
            }
        }
        finally
        {
            if (!mutationCommitted)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }

            if (activationTask is not null && !activationTask.IsCompleted)
            {
                await DrainSafelyAsync(activationTask);
            }

            if (!activationCompleted)
            {
                await RollbackSafelyAsync(activationTransaction);
            }
        }

        ListingSnapshot final = await ReadSnapshotAsync(
            observer,
            transaction: null,
            seed.ListingId);
        final.Status.Should().Be(leavesPublishable ? "Active" : "Draft");
        final.Translations.Should().BeEquivalentTo(
            initial.Translations,
            options => options.WithStrictOrdering());
        if (leavesPublishable)
        {
            AssertAlternateRoot(final.Root);
        }
        else
        {
            AssertUnresolvedRoot(final.Root);
        }
        final.Xmin.Should().NotBe(initial.Xmin);
        final.ModifiedAtUtc.Should().Be(initial.ModifiedAtUtc);
    }

    [Fact]
    public async Task ActivationFirst_RootMutationBlocksThenRootFreezeRejects()
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "activation-root");
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var activation = await OpenConnectionAsync(connectionString);
        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction activationTransaction =
            await activation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int activationPid = await ReadBackendPidAsync(activation);
        int mutationPid = await ReadBackendPidAsync(mutation);
        bool activationCommitted = false;
        bool mutationRolledBack = false;
        Task? mutationTask = null;

        try
        {
            await ActivateAsync(
                activation,
                activationTransaction,
                [seed.ListingId]);

            mutationTask = SetRootAsync(
                mutation,
                mutationTransaction,
                seed.ListingId,
                confirmed: true);
            BlockingObservation blocking = await WaitForBlockedAsync(
                observer,
                mutationPid,
                activationPid,
                mutationTask,
                "UPDATE public.\"Listings\"");
            AssertParentBlock(blocking, "activation-first root mutation");

            await activationTransaction.CommitAsync();
            activationCommitted = true;

            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                async () => await mutationTask.WaitAsync(TestTimeout));
            AssertTriggerFailure(exception, LocationFreezeMessage);
            await mutationTransaction.RollbackAsync();
            mutationRolledBack = true;
        }
        finally
        {
            if (!activationCommitted)
            {
                await RollbackSafelyAsync(activationTransaction);
            }

            if (mutationTask is not null && !mutationTask.IsCompleted)
            {
                await DrainSafelyAsync(mutationTask);
            }

            if (!mutationRolledBack)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }
        }

        ListingSnapshot final = await ReadSnapshotAsync(
            observer,
            transaction: null,
            seed.ListingId);
        final.Status.Should().Be("Active");
        final.Root.Should().Be(initial.Root);
        final.Translations.Should().BeEquivalentTo(
            initial.Translations,
            options => options.WithStrictOrdering());
        final.Xmin.Should().NotBe(initial.Xmin);
    }

    [Fact]
    public async Task RepeatableReadStaleActivation_AfterStrongTranslationMutation_AbortsOnParentMvccTouch()
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "stale-translation");
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var stale = await OpenConnectionAsync(connectionString);
        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction staleTransaction =
            await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        bool mutationCommitted = false;
        bool staleRolledBack = false;

        try
        {
            ListingSnapshot staleBefore = await ReadSnapshotAsync(
                stale,
                staleTransaction,
                seed.ListingId);
            staleBefore.Should().BeEquivalentTo(
                initial,
                options => options.WithStrictOrdering());

            await SetTranslationMunicipalityAsync(
                mutation,
                mutationTransaction,
                seed.PrimaryTranslationId,
                municipality: null);
            await mutationTransaction.CommitAsync();
            mutationCommitted = true;

            ListingSnapshot committed = await ReadSnapshotAsync(
                observer,
                transaction: null,
                seed.ListingId);
            committed.Status.Should().Be("Draft");
            committed.Translations[0].Municipality.Should().BeNull();
            committed.Xmin.Should().NotBe(initial.Xmin);
            committed.ModifiedAtUtc.Should().Be(initial.ModifiedAtUtc);

            ListingSnapshot staleAfter = await ReadSnapshotAsync(
                stale,
                staleTransaction,
                seed.ListingId);
            staleAfter.Should().BeEquivalentTo(
                initial,
                options => options.WithStrictOrdering());

            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                () => ActivateAsync(stale, staleTransaction, [seed.ListingId]));
            exception.SqlState.Should().Be(PostgresErrorCodes.SerializationFailure);
            await staleTransaction.RollbackAsync();
            staleRolledBack = true;

            ListingSnapshot final = await ReadSnapshotAsync(
                observer,
                transaction: null,
                seed.ListingId);
            final.Should().BeEquivalentTo(
                committed,
                options => options.WithStrictOrdering());
        }
        finally
        {
            if (!mutationCommitted)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }

            if (!staleRolledBack)
            {
                await RollbackSafelyAsync(staleTransaction);
            }
        }
    }

    [Fact]
    public async Task RepeatableReadStaleActivation_AfterRootMutation_AbortsOnRootMvccChange()
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "stale-root");
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var stale = await OpenConnectionAsync(connectionString);
        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction staleTransaction =
            await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        bool mutationCommitted = false;
        bool staleRolledBack = false;

        try
        {
            ListingSnapshot staleBefore = await ReadSnapshotAsync(
                stale,
                staleTransaction,
                seed.ListingId);
            staleBefore.Should().BeEquivalentTo(
                initial,
                options => options.WithStrictOrdering());

            await SetRootAsync(
                mutation,
                mutationTransaction,
                seed.ListingId,
                confirmed: false);
            await mutationTransaction.CommitAsync();
            mutationCommitted = true;

            ListingSnapshot committed = await ReadSnapshotAsync(
                observer,
                transaction: null,
                seed.ListingId);
            committed.Status.Should().Be("Draft");
            AssertUnresolvedRoot(committed.Root);
            committed.Xmin.Should().NotBe(initial.Xmin);

            ListingSnapshot staleAfter = await ReadSnapshotAsync(
                stale,
                staleTransaction,
                seed.ListingId);
            staleAfter.Should().BeEquivalentTo(
                initial,
                options => options.WithStrictOrdering());

            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                () => ActivateAsync(stale, staleTransaction, [seed.ListingId]));
            exception.SqlState.Should().Be(PostgresErrorCodes.SerializationFailure);
            await staleTransaction.RollbackAsync();
            staleRolledBack = true;

            ListingSnapshot final = await ReadSnapshotAsync(
                observer,
                transaction: null,
                seed.ListingId);
            final.Should().BeEquivalentTo(
                committed,
                options => options.WithStrictOrdering());
        }
        finally
        {
            if (!mutationCommitted)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }

            if (!staleRolledBack)
            {
                await RollbackSafelyAsync(staleTransaction);
            }
        }
    }

    [Fact]
    public async Task OppositeLogicalOrderMultiParentMutations_SerializeWithoutDeadlockAndTouchEveryParent()
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed first = await SeedReadyDraftAsync(
            setup,
            "multi-first",
            includeSecondaryTranslation: true);
        ListingSeed second = await SeedReadyDraftAsync(
            setup,
            "multi-second",
            includeSecondaryTranslation: true);
        Guid[] canonicalOrder = await ReadCanonicalListingOrderAsync(
            setup,
            [first.ListingId, second.ListingId]);
        ListingSeed lower = canonicalOrder[0] == first.ListingId ? first : second;
        ListingSeed higher = canonicalOrder[1] == first.ListingId ? first : second;
        ListingSnapshot lowerBefore = await ReadSnapshotAsync(
            setup,
            transaction: null,
            lower.ListingId);
        ListingSnapshot higherBefore = await ReadSnapshotAsync(
            setup,
            transaction: null,
            higher.ListingId);

        await using var gate = await OpenConnectionAsync(connectionString);
        await using var firstWriter = await OpenConnectionAsync(connectionString);
        await using var secondWriter = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction gateTransaction =
            await gate.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction firstTransaction =
            await firstWriter.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction secondTransaction =
            await secondWriter.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int gatePid = await ReadBackendPidAsync(gate);
        int firstPid = await ReadBackendPidAsync(firstWriter);
        int secondPid = await ReadBackendPidAsync(secondWriter);
        bool gateReleased = false;
        bool firstCommitted = false;
        bool secondCommitted = false;
        Task? firstTask = null;
        Task? secondTask = null;

        try
        {
            await LockListingForUpdateAsync(
                gate,
                gateTransaction,
                higher.ListingId);

            firstTask = UpdateTranslationsSetWiseAsync(
                firstWriter,
                firstTransaction,
                [lower.ListingId, higher.ListingId],
                languageCode: "en",
                title: "Writer A title");
            BlockingObservation firstBlocking = await WaitForBlockedAsync(
                observer,
                firstPid,
                gatePid,
                firstTask,
                "UPDATE public.\"ListingTranslations\"");
            AssertParentBlock(
                firstBlocking,
                "Writer A must hold the lower parent before waiting on the gated higher parent",
                expectedBlockerRelationLockMode: "RowShareLock");
            firstBlocking.BlockingPids.Should().Equal(gatePid);

            secondTask = UpdateTranslationsSetWiseAsync(
                secondWriter,
                secondTransaction,
                [higher.ListingId, lower.ListingId],
                languageCode: "mk",
                title: "Writer B title");
            BlockingObservation secondBlocking = await WaitForBlockedAsync(
                observer,
                secondPid,
                firstPid,
                secondTask,
                "UPDATE public.\"ListingTranslations\"");
            AssertParentBlock(
                secondBlocking,
                "Writer B must follow canonical order and wait on Writer A's lower parent",
                expectedBlockerRelationLockMode: "RowShareLock");
            secondBlocking.BlockingPids.Should().Equal(firstPid);
            secondBlocking.BlockingPids.Should().NotContain(gatePid);

            await gateTransaction.CommitAsync();
            gateReleased = true;
            await firstTask.WaitAsync(TestTimeout);

            ListingSnapshot lowerInsideA = await ReadSnapshotAsync(
                firstWriter,
                firstTransaction,
                lower.ListingId);
            ListingSnapshot higherInsideA = await ReadSnapshotAsync(
                firstWriter,
                firstTransaction,
                higher.ListingId);
            AssertFirstMultiParentWriter(lowerInsideA, lowerBefore);
            AssertFirstMultiParentWriter(higherInsideA, higherBefore);

            await firstTransaction.CommitAsync();
            firstCommitted = true;
            await secondTask.WaitAsync(TestTimeout);

            ListingSnapshot lowerAfterA = await ReadSnapshotAsync(
                observer,
                transaction: null,
                lower.ListingId);
            ListingSnapshot higherAfterA = await ReadSnapshotAsync(
                observer,
                transaction: null,
                higher.ListingId);
            lowerAfterA.Should().BeEquivalentTo(lowerInsideA);
            higherAfterA.Should().BeEquivalentTo(higherInsideA);

            await secondTransaction.CommitAsync();
            secondCommitted = true;

            ListingSnapshot lowerFinal = await ReadSnapshotAsync(
                observer,
                transaction: null,
                lower.ListingId);
            ListingSnapshot higherFinal = await ReadSnapshotAsync(
                observer,
                transaction: null,
                higher.ListingId);
            AssertMultiParentFinal(lowerFinal, lowerAfterA, lowerBefore);
            AssertMultiParentFinal(higherFinal, higherAfterA, higherBefore);
        }
        finally
        {
            if (!gateReleased)
            {
                await RollbackSafelyAsync(gateTransaction);
            }

            if (firstTask is not null && !firstTask.IsCompleted)
            {
                await DrainSafelyAsync(firstTask);
            }

            if (!firstCommitted)
            {
                await RollbackSafelyAsync(firstTransaction);
            }

            if (secondTask is not null && !secondTask.IsCompleted)
            {
                await DrainSafelyAsync(secondTask);
            }

            if (!secondCommitted)
            {
                await RollbackSafelyAsync(secondTransaction);
            }
        }
    }

    [Fact]
    public async Task UnpublishFirst_WaitingTranslationAndRootMutationBecomesPermittedThenRepublishes()
    {
        string connectionString = await GetPinnedConnectionStringAsync();
        await using var setup = await OpenConnectionAsync(connectionString);
        ListingSeed seed = await SeedReadyDraftAsync(setup, "unpublish-repair");
        await ActivateAutocommitAsync(setup, seed.ListingId);
        ListingSnapshot initial = await ReadSnapshotAsync(
            setup,
            transaction: null,
            seed.ListingId);

        await using var unpublish = await OpenConnectionAsync(connectionString);
        await using var mutation = await OpenConnectionAsync(connectionString);
        await using var observer = await OpenConnectionAsync(connectionString);
        await using NpgsqlTransaction unpublishTransaction =
            await unpublish.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using NpgsqlTransaction mutationTransaction =
            await mutation.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        int unpublishPid = await ReadBackendPidAsync(unpublish);
        int mutationPid = await ReadBackendPidAsync(mutation);
        bool unpublishCommitted = false;
        bool mutationCommitted = false;
        Task? mutationTask = null;

        try
        {
            await UnpublishAsync(
                unpublish,
                unpublishTransaction,
                seed.ListingId);

            mutationTask = MutateTranslationAndRootAsync(
                mutation,
                mutationTransaction,
                seed);
            BlockingObservation blocking = await WaitForBlockedAsync(
                observer,
                mutationPid,
                unpublishPid,
                mutationTask,
                "UPDATE public.\"ListingTranslations\"");
            AssertParentBlock(blocking, "unpublish-before-mutation");

            await unpublishTransaction.CommitAsync();
            unpublishCommitted = true;
            await mutationTask.WaitAsync(TestTimeout);
            await mutationTransaction.CommitAsync();
            mutationCommitted = true;
        }
        finally
        {
            if (!unpublishCommitted)
            {
                await RollbackSafelyAsync(unpublishTransaction);
            }

            if (mutationTask is not null && !mutationTask.IsCompleted)
            {
                await DrainSafelyAsync(mutationTask);
            }

            if (!mutationCommitted)
            {
                await RollbackSafelyAsync(mutationTransaction);
            }
        }

        ListingSnapshot repairedDraft = await ReadSnapshotAsync(
            observer,
            transaction: null,
            seed.ListingId);
        repairedDraft.Status.Should().Be("Draft");
        repairedDraft.Translations.Should().BeEquivalentTo(
            [initial.Translations[0] with { Municipality = "Gazi Baba" }],
            options => options.WithStrictOrdering());
        AssertAlternateRoot(repairedDraft.Root);

        await ActivateAutocommitAsync(observer, seed.ListingId);
        ListingSnapshot final = await ReadSnapshotAsync(
            observer,
            transaction: null,
            seed.ListingId);
        final.Status.Should().Be("Active");
        final.Translations.Should().BeEquivalentTo(
            repairedDraft.Translations,
            options => options.WithStrictOrdering());
        AssertAlternateRoot(final.Root);
        final.Xmin.Should().NotBe(initial.Xmin);
    }

    private async Task<string> GetPinnedConnectionStringAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        string connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The PostgreSQL connection string is unavailable.");

        return new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            ApplicationName = nameof(
                PostgreSqlStrongActiveConcurrencyClosureTests),
            CommandTimeout = (int)TestTimeout.TotalSeconds
        }.ConnectionString;
    }

    private static async Task<NpgsqlConnection> OpenConnectionAsync(
        string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<ListingSeed> SeedReadyDraftAsync(
        NpgsqlConnection connection,
        string suffix,
        bool includeSecondaryTranslation = false)
    {
        Guid listingId = Guid.NewGuid();
        Guid primaryTranslationId = Guid.NewGuid();
        Guid? secondaryTranslationId = includeSecondaryTranslation
            ? Guid.NewGuid()
            : null;

        await using (NpgsqlCommand listing = connection.CreateCommand())
        {
            listing.CommandText =
                """
                INSERT INTO public."Listings"
                    ("Id", "ListingType", "PropertyType", "Status", "Price",
                     "Currency", "AreaSquareMeters", "Latitude", "Longitude",
                     "LocationPrecision", "GeocodingProviderKey",
                     "GeocodingResultReference", "GeocodedDisplayName",
                     "LocationConfirmedAtUtc", "CreatedAtUtc")
                VALUES
                    (@listingId, 'Sale', 'Apartment', 'Draft', 100000,
                     'EUR', 80, 41.9981, 21.4254, 'ExactAddress',
                     @providerKey, @resultReference, @displayName,
                     TIMESTAMPTZ '2026-08-27 08:00:00+00', @createdAtUtc);
                """;
            listing.Parameters.AddWithValue("listingId", listingId);
            listing.Parameters.AddWithValue("providerKey", $"j6-{suffix}");
            listing.Parameters.AddWithValue(
                "resultReference",
                $"j6-reference-{suffix}");
            listing.Parameters.AddWithValue("displayName", $"J6 {suffix}");
            listing.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
            await listing.ExecuteNonQueryAsync();
        }

        await InsertTranslationAsync(
            connection,
            listingId,
            primaryTranslationId,
            "en",
            $"English {suffix}");

        if (secondaryTranslationId.HasValue)
        {
            await InsertTranslationAsync(
                connection,
                listingId,
                secondaryTranslationId.Value,
                "mk",
                $"Macedonian {suffix}");
        }

        return new ListingSeed(
            listingId,
            primaryTranslationId,
            secondaryTranslationId);
    }

    private static async Task InsertTranslationAsync(
        NpgsqlConnection connection,
        Guid listingId,
        Guid translationId,
        string languageCode,
        string title)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO public."ListingTranslations"
                ("Id", "ListingId", "LanguageCode", "Title", "City",
                 "Municipality", "AddressLine", "Description")
            VALUES
                (@translationId, @listingId, @languageCode, @title, 'Skopje',
                 'Centar', 'J6 address', 'J6 ready description');
            """;
        command.Parameters.AddWithValue("translationId", translationId);
        command.Parameters.AddWithValue("listingId", listingId);
        command.Parameters.AddWithValue("languageCode", languageCode);
        command.Parameters.AddWithValue("title", title);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid[]> ReadCanonicalListingOrderAsync(
        NpgsqlConnection connection,
        Guid[] listingIds)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT requested_id
            FROM unnest(@listingIds) AS requested(requested_id)
            ORDER BY requested_id;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>(
            "listingIds",
            listingIds));

        var ordered = new List<Guid>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ordered.Add(reader.GetGuid(0));
        }

        ordered.Should().HaveCount(2);
        ordered.Should().OnlyHaveUniqueItems();
        return ordered.ToArray();
    }

    private static async Task LockListingForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid listingId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT "Id"
            FROM public."Listings"
            WHERE "Id" = @listingId
            FOR UPDATE;
            """;
        command.Parameters.AddWithValue("listingId", listingId);
        Guid lockedId = (Guid)(await command.ExecuteScalarAsync())!;
        lockedId.Should().Be(listingId);
    }

    private static async Task ActivateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] listingIds)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE public."Listings"
            SET "Status" = 'Active'
            WHERE "Id" = ANY(@listingIds);
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>(
            "listingIds",
            listingIds));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ActivateAutocommitAsync(
        NpgsqlConnection connection,
        Guid listingId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE public."Listings"
            SET "Status" = 'Active'
            WHERE "Id" = @listingId;
            """;
        command.Parameters.AddWithValue("listingId", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UnpublishAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid listingId)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE public."Listings"
            SET "Status" = 'Draft'
            WHERE "Id" = @listingId;
            """;
        command.Parameters.AddWithValue("listingId", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetTranslationMunicipalityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid translationId,
        string? municipality)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE public."ListingTranslations"
            SET "Municipality" = @municipality
            WHERE "Id" = @translationId;
            """;
        command.Parameters.AddWithValue("translationId", translationId);
        command.Parameters.Add("municipality", NpgsqlDbType.Text).Value =
            municipality is null ? DBNull.Value : municipality;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetRootAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid listingId,
        bool confirmed)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = confirmed
            ?
            """
            UPDATE public."Listings"
            SET "Latitude" = 42.004,
                "Longitude" = 21.409,
                "LocationPrecision" = 'Street',
                "GeocodingProviderKey" = 'j6-alternate-provider',
                "GeocodingResultReference" = 'j6-alternate-reference',
                "GeocodedDisplayName" = 'J6 alternate display',
                "LocationConfirmedAtUtc" =
                    TIMESTAMPTZ '2026-08-27 09:00:00+00'
            WHERE "Id" = @listingId;
            """
            :
            """
            UPDATE public."Listings"
            SET "Latitude" = NULL,
                "Longitude" = NULL,
                "LocationPrecision" = NULL,
                "GeocodingProviderKey" = NULL,
                "GeocodingResultReference" = NULL,
                "GeocodedDisplayName" = NULL,
                "LocationConfirmedAtUtc" = NULL
            WHERE "Id" = @listingId;
            """;
        command.Parameters.AddWithValue("listingId", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task MutateTranslationAndRootAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ListingSeed seed)
    {
        await SetTranslationMunicipalityAsync(
            connection,
            transaction,
            seed.PrimaryTranslationId,
            "Gazi Baba");
        await SetRootAsync(
            connection,
            transaction,
            seed.ListingId,
            confirmed: true);
    }

    private static async Task UpdateTranslationsSetWiseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] listingIds,
        string languageCode,
        string title)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            WITH requested("ListingId", ordinal) AS
            (
                SELECT requested_id, ordinal
                FROM unnest(@listingIds) WITH ORDINALITY
                    AS requested(requested_id, ordinal)
            )
            UPDATE public."ListingTranslations" AS translation
            SET "Title" = @title
            FROM requested
            WHERE translation."ListingId" = requested."ListingId"
              AND translation."LanguageCode" = @languageCode;
            """;
        command.Parameters.Add(new NpgsqlParameter<Guid[]>(
            "listingIds",
            listingIds));
        command.Parameters.AddWithValue("languageCode", languageCode);
        command.Parameters.AddWithValue("title", title);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ListingSnapshot> ReadSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid listingId)
    {
        string status;
        RootSnapshot root;
        long xmin;
        DateTime? modifiedAtUtc;

        await using (NpgsqlCommand listing = connection.CreateCommand())
        {
            listing.Transaction = transaction;
            listing.CommandText =
                """
                SELECT "Status", "Latitude", "Longitude", "LocationPrecision",
                       "GeocodingProviderKey", "GeocodingResultReference",
                       "GeocodedDisplayName", "LocationConfirmedAtUtc",
                       xmin::text::bigint, "ModifiedAtUtc"
                FROM public."Listings"
                WHERE "Id" = @listingId;
                """;
            listing.Parameters.AddWithValue("listingId", listingId);
            await using NpgsqlDataReader reader = await listing.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            status = reader.GetString(0);
            root = new RootSnapshot(
                reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7));
            xmin = reader.GetInt64(8);
            modifiedAtUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9);
        }

        var translations = new List<TranslationSnapshot>();
        await using (NpgsqlCommand translation = connection.CreateCommand())
        {
            translation.Transaction = transaction;
            translation.CommandText =
                """
                SELECT "Id", "LanguageCode", "Title", "City", "Municipality",
                       "AddressLine", "Description"
                FROM public."ListingTranslations"
                WHERE "ListingId" = @listingId
                ORDER BY "LanguageCode";
                """;
            translation.Parameters.AddWithValue("listingId", listingId);
            await using NpgsqlDataReader reader =
                await translation.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                translations.Add(new TranslationSnapshot(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }

        return new ListingSnapshot(
            status,
            root,
            xmin,
            modifiedAtUtc,
            translations.ToArray());
    }

    private static async Task<int> ReadBackendPidAsync(
        NpgsqlConnection connection)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_catalog.pg_backend_pid();";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<BlockingObservation> WaitForBlockedAsync(
        NpgsqlConnection observer,
        int waitingPid,
        int blockingPid,
        Task competingTask,
        string expectedQueryFragment)
    {
        using var timeout = new CancellationTokenSource(TestTimeout);

        try
        {
            while (true)
            {
                if (competingTask.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "The competing statement completed before PostgreSQL " +
                        "reported the expected parent-lock wait.");
                }

                await using NpgsqlCommand command = observer.CreateCommand();
                command.CommandTimeout = 2;
                command.CommandText =
                    """
                    SELECT activity.wait_event,
                           activity.query,
                           COALESCE((
                               SELECT string_agg(lock_mode.mode, ',' ORDER BY lock_mode.mode)
                               FROM (
                                   SELECT DISTINCT held_lock.mode
                                   FROM pg_catalog.pg_locks AS held_lock
                                   INNER JOIN pg_catalog.pg_class AS relation
                                       ON relation.oid = held_lock.relation
                                   INNER JOIN pg_catalog.pg_namespace AS namespace
                                       ON namespace.oid = relation.relnamespace
                                   WHERE held_lock.pid = @blockingPid
                                     AND held_lock.granted
                                     AND namespace.nspname = 'public'
                                     AND relation.relname = 'Listings'
                               ) AS lock_mode
                           ), '') AS blocker_listing_lock_modes,
                           pg_catalog.pg_blocking_pids(activity.pid)
                    FROM pg_catalog.pg_stat_activity AS activity
                    WHERE activity.datname = pg_catalog.current_database()
                      AND activity.pid = @waitingPid
                      AND activity.state = 'active'
                      AND activity.wait_event_type = 'Lock'
                      AND @blockingPid = ANY(
                          pg_catalog.pg_blocking_pids(activity.pid));
                    """;
                command.Parameters.AddWithValue("waitingPid", waitingPid);
                command.Parameters.AddWithValue("blockingPid", blockingPid);

                await using NpgsqlDataReader reader =
                    await command.ExecuteReaderAsync(timeout.Token);
                if (!await reader.ReadAsync(timeout.Token))
                {
                    continue;
                }

                var observation = new BlockingObservation(
                    waitingPid,
                    blockingPid,
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetFieldValue<int[]>(3));
                observation.Query.Should().Contain(expectedQueryFragment);
                return observation;
            }
        }
        catch (OperationCanceledException exception)
            when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                "PostgreSQL did not report the expected bounded parent-lock wait.",
                exception);
        }
    }

    private static void AssertParentBlock(
        BlockingObservation observation,
        string because,
        string expectedBlockerRelationLockMode = "RowExclusiveLock")
    {
        observation.WaitingPid.Should().NotBe(observation.BlockingPid, because);
        observation.WaitEvent.Should().BeOneOf("transactionid", "tuple");
        observation.BlockerListingsLockModes.Should()
            .Contain(expectedBlockerRelationLockMode, because);
    }

    private static void AssertTriggerFailure(
        PostgresException exception,
        string message)
    {
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.SqlState.Should().NotBe(PostgresErrorCodes.DeadlockDetected);
        exception.ConstraintName.Should().BeNull();
        exception.MessageText.Should().Be(message);
    }

    private static void AssertAlternateRoot(RootSnapshot root)
    {
        root.Should().Be(new RootSnapshot(
            42.004m,
            21.409m,
            "Street",
            "j6-alternate-provider",
            "j6-alternate-reference",
            "J6 alternate display",
            new DateTime(2026, 8, 27, 9, 0, 0, DateTimeKind.Utc)));
    }

    private static void AssertUnresolvedRoot(RootSnapshot root)
    {
        root.Should().Be(new RootSnapshot(
            null,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    private static void AssertMultiParentFinal(
        ListingSnapshot final,
        ListingSnapshot afterFirstWriter,
        ListingSnapshot before)
    {
        final.Status.Should().Be("Draft");
        final.Root.Should().Be(before.Root);
        final.ModifiedAtUtc.Should().Be(before.ModifiedAtUtc);
        final.Xmin.Should().NotBe(afterFirstWriter.Xmin);
        TranslationSnapshot[] expectedTranslations = before.Translations
            .Select(translation => translation with
            {
                Title = translation.LanguageCode == "en"
                    ? "Writer A title"
                    : "Writer B title"
            })
            .ToArray();
        final.Translations.Should().BeEquivalentTo(
            expectedTranslations,
            options => options.WithStrictOrdering());
    }

    private static void AssertFirstMultiParentWriter(
        ListingSnapshot current,
        ListingSnapshot before)
    {
        current.Status.Should().Be("Draft");
        current.Root.Should().Be(before.Root);
        current.ModifiedAtUtc.Should().Be(before.ModifiedAtUtc);
        current.Xmin.Should().NotBe(before.Xmin);
        TranslationSnapshot[] expectedTranslations = before.Translations
            .Select(translation => translation.LanguageCode == "en"
                ? translation with { Title = "Writer A title" }
                : translation)
            .ToArray();
        current.Translations.Should().BeEquivalentTo(
            expectedTranslations,
            options => options.WithStrictOrdering());
    }

    private static async Task DrainSafelyAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TestTimeout);
        }
        catch
        {
            // The owning assertion records the expected result. This only observes
            // a started task so transaction cleanup cannot leave a blocked command.
        }
    }

    private static async Task RollbackSafelyAsync(
        NpgsqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (InvalidOperationException)
        {
            // The transaction was already completed.
        }
        catch (PostgresException)
        {
            // A failed statement may already have aborted the transaction.
        }
    }

    private sealed record ListingSeed(
        Guid ListingId,
        Guid PrimaryTranslationId,
        Guid? SecondaryTranslationId);

    private sealed record ListingSnapshot(
        string Status,
        RootSnapshot Root,
        long Xmin,
        DateTime? ModifiedAtUtc,
        IReadOnlyList<TranslationSnapshot> Translations);

    private sealed record RootSnapshot(
        decimal? Latitude,
        decimal? Longitude,
        string? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc);

    private sealed record TranslationSnapshot(
        Guid Id,
        string LanguageCode,
        string Title,
        string? City,
        string? Municipality,
        string? AddressLine,
        string? Description);

    private sealed record BlockingObservation(
        int WaitingPid,
        int BlockingPid,
        string? WaitEvent,
        string Query,
        string BlockerListingsLockModes,
        IReadOnlyList<int> BlockingPids);
}
