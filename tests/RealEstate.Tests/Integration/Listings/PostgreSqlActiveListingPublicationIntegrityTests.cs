using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlActiveListingPublicationIntegrityTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string PreviousMigration =
        "20260809124123_EnforceListingTranslationRowIntegrity";
    private const string CurrentMigration =
        "20260811091318_EnforceActiveListingPublicationIntegrity";
    private const string IntegrityViolationMessage =
        "Active listing publication integrity violation.";
    private const string ActiveFreezeMessage =
        "Active listing translations are immutable; unpublish before editing.";

    private static readonly string[] FunctionNames =
    [
        "re_assert_active_listing_publication_integrity",
        "re_guard_listing_translation_parent_mutation",
        "re_listings_active_integrity_after_insert",
        "re_listings_active_integrity_after_update",
        "re_listing_translations_guard_after_insert",
        "re_listing_translations_guard_after_update",
        "re_listing_translations_guard_after_delete"
    ];

    private static readonly IReadOnlyDictionary<string, TriggerExpectation>
        TriggerExpectations = new Dictionary<string, TriggerExpectation>(
            StringComparer.Ordinal)
        {
            ["TR_Listings_ActivePublicationIntegrity_Insert"] = new(
                "Listings",
                "AFTER INSERT",
                "NEW TABLE AS new_listings"),
            ["TR_Listings_ActivePublicationIntegrity_Update"] = new(
                "Listings",
                "AFTER UPDATE",
                "NEW TABLE AS new_listings"),
            ["TR_ListingTranslations_ActiveFreeze_Insert"] = new(
                "ListingTranslations",
                "AFTER INSERT",
                "NEW TABLE AS new_translations"),
            ["TR_ListingTranslations_ActiveFreeze_Update"] = new(
                "ListingTranslations",
                "AFTER UPDATE",
                "OLD TABLE AS old_translations NEW TABLE AS new_translations"),
            ["TR_ListingTranslations_ActiveFreeze_Delete"] = new(
                "ListingTranslations",
                "AFTER DELETE",
                "OLD TABLE AS old_translations")
        };

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlActiveListingPublicationIntegrityTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_ContainsSchemaSafeStatementTriggersWithTransitionTables()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        IReadOnlyDictionary<string, TriggerCatalogRow> triggers =
            await ReadTriggerCatalogAsync(dbContext);
        IReadOnlyDictionary<string, string> functions =
            await ReadFunctionCatalogAsync(dbContext);

        triggers.Keys.Should().BeEquivalentTo(TriggerExpectations.Keys);

        foreach ((string triggerName, TriggerExpectation expected) in
                 TriggerExpectations)
        {
            TriggerCatalogRow actual = triggers[triggerName];
            actual.TableName.Should().Be(expected.TableName);
            actual.IsStatementLevel.Should().BeTrue();
            actual.Definition.Should().Contain(expected.Event);
            actual.Definition.Should().Contain(expected.TransitionTables);
            actual.Definition.Should().Contain("FOR EACH STATEMENT");
        }

        functions.Keys.Should().BeEquivalentTo(FunctionNames);
        functions.Values.Should().OnlyContain(configuration =>
            configuration.Contains(
                "search_path=pg_catalog",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivationGuards_EnforceExistenceValidityAndStatementAtomicity()
    {
        Guid emptyListingId = await InsertListingAsync(ListingStatus.Draft);
        (await ReadListingStatusAsync(emptyListingId))
            .Should().Be(ListingStatus.Draft);

        await AssertIntegrityRejectedAsync(
            () => SetListingStatusAsync(emptyListingId, ListingStatus.Active));
        (await ReadListingStatusAsync(emptyListingId))
            .Should().Be(ListingStatus.Draft);

        Guid validListingId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(validListingId, "en", "Valid title", "Skopje", "Valid description");
        await SetListingStatusAsync(validListingId, ListingStatus.Active);
        (await ReadListingStatusAsync(validListingId))
            .Should().Be(ListingStatus.Active);

        Guid incompleteListingId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(
            incompleteListingId,
            "en",
            "Incomplete title",
            city: null,
            description: "Draft-valid description");
        await AssertIntegrityRejectedAsync(
            () => SetListingStatusAsync(incompleteListingId, ListingStatus.Active));
        (await ReadListingStatusAsync(incompleteListingId))
            .Should().Be(ListingStatus.Draft);

        Guid secondValidListingId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(
            secondValidListingId,
            "en",
            "Second valid title",
            "Skopje",
            "Second valid description");

        await AssertIntegrityRejectedAsync(
            () => ActivateListingsAsync(incompleteListingId, secondValidListingId));
        (await ReadListingStatusAsync(incompleteListingId))
            .Should().Be(ListingStatus.Draft);
        (await ReadListingStatusAsync(secondValidListingId))
            .Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public async Task ActivationMatrix_RequiresEveryTranslationAndRevalidatesActiveRows()
    {
        Guid multipleValidId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(
            multipleValidId,
            "en",
            "First valid title",
            "Skopje",
            "First valid description");
        await InsertTranslationAsync(
            multipleValidId,
            "mk",
            "Second valid title",
            "Bitola",
            "Second valid description");

        await SetListingStatusAsync(multipleValidId, ListingStatus.Active);
        await UpdateListingPriceToCurrentValueAsync(multipleValidId);

        (await ReadListingStatusAsync(multipleValidId))
            .Should().Be(ListingStatus.Active);

        Guid mixedValidityId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(
            mixedValidityId,
            "en",
            "Valid translation",
            "Skopje",
            "Valid description");
        await InsertTranslationAsync(
            mixedValidityId,
            "mk",
            "Incomplete translation",
            city: null,
            description: "Still a Draft-valid row");

        await AssertIntegrityRejectedAsync(
            () => SetListingStatusAsync(
                mixedValidityId,
                ListingStatus.Active));
        (await ReadListingStatusAsync(mixedValidityId))
            .Should().Be(ListingStatus.Draft);

        Guid allIncompleteId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(
            allIncompleteId,
            "en",
            "Missing City",
            city: null,
            description: "Present description");
        await InsertTranslationAsync(
            allIncompleteId,
            "mk",
            "Missing Description",
            city: "Skopje",
            description: null);

        await AssertIntegrityRejectedAsync(
            () => SetListingStatusAsync(
                allIncompleteId,
                ListingStatus.Active));
        (await ReadListingStatusAsync(allIncompleteId))
            .Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public async Task DirectActiveInsert_WithoutAggregateTruth_IsRejected()
    {
        Guid listingId = Guid.NewGuid();

        await AssertIntegrityRejectedAsync(
            () => InsertListingAsync(listingId, ListingStatus.Active));

        (await ListingExistsAsync(listingId)).Should().BeFalse();
    }

    [Fact]
    public async Task DirectDatabaseActivationRejectionSetup_CannotCommitActiveState()
    {
        Listing listing = StrongLocationListingTestFixtures
            .CreateDirectDatabaseActivationRejectionSetup();

        await using (AsyncServiceScope scope =
                     _factory.Services.CreateAsyncScope())
        {
            RealEstateDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            dbContext.Listings.Add(listing);
            await dbContext.SaveChangesAsync();
        }

        await AssertIntegrityRejectedAsync(
            () => SetListingStatusAsync(
                listing.Id,
                ListingStatus.Active));
        (await ReadListingStatusAsync(listing.Id))
            .Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public async Task ActiveTranslationMutations_AreFrozenUntilParentIsDraft()
    {
        Guid listingId = await InsertListingAsync(ListingStatus.Draft);
        Guid translationId = await InsertTranslationAsync(
            listingId,
            "en",
            "Original title",
            "Skopje",
            "Original description");
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        await AssertActiveFreezeRejectedAsync(
            () => InsertTranslationAsync(
                listingId,
                "mk",
                "Нов наслов",
                "Скопје",
                "Нов опис"));
        await AssertActiveFreezeRejectedAsync(
            () => UpdateTranslationTitleAsync(translationId, "Changed title"));
        await AssertActiveFreezeRejectedAsync(
            () => DeleteTranslationAsync(translationId));

        await SetListingStatusAsync(listingId, ListingStatus.Draft);
        await UpdateTranslationTitleAsync(translationId, "Changed after unpublish");

        ListingTranslation persisted = await ReadTranslationAsync(translationId);
        persisted.Title.Should().Be("Changed after unpublish");
        persisted.ListingId.Should().Be(listingId);
    }

    [Fact]
    public async Task ActiveTranslationInsert_RejectsValidAndIncompleteRows()
    {
        Guid listingId = await InsertListingAsync(ListingStatus.Draft);
        await InsertTranslationAsync(
            listingId,
            "en",
            "Existing title",
            "Skopje",
            "Existing description");
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        await AssertActiveFreezeRejectedAsync(
            () => InsertTranslationAsync(
                listingId,
                "mk",
                "Valid inserted title",
                "Bitola",
                "Valid inserted description"));
        await AssertActiveFreezeRejectedAsync(
            () => InsertTranslationAsync(
                listingId,
                "de",
                "Incomplete inserted title",
                city: null,
                description: "Draft-valid description"));

        (await CountTranslationsAsync(listingId)).Should().Be(1);
    }

    [Fact]
    public async Task ActiveTranslationUpdateAndDelete_FreezeEveryEditableColumnAndCardinality()
    {
        Guid multipleListingId = await InsertListingAsync(ListingStatus.Draft);
        Guid firstTranslationId = await InsertTranslationAsync(
            multipleListingId,
            "en",
            "First title",
            "Skopje",
            "First description");
        await InsertTranslationAsync(
            multipleListingId,
            "mk",
            "Second title",
            "Bitola",
            "Second description");
        await SetListingStatusAsync(
            multipleListingId,
            ListingStatus.Active);

        ListingTranslation original =
            await ReadTranslationAsync(firstTranslationId);

        foreach ((string column, string value) in new[]
                 {
                     ("Title", "Changed title"),
                     ("City", "Ohrid"),
                     ("Description", "Changed description"),
                     ("LanguageCode", "de")
                 })
        {
            await AssertActiveFreezeRejectedAsync(
                () => UpdateTranslationColumnAsync(
                    firstTranslationId,
                    column,
                    value));
        }

        await AssertActiveFreezeRejectedAsync(
            () => DeleteTranslationAsync(firstTranslationId));

        ListingTranslation afterRejectedMutations =
            await ReadTranslationAsync(firstTranslationId);
        afterRejectedMutations.Should().BeEquivalentTo(original);
        (await CountTranslationsAsync(multipleListingId)).Should().Be(2);

        Guid singleListingId = await InsertListingAsync(ListingStatus.Draft);
        Guid lastTranslationId = await InsertTranslationAsync(
            singleListingId,
            "en",
            "Last title",
            "Skopje",
            "Last description");
        await SetListingStatusAsync(singleListingId, ListingStatus.Active);

        await AssertActiveFreezeRejectedAsync(
            () => DeleteTranslationAsync(lastTranslationId));

        (await ReadTranslationAsync(lastTranslationId)).ListingId
            .Should().Be(singleListingId);
        (await CountTranslationsAsync(singleListingId)).Should().Be(1);
    }

    [Fact]
    public async Task DraftTranslationMutations_AcceptNullableContentAndTouchParentMvccOnly()
    {
        Guid listingId = await InsertListingAsync(ListingStatus.Draft);
        long initialXmin = await ReadListingXminAsync(listingId);
        DateTime? initialModifiedAt = await ReadListingModifiedAtAsync(listingId);

        Guid translationId = await InsertTranslationAsync(
            listingId,
            "en",
            "Draft title",
            city: null,
            description: null);
        long afterInsertXmin = await ReadListingXminAsync(listingId);

        await UpdateTranslationTitleAsync(translationId, "Updated Draft title");
        long afterUpdateXmin = await ReadListingXminAsync(listingId);

        await DeleteTranslationAsync(translationId);
        long afterDeleteXmin = await ReadListingXminAsync(listingId);

        afterInsertXmin.Should().NotBe(initialXmin);
        afterUpdateXmin.Should().NotBe(afterInsertXmin);
        afterDeleteXmin.Should().NotBe(afterUpdateXmin);
        (await ReadListingStatusAsync(listingId)).Should().Be(ListingStatus.Draft);
        (await ReadListingModifiedAtAsync(listingId)).Should().Be(initialModifiedAt);
    }

    [Fact]
    public async Task TranslationListingIdMove_LocksAndTouchesBothParentsAndRejectsBothActiveDirections()
    {
        Guid firstDraftId = await InsertListingAsync(ListingStatus.Draft);
        Guid secondDraftId = await InsertListingAsync(ListingStatus.Draft);
        Guid translationId = await InsertTranslationAsync(
            firstDraftId,
            "en",
            "Moveable title",
            "Skopje",
            "Moveable description");
        long firstBeforeMove = await ReadListingXminAsync(firstDraftId);
        long secondBeforeMove = await ReadListingXminAsync(secondDraftId);

        await MoveTranslationAsync(translationId, secondDraftId);

        (await ReadTranslationAsync(translationId)).ListingId
            .Should().Be(secondDraftId);
        (await ReadListingXminAsync(firstDraftId)).Should().NotBe(firstBeforeMove);
        (await ReadListingXminAsync(secondDraftId)).Should().NotBe(secondBeforeMove);

        await SetListingStatusAsync(secondDraftId, ListingStatus.Active);
        Guid thirdDraftId = await InsertListingAsync(ListingStatus.Draft);

        await AssertActiveFreezeRejectedAsync(
            () => MoveTranslationAsync(translationId, thirdDraftId));

        (await ReadTranslationAsync(translationId)).ListingId
            .Should().Be(secondDraftId);

        Guid fourthDraftId = await InsertListingAsync(ListingStatus.Draft);
        Guid draftSourceTranslationId = await InsertTranslationAsync(
            fourthDraftId,
            "de",
            "Draft source title",
            "Berlin",
            "Draft source description");
        Guid activeDestinationId = await InsertListingAsync(ListingStatus.Draft);
        Guid activeDestinationTranslationId = await InsertTranslationAsync(
            activeDestinationId,
            "en",
            "Active destination title",
            "Skopje",
            "Active destination description");
        await SetListingStatusAsync(
            activeDestinationId,
            ListingStatus.Active);
        long draftSourceXminBeforeRejectedMove =
            await ReadListingXminAsync(fourthDraftId);
        long activeDestinationXminBeforeRejectedMove =
            await ReadListingXminAsync(activeDestinationId);

        await AssertActiveFreezeRejectedAsync(
            () => MoveTranslationAsync(
                draftSourceTranslationId,
                activeDestinationId));

        (await ReadTranslationAsync(draftSourceTranslationId)).ListingId
            .Should().Be(fourthDraftId);
        (await ReadTranslationAsync(activeDestinationTranslationId)).ListingId
            .Should().Be(activeDestinationId);
        (await ReadListingXminAsync(fourthDraftId))
            .Should().Be(draftSourceXminBeforeRejectedMove);
        (await ReadListingXminAsync(activeDestinationId))
            .Should().Be(activeDestinationXminBeforeRejectedMove);
    }

    [Fact]
    public async Task TranslationListingIdMove_ActiveToActiveRejectsAtomically()
    {
        Guid activeSourceId = await InsertListingAsync(ListingStatus.Draft);
        Guid sourceTranslationId = await InsertTranslationAsync(
            activeSourceId,
            "en",
            "Active source title",
            "Skopje",
            "Active source description");
        await SetListingStatusAsync(activeSourceId, ListingStatus.Active);

        Guid activeDestinationId = await InsertListingAsync(ListingStatus.Draft);
        Guid destinationTranslationId = await InsertTranslationAsync(
            activeDestinationId,
            "mk",
            "Active destination title",
            "Bitola",
            "Active destination description");
        await SetListingStatusAsync(
            activeDestinationId,
            ListingStatus.Active);
        long sourceXmin = await ReadListingXminAsync(activeSourceId);
        long destinationXmin =
            await ReadListingXminAsync(activeDestinationId);

        await AssertActiveFreezeRejectedAsync(
            () => MoveTranslationAsync(
                sourceTranslationId,
                activeDestinationId));

        (await ReadTranslationAsync(sourceTranslationId)).ListingId
            .Should().Be(activeSourceId);
        (await ReadTranslationAsync(destinationTranslationId)).ListingId
            .Should().Be(activeDestinationId);
        (await CountTranslationsAsync(activeSourceId)).Should().Be(1);
        (await CountTranslationsAsync(activeDestinationId)).Should().Be(1);
        (await ReadListingXminAsync(activeSourceId)).Should().Be(sourceXmin);
        (await ReadListingXminAsync(activeDestinationId))
            .Should().Be(destinationXmin);
    }

    [Fact]
    public async Task MultiRowTranslationUpdate_WithActiveParentRejectsEntireStatement()
    {
        Guid draftListingId = await InsertListingAsync(ListingStatus.Draft);
        Guid draftTranslationId = await InsertTranslationAsync(
            draftListingId,
            "en",
            "Draft original title",
            "Skopje",
            "Draft description");
        Guid activeListingId = await InsertListingAsync(ListingStatus.Draft);
        Guid activeTranslationId = await InsertTranslationAsync(
            activeListingId,
            "en",
            "Active original title",
            "Bitola",
            "Active description");
        await SetListingStatusAsync(activeListingId, ListingStatus.Active);
        long draftXmin = await ReadListingXminAsync(draftListingId);
        long activeXmin = await ReadListingXminAsync(activeListingId);

        await AssertActiveFreezeRejectedAsync(
            () => UpdateTranslationTitlesAsync(
                [draftTranslationId, activeTranslationId],
                "Rejected multi-row title"));

        (await ReadTranslationAsync(draftTranslationId)).Title
            .Should().Be("Draft original title");
        (await ReadTranslationAsync(activeTranslationId)).Title
            .Should().Be("Active original title");
        (await ReadListingXminAsync(draftListingId)).Should().Be(draftXmin);
        (await ReadListingXminAsync(activeListingId)).Should().Be(activeXmin);
    }

    [Fact]
    public async Task MultiRowDraftTranslationUpdate_TouchesEveryParentWithoutAuditChange()
    {
        Guid firstListingId = await InsertListingAsync(ListingStatus.Draft);
        Guid firstTranslationId = await InsertTranslationAsync(
            firstListingId,
            "en",
            "First original title",
            city: null,
            description: null);
        Guid secondListingId = await InsertListingAsync(ListingStatus.Draft);
        Guid secondTranslationId = await InsertTranslationAsync(
            secondListingId,
            "en",
            "Second original title",
            city: null,
            description: null);
        long firstXmin = await ReadListingXminAsync(firstListingId);
        long secondXmin = await ReadListingXminAsync(secondListingId);
        DateTime? firstModifiedAt =
            await ReadListingModifiedAtAsync(firstListingId);
        DateTime? secondModifiedAt =
            await ReadListingModifiedAtAsync(secondListingId);

        await UpdateTranslationTitlesAsync(
            [firstTranslationId, secondTranslationId],
            "Accepted multi-row title");

        (await ReadTranslationAsync(firstTranslationId)).Title
            .Should().Be("Accepted multi-row title");
        (await ReadTranslationAsync(secondTranslationId)).Title
            .Should().Be("Accepted multi-row title");
        (await ReadListingXminAsync(firstListingId)).Should().NotBe(firstXmin);
        (await ReadListingXminAsync(secondListingId)).Should().NotBe(secondXmin);
        (await ReadListingModifiedAtAsync(firstListingId))
            .Should().Be(firstModifiedAt);
        (await ReadListingModifiedAtAsync(secondListingId))
            .Should().Be(secondModifiedAt);
    }

    [Fact]
    public async Task ListingDelete_CascadesTranslationsWithoutChildGuardFailure()
    {
        Guid listingId = await InsertListingAsync(ListingStatus.Draft);
        Guid translationId = await InsertTranslationAsync(
            listingId,
            "en",
            "Cascade title",
            "Skopje",
            "Cascade description");
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        await DeleteListingAsync(listingId);

        (await ListingExistsAsync(listingId)).Should().BeFalse();
        (await TranslationExistsAsync(translationId)).Should().BeFalse();
    }

    [Fact]
    public async Task MigrationLifecycle_UpDownAndUpAgain_PreservesEarlierObjects()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();

        await using (RealEstateDbContext dbContext = database.CreateContext())
        {
            IMigrator migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(CurrentMigration);
            (await CountIntegrityObjectsAsync(dbContext)).Should().Be((5, 7));

            await migrator.MigrateAsync(PreviousMigration);
            (await CountIntegrityObjectsAsync(dbContext)).Should().Be((0, 0));
            (await ReadEarlierIntegrityObjectsAsync(dbContext)).Should().Be(
                (4, 2));

            await migrator.MigrateAsync(CurrentMigration);
            (await CountIntegrityObjectsAsync(dbContext)).Should().Be((5, 7));
        }
    }

    [Fact]
    public async Task AlreadyActiveUpdate_RevalidatesMalformedAggregateInIsolatedDatabase()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid listingId = Guid.NewGuid();
        Guid translationId = Guid.NewGuid();

        await using RealEstateDbContext dbContext = database.CreateContext();
        IMigrator migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(CurrentMigration);
        await InsertListingAsync(dbContext, listingId, ListingStatus.Draft);
        await InsertTranslationAsync(
            dbContext,
            translationId,
            listingId,
            "en",
            "Initially valid title",
            "Skopje",
            "Initially valid description");
        await SetListingStatusAsync(
            dbContext,
            listingId,
            ListingStatus.Active);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE public."ListingTranslations"
            DISABLE TRIGGER "TR_ListingTranslations_ActiveFreeze_Update";
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE public."ListingTranslations"
             SET "Description" = NULL
             WHERE "Id" = {translationId}
             """);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE public."ListingTranslations"
            ENABLE TRIGGER "TR_ListingTranslations_ActiveFreeze_Update";
            """);

        Func<Task> action = () => UpdateListingPriceToCurrentValueAsync(
            dbContext,
            listingId);

        await AssertIntegrityRejectedAsync(action);
        (await ReadListingStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Active);
        (await ReadTranslationDescriptionAsync(dbContext, translationId))
            .Should().BeNull();
    }

    [Fact]
    public async Task MigrationWithMalformedActiveData_FailsAtomicallyWithoutRepair()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid listingId = Guid.NewGuid();

        await using (RealEstateDbContext setupContext = database.CreateContext())
        {
            IMigrator migrator = setupContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertListingAsync(
                setupContext,
                listingId,
                ListingStatus.Active);
        }

        await using (RealEstateDbContext migrationContext = database.CreateContext())
        {
            IMigrator migrator = migrationContext.GetService<IMigrator>();
            Func<Task> action = () => migrator.MigrateAsync(CurrentMigration);

            PostgresException exception =
                (await action.Should().ThrowAsync<PostgresException>()).Which;
            exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            exception.MessageText.Should().Be(IntegrityViolationMessage);
        }

        await using (RealEstateDbContext verificationContext = database.CreateContext())
        {
            (await CountIntegrityObjectsAsync(verificationContext))
                .Should().Be((0, 0));
            (await ReadMigrationAppliedAsync(
                    verificationContext,
                    CurrentMigration))
                .Should().BeFalse();
            ListingStatus status = await verificationContext.Listings
                .AsNoTracking()
                .Where(listing => listing.Id == listingId)
                .Select(listing => listing.Status)
                .SingleAsync();
            status.Should().Be(ListingStatus.Active);
        }
    }

    private async Task<Guid> InsertListingAsync(ListingStatus status)
    {
        Guid listingId = Guid.NewGuid();
        await InsertListingAsync(listingId, status);
        return listingId;
    }

    private async Task InsertListingAsync(Guid listingId, ListingStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await InsertListingAsync(dbContext, listingId, status);
    }

    private static Task InsertListingAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        ListingStatus status)
    {
        DateTime createdAtUtc = DateTime.UtcNow;
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Listings"
                 ("Id", "ListingType", "PropertyType", "Status", "Price",
                  "Currency", "AreaSquareMeters", "CreatedAtUtc")
             VALUES
                 ({listingId}, 'Sale', 'Apartment', {status.ToString()}, 100000,
                  'EUR', 80, {createdAtUtc})
             """);
    }

    private async Task<Guid> InsertTranslationAsync(
        Guid listingId,
        string languageCode,
        string title,
        string? city,
        string? description)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid translationId = Guid.NewGuid();

        await InsertTranslationAsync(
            dbContext,
            translationId,
            listingId,
            languageCode,
            title,
            city,
            description);

        return translationId;
    }

    private static Task InsertTranslationAsync(
        RealEstateDbContext dbContext,
        Guid translationId,
        Guid listingId,
        string languageCode,
        string title,
        string? city,
        string? description)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ListingTranslations"
                 ("Id", "ListingId", "LanguageCode", "Title", "City", "Description")
             VALUES
                 ({translationId}, {listingId}, {languageCode}, {title}, {city}, {description})
             """);
    }

    private async Task SetListingStatusAsync(
        Guid listingId,
        ListingStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await SetListingStatusAsync(dbContext, listingId, status);
    }

    private static Task SetListingStatusAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        ListingStatus status)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Status" = {status.ToString()}
             WHERE "Id" = {listingId}
             """);
    }

    private async Task UpdateListingPriceToCurrentValueAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await UpdateListingPriceToCurrentValueAsync(dbContext, listingId);
    }

    private static Task UpdateListingPriceToCurrentValueAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Price" = "Price"
             WHERE "Id" = {listingId}
             """);
    }

    private async Task ActivateListingsAsync(params Guid[] listingIds)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Status" = 'Active'
             WHERE "Id" = ANY({listingIds})
             """);
    }

    private async Task UpdateTranslationTitleAsync(
        Guid translationId,
        string title)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "ListingTranslations"
             SET "Title" = {title}
             WHERE "Id" = {translationId}
             """);
    }

    private async Task UpdateTranslationColumnAsync(
        Guid translationId,
        string column,
        string value)
    {
        string commandText = column switch
        {
            "Title" =>
                "UPDATE \"ListingTranslations\" SET \"Title\" = @value WHERE \"Id\" = @id;",
            "City" =>
                "UPDATE \"ListingTranslations\" SET \"City\" = @value WHERE \"Id\" = @id;",
            "Description" =>
                "UPDATE \"ListingTranslations\" SET \"Description\" = @value WHERE \"Id\" = @id;",
            "LanguageCode" =>
                "UPDATE \"ListingTranslations\" SET \"LanguageCode\" = @value WHERE \"Id\" = @id;",
            _ => throw new ArgumentOutOfRangeException(
                nameof(column),
                column,
                "Unsupported translation column.")
        };

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        DbParameter valueParameter = command.CreateParameter();
        valueParameter.ParameterName = "value";
        valueParameter.DbType = DbType.String;
        valueParameter.Value = value;
        command.Parameters.Add(valueParameter);
        AddGuidParameter(command, "id", translationId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateTranslationTitlesAsync(
        Guid[] translationIds,
        string title)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "ListingTranslations"
             SET "Title" = {title}
             WHERE "Id" = ANY({translationIds})
             """);
    }

    private async Task MoveTranslationAsync(
        Guid translationId,
        Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "ListingTranslations"
             SET "ListingId" = {listingId}
             WHERE "Id" = {translationId}
             """);
    }

    private async Task DeleteTranslationAsync(Guid translationId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM "ListingTranslations"
             WHERE "Id" = {translationId}
             """);
    }

    private async Task DeleteListingAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM "Listings"
             WHERE "Id" = {listingId}
             """);
    }

    private async Task<ListingStatus> ReadListingStatusAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return await ReadListingStatusAsync(dbContext, listingId);
    }

    private static Task<ListingStatus> ReadListingStatusAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => listing.Status)
            .SingleAsync();
    }

    private async Task<DateTime?> ReadListingModifiedAtAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => listing.ModifiedAtUtc)
            .SingleAsync();
    }

    private async Task<long> ReadListingXminAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT xmin::text::bigint
            FROM public."Listings"
            WHERE "Id" = @listingId;
            """;
        AddGuidParameter(command, "listingId", listingId);

        object result = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Listing xmin was not found.");
        return Convert.ToInt64(result);
    }

    private async Task<ListingTranslation> ReadTranslationAsync(Guid translationId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingTranslation>()
            .AsNoTracking()
            .SingleAsync(translation => translation.Id == translationId);
    }

    private static Task<string?> ReadTranslationDescriptionAsync(
        RealEstateDbContext dbContext,
        Guid translationId)
    {
        return dbContext.Set<ListingTranslation>()
            .AsNoTracking()
            .Where(translation => translation.Id == translationId)
            .Select(translation => translation.Description)
            .SingleAsync();
    }

    private async Task<int> CountTranslationsAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<ListingTranslation>()
            .AsNoTracking()
            .CountAsync(translation => translation.ListingId == listingId);
    }

    private async Task<bool> ListingExistsAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        return await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(listing => listing.Id == listingId);
    }

    private async Task<bool> TranslationExistsAsync(Guid translationId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        return await dbContext.Set<ListingTranslation>()
            .AsNoTracking()
            .AnyAsync(translation => translation.Id == translationId);
    }

    private static async Task<IReadOnlyDictionary<string, TriggerCatalogRow>>
        ReadTriggerCatalogAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT trigger.tgname,
                   table_class.relname,
                   (trigger.tgtype & 1) = 0 AS is_statement_level,
                   pg_get_triggerdef(trigger.oid)
            FROM pg_catalog.pg_trigger AS trigger
            JOIN pg_catalog.pg_class AS table_class
              ON table_class.oid = trigger.tgrelid
            JOIN pg_catalog.pg_namespace AS table_namespace
              ON table_namespace.oid = table_class.relnamespace
            WHERE NOT trigger.tgisinternal
              AND table_namespace.nspname = 'public'
              AND trigger.tgname = ANY(@trigger_names)
            ORDER BY trigger.tgname;
            """;
        AddTextArrayParameter(command, "trigger_names", TriggerExpectations.Keys);

        var result = new Dictionary<string, TriggerCatalogRow>(StringComparer.Ordinal);
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(
                reader.GetString(0),
                new TriggerCatalogRow(
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetString(3)));
        }

        return result;
    }

    private static async Task<IReadOnlyDictionary<string, string>>
        ReadFunctionCatalogAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT procedure.proname,
                   COALESCE(array_to_string(procedure.proconfig, ','), '')
            FROM pg_catalog.pg_proc AS procedure
            JOIN pg_catalog.pg_namespace AS function_namespace
              ON function_namespace.oid = procedure.pronamespace
            WHERE function_namespace.nspname = 'public'
              AND procedure.proname = ANY(@function_names)
            ORDER BY procedure.proname;
            """;
        AddTextArrayParameter(command, "function_names", FunctionNames);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0), reader.GetString(1));
        }

        return result;
    }

    private static async Task<(int Triggers, int Functions)>
        CountIntegrityObjectsAsync(RealEstateDbContext dbContext)
    {
        IReadOnlyDictionary<string, TriggerCatalogRow> triggers =
            await ReadTriggerCatalogAsync(dbContext);
        IReadOnlyDictionary<string, string> functions =
            await ReadFunctionCatalogAsync(dbContext);
        return (triggers.Count, functions.Count);
    }

    private static async Task<(int Constraints, int Indexes)>
        ReadEarlierIntegrityObjectsAsync(RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (
                    SELECT count(*)::integer
                    FROM pg_catalog.pg_constraint
                    WHERE conrelid = 'public."ListingTranslations"'::regclass
                      AND conname IN (
                          'CK_ListingTranslations_LanguageCode_Canonical',
                          'CK_ListingTranslations_Title_TrimmedNonBlank',
                          'CK_ListingTranslations_City_TrimmedNonBlank',
                          'CK_ListingTranslations_Description_TrimmedNonBlank'
                      )
                ),
                (
                    SELECT count(*)::integer
                    FROM pg_catalog.pg_indexes
                    WHERE schemaname = 'public'
                      AND tablename = 'ListingTranslations'
                      AND indexname IN (
                          'IX_ListingTranslations_ListingId_LanguageCode',
                          'IX_ListingTranslations_Q_Trigram'
                      )
                );
            """;

        await using DbDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<bool> ReadMigrationAppliedAsync(
        RealEstateDbContext dbContext,
        string migrationId)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS (
                SELECT 1
                FROM public."__EFMigrationsHistory"
                WHERE "MigrationId" = @migration_id
            );
            """;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "migration_id";
        parameter.DbType = DbType.String;
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);

        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static void AddGuidParameter(
        DbCommand command,
        string name,
        Guid value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Guid;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddTextArrayParameter(
        DbCommand command,
        string name,
        IEnumerable<string> values)
    {
        var parameter = new NpgsqlParameter<string[]>(
            name,
            values.ToArray());
        command.Parameters.Add(parameter);
    }

    private static async Task AssertIntegrityRejectedAsync(Func<Task> action)
    {
        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.MessageText.Should().Be(IntegrityViolationMessage);
    }

    private static async Task AssertActiveFreezeRejectedAsync(Func<Task> action)
    {
        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.MessageText.Should().Be(ActiveFreezeMessage);
    }

    private async Task<IsolatedMigrationDatabase>
        CreateIsolatedMigrationDatabaseAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        string mainConnectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Integration PostgreSQL connection string is unavailable.");
        string databaseName = $"re_13f_{Guid.NewGuid():N}";
        var targetBuilder = new NpgsqlConnectionStringBuilder(mainConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        await using var adminConnection = new NpgsqlConnection(mainConnectionString);
        await adminConnection.OpenAsync();
        await using NpgsqlCommand command = adminConnection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
        await command.ExecuteNonQueryAsync();

        return new IsolatedMigrationDatabase(
            mainConnectionString,
            targetBuilder.ConnectionString,
            databaseName);
    }

    private sealed class IsolatedMigrationDatabase(
        string adminConnectionString,
        string connectionString,
        string databaseName) : IAsyncDisposable
    {
        public RealEstateDbContext CreateContext()
        {
            DbContextOptions<RealEstateDbContext> options =
                new DbContextOptionsBuilder<RealEstateDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;
            return new RealEstateDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            if (!databaseName.StartsWith("re_13f_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to drop a database outside the isolated 13F test namespace.");
            }

            await using (var pooledConnection =
                         new NpgsqlConnection(connectionString))
            {
                NpgsqlConnection.ClearPool(pooledConnection);
            }
            await using var adminConnection =
                new NpgsqlConnection(adminConnectionString);
            await adminConnection.OpenAsync();
            await using NpgsqlCommand command = adminConnection.CreateCommand();
            command.CommandText = $"DROP DATABASE \"{databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed record TriggerExpectation(
        string TableName,
        string Event,
        string TransitionTables);

    private sealed record TriggerCatalogRow(
        string TableName,
        bool IsStatementLevel,
        string Definition);
}
