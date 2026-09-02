using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlStrongActiveLocationIntegrityMigrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string ThirteenFMigration =
        "20260811091318_EnforceActiveListingPublicationIntegrity";
    private const string PreviousMigration =
        "20260813100457_AddCanonicalGeocodedLocationSnapshot";
    private const string CurrentMigration =
        "20260824141614_EnforceStrongActiveLocationIntegrity";
    private const string IntegrityViolationMessage =
        "Active listing publication integrity violation.";
    private const string TranslationFreezeMessage =
        "Active listing translations are immutable; unpublish before editing.";
    private const string LocationFreezeMessage =
        "Active listing location is immutable; unpublish before resolving.";

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
        J4TriggerExpectations = new Dictionary<string, TriggerExpectation>(
            StringComparer.Ordinal)
        {
            ["TR_Listings_ActivePublicationIntegrity_Insert"] = new(
                "Listings",
                "AFTER INSERT",
                "NEW TABLE AS new_listings"),
            ["TR_Listings_ActivePublicationIntegrity_Update"] = new(
                "Listings",
                "AFTER UPDATE",
                "OLD TABLE AS old_listings NEW TABLE AS new_listings"),
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

    public static TheoryData<decimal, decimal> CoordinateBoundaries => new()
    {
        { -90m, -180m },
        { 90m, 180m }
    };

    public static TheoryData<string> ValidPrecisions => new()
    {
        "ExactAddress",
        "Street",
        "Neighborhood",
        "Municipality",
        "City",
        "Approximate"
    };

    public static TheoryData<string, string> InvalidCanonicalTranslationRows =>
        new()
        {
            { "LanguageCode", "EN" },
            { "Title", " Title " },
            { "City", " Skopje " },
            { "Description", " Description " },
            { "Municipality", "" },
            { "Municipality", "\u00A0" },
            { "Municipality", " Municipality " },
            { "AddressLine", "" },
            { "AddressLine", "\t" },
            { "AddressLine", " Address " }
        };

    public static TheoryData<string, string> InvalidRootAssignments => new()
    {
        { "latitude_only", "\"Latitude\" = 41.9981" },
        { "longitude_only", "\"Longitude\" = 21.4254" },
        {
            "missing_precision",
            ConfirmedAssignment(precision: "NULL")
        },
        {
            "undefined_precision",
            ConfirmedAssignment(precision: "'Parcel'")
        },
        {
            "missing_provider_key",
            ConfirmedAssignment(providerKey: "NULL")
        },
        {
            "blank_provider_key",
            ConfirmedAssignment(providerKey: "''")
        },
        {
            "untrimmed_provider_key",
            ConfirmedAssignment(providerKey: "' provider '")
        },
        {
            "too_long_provider_key",
            ConfirmedAssignment(providerKey: "repeat('p', 65)")
        },
        {
            "missing_result_reference",
            ConfirmedAssignment(resultReference: "NULL")
        },
        {
            "blank_result_reference",
            ConfirmedAssignment(resultReference: "''")
        },
        {
            "untrimmed_result_reference",
            ConfirmedAssignment(resultReference: "' reference '")
        },
        {
            "too_long_result_reference",
            ConfirmedAssignment(resultReference: "repeat('r', 513)")
        },
        {
            "missing_confirmation_time",
            ConfirmedAssignment(confirmationTime: "NULL")
        },
        {
            "latitude_below_range",
            ConfirmedAssignment(latitude: "-90.000001")
        },
        {
            "latitude_above_range",
            ConfirmedAssignment(latitude: "90.000001")
        },
        {
            "longitude_below_range",
            ConfirmedAssignment(longitude: "-180.000001")
        },
        {
            "longitude_above_range",
            ConfirmedAssignment(longitude: "180.000001")
        },
        {
            "blank_display_name",
            ConfirmedAssignment(displayName: "''")
        },
        {
            "untrimmed_display_name",
            ConfirmedAssignment(displayName: "' display '")
        },
        {
            "too_long_display_name",
            ConfirmedAssignment(displayName: "repeat('d', 501)")
        },
        {
            "metadata_without_coordinates",
            "\"LocationPrecision\" = 'City'"
        }
    };

    public static TheoryData<string, string> ActiveRootMutations => new()
    {
        { "latitude", "\"Latitude\" = 42.1" },
        { "longitude", "\"Longitude\" = 22.1" },
        { "precision", "\"LocationPrecision\" = 'Street'" },
        { "provider_key", "\"GeocodingProviderKey\" = 'provider-2'" },
        { "result_reference", "\"GeocodingResultReference\" = 'reference-2'" },
        { "display_name", "\"GeocodedDisplayName\" = 'Changed display'" },
        {
            "confirmation_time",
            "\"LocationConfirmedAtUtc\" = TIMESTAMPTZ '2026-08-25 12:00:00+00'"
        }
    };

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlStrongActiveLocationIntegrityMigrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_Retains13FObjectsAndAddsExactOldNewRootGuard()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        IntegrityCatalog catalog = await ReadIntegrityCatalogAsync(dbContext);

        catalog.Triggers.Keys.Should().BeEquivalentTo(
            J4TriggerExpectations.Keys);
        catalog.Functions.Keys.Should().BeEquivalentTo(FunctionNames);

        foreach ((string name, TriggerExpectation expected) in
                 J4TriggerExpectations)
        {
            TriggerCatalogRow actual = catalog.Triggers[name];
            actual.TableName.Should().Be(expected.TableName);
            actual.IsStatementLevel.Should().BeTrue();
            actual.Definition.Should().Contain(expected.Event);
            actual.Definition.Should().Contain(expected.TransitionTables);
            actual.Definition.Should().Contain("FOR EACH STATEMENT");
        }

        catalog.Functions.Values.Should().OnlyContain(function =>
            function.Configuration.Contains(
                "search_path=pg_catalog",
                StringComparison.Ordinal));

        string assertion = catalog.Functions[
            "re_assert_active_listing_publication_integrity"].Definition;
        assertion.Should().Contain("translation.\"Municipality\" IS NULL");
        assertion.Should().Contain("translation.\"AddressLine\" IS NULL");
        assertion.Should().Contain("btrim");
        assertion.Should().Contain("chr(12288)");
        assertion.Should().Contain("listing.\"Latitude\" NOT BETWEEN -90 AND 90");
        assertion.Should().Contain("listing.\"Longitude\" NOT BETWEEN -180 AND 180");
        assertion.Should().Contain("'ExactAddress'");
        assertion.Should().Contain("'Street'");
        assertion.Should().Contain("'Neighborhood'");
        assertion.Should().Contain("'Municipality'");
        assertion.Should().Contain("'City'");
        assertion.Should().Contain("'Approximate'");
        assertion.Should().Contain(
            "char_length(listing.\"GeocodingProviderKey\") > 64");
        assertion.Should().Contain(
            "char_length(listing.\"GeocodingResultReference\") > 512");
        assertion.Should().Contain(
            "char_length(listing.\"GeocodedDisplayName\") > 500");
        assertion.Should().Contain(
            "listing.\"LocationConfirmedAtUtc\" IS NULL");

        string updateGuard = catalog.Functions[
            "re_listings_active_integrity_after_update"].Definition;
        updateGuard.Should().Contain("FROM old_listings AS old_listing");
        updateGuard.Should().Contain("JOIN new_listings AS new_listing");
        updateGuard.Should().Contain("old_listing.\"Status\" = 'Active'");
        updateGuard.Should().Contain("new_listing.\"Status\" = 'Active'");
        foreach (string field in RootSnapshotFields)
        {
            updateGuard.Should().Contain(
                $"old_listing.\"{field}\" IS DISTINCT FROM");
            updateGuard.Should().Contain($"new_listing.\"{field}\"");
        }
    }

    [Fact]
    public async Task MigrationScript_LocksCanonicalTablesBeforeReplacementAndValidation()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        IMigrator migrator = dbContext.GetService<IMigrator>();

        string script = migrator.GenerateScript(
            PreviousMigration,
            CurrentMigration);

        int listingsLock = script.IndexOf(
            "LOCK TABLE public.\"Listings\" IN SHARE ROW EXCLUSIVE MODE",
            StringComparison.Ordinal);
        int translationsLock = script.IndexOf(
            "LOCK TABLE public.\"ListingTranslations\" IN SHARE ROW EXCLUSIVE MODE",
            StringComparison.Ordinal);
        int assertionReplacement = script.IndexOf(
            "CREATE OR REPLACE FUNCTION public.re_assert_active_listing_publication_integrity",
            StringComparison.Ordinal);
        int triggerReplacement = script.IndexOf(
            "CREATE TRIGGER \"TR_Listings_ActivePublicationIntegrity_Update\"",
            StringComparison.Ordinal);
        int validation = script.LastIndexOf(
            "SELECT public.re_assert_active_listing_publication_integrity",
            StringComparison.Ordinal);

        listingsLock.Should().BeGreaterThanOrEqualTo(0);
        translationsLock.Should().BeGreaterThan(listingsLock);
        assertionReplacement.Should().BeGreaterThan(translationsLock);
        triggerReplacement.Should().BeGreaterThan(assertionReplacement);
        validation.Should().BeGreaterThan(triggerReplacement);
        script.Should().NotContain("SET \"Municipality\" =");
        script.Should().NotContain("SET \"AddressLine\" =");
        script.Should().NotContain("SET \"Latitude\" =");
        script.Should().NotContain("DELETE FROM public.\"Listings\"");
        script.Should().NotContain("DELETE FROM public.\"ListingTranslations\"");
    }

    [Theory]
    [MemberData(nameof(CoordinateBoundaries))]
    public async Task ValidActive_AcceptsInclusiveCoordinateBoundaries(
        decimal latitude,
        decimal longitude)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);

        await ConfirmRootAsync(
            dbContext,
            listingId,
            latitude,
            longitude,
            "ExactAddress",
            displayName: null);
        await SetStatusAsync(dbContext, listingId, ListingStatus.Active);

        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Active);
    }

    [Theory]
    [MemberData(nameof(ValidPrecisions))]
    public async Task ValidActive_AcceptsEveryDefinedPrecision(string precision)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);

        await ConfirmRootAsync(
            dbContext,
            listingId,
            41.9981m,
            21.4254m,
            precision,
            "Canonical display");
        await SetStatusAsync(dbContext, listingId, ListingStatus.Active);

        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Canonical display")]
    public async Task ValidActive_AcceptsNullableOrCanonicalDisplayName(
        string? displayName)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);

        await ConfirmRootAsync(
            dbContext,
            listingId,
            41.9981m,
            21.4254m,
            "Municipality",
            displayName);
        await SetStatusAsync(dbContext, listingId, ListingStatus.Active);

        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Active);
    }

    [Theory]
    [InlineData("City")]
    [InlineData("Description")]
    [InlineData("Municipality")]
    [InlineData("AddressLine")]
    public async Task ActiveTranslation_RejectsMissingRequiredNullableField(
        string field)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertDraftListingAsync(dbContext);
        await InsertTranslationAsync(
            dbContext,
            listingId,
            city: field == "City" ? null : "Skopje",
            description: field == "Description" ? null : "Description",
            municipality: field == "Municipality" ? null : "Centar",
            addressLine: field == "AddressLine" ? null : "Address");
        await ConfirmRootAsync(dbContext, listingId);

        await AssertIntegrityRejectedAsync(
            () => SetStatusAsync(dbContext, listingId, ListingStatus.Active));
        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Draft);
    }

    [Theory]
    [MemberData(nameof(InvalidCanonicalTranslationRows))]
    public async Task TranslationRowTruth_RejectsNoncanonicalRequiredValue(
        string field,
        string invalidValue)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertDraftListingAsync(dbContext);

        Func<Task> action = () => InsertTranslationWithOverrideAsync(
            dbContext,
            listingId,
            field,
            invalidValue);

        await AssertPostgreSqlRejectedAsync(action);
        (await CountTranslationsAsync(dbContext, listingId)).Should().Be(0);
        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public async Task ActiveTranslation_StillRequiresAtLeastOneRow()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertDraftListingAsync(dbContext);
        await ConfirmRootAsync(dbContext, listingId);

        await AssertIntegrityRejectedAsync(
            () => SetStatusAsync(dbContext, listingId, ListingStatus.Active));
        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Draft);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveRoot_RejectsUnresolvedAndLegacyUnverifiedState(
        bool legacyCoordinates)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);
        if (legacyCoordinates)
        {
            await ExecuteListingAssignmentAsync(
                dbContext,
                listingId,
                "\"Latitude\" = 41.9981, \"Longitude\" = 21.4254");
        }

        await AssertIntegrityRejectedAsync(
            () => SetStatusAsync(dbContext, listingId, ListingStatus.Active));
        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Draft);
    }

    [Theory]
    [MemberData(nameof(InvalidRootAssignments))]
    public async Task RootPersistenceTruth_RejectsPartialOrInvalidSnapshot(
        string caseName,
        string assignment)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);

        Func<Task> action = () => ExecuteListingAssignmentAsync(
            dbContext,
            listingId,
            assignment);

        await AssertPostgreSqlRejectedAsync(action, caseName);
        LocationSnapshot snapshot = await ReadLocationSnapshotAsync(
            dbContext,
            listingId);
        snapshot.Should().Be(LocationSnapshot.Unresolved);
        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Draft);
    }

    [Theory]
    [MemberData(nameof(ActiveRootMutations))]
    public async Task ActiveRootMutation_IsFrozenForEverySnapshotMember(
        string fieldFamily,
        string assignment)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);
        await ConfirmRootAsync(dbContext, listingId);
        await SetStatusAsync(dbContext, listingId, ListingStatus.Active);
        LocationSnapshot before = await ReadLocationSnapshotAsync(
            dbContext,
            listingId);

        await AssertLocationFreezeRejectedAsync(
            () => ExecuteListingAssignmentAsync(
                dbContext,
                listingId,
                assignment),
            fieldFamily);

        (await ReadLocationSnapshotAsync(dbContext, listingId))
            .Should().Be(before);
        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task UnpublishThenResolveAndPublish_RemainsSupported()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid listingId = await InsertPublishableDraftAsync(dbContext);
        await ConfirmRootAsync(dbContext, listingId);
        await SetStatusAsync(dbContext, listingId, ListingStatus.Active);

        await SetStatusAsync(dbContext, listingId, ListingStatus.Draft);
        await ExecuteListingAssignmentAsync(
            dbContext,
            listingId,
            ConfirmedAssignment(
                latitude: "42.004",
                longitude: "21.41",
                precision: "'Street'",
                providerKey: "'provider-2'",
                resultReference: "'reference-2'",
                displayName: "'Resolved display'",
                confirmationTime:
                    "TIMESTAMPTZ '2026-08-25 12:00:00+00'"));
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "ListingTranslations"
             SET "Municipality" = 'Karpos',
                 "AddressLine" = 'Resolved address'
             WHERE "ListingId" = {listingId}
             """);
        await SetStatusAsync(dbContext, listingId, ListingStatus.Active);

        (await ReadStatusAsync(dbContext, listingId))
            .Should().Be(ListingStatus.Active);
        LocationSnapshot after = await ReadLocationSnapshotAsync(
            dbContext,
            listingId);
        after.Latitude.Should().Be(42.004m);
        after.Precision.Should().Be("Street");
        after.ProviderKey.Should().Be("provider-2");
    }

    [Fact]
    public async Task MultiRowActivation_IsSetBasedAndStatementAtomic()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Guid validListingId = await InsertPublishableDraftAsync(dbContext);
        Guid unresolvedListingId = await InsertPublishableDraftAsync(dbContext);
        await ConfirmRootAsync(dbContext, validListingId);

        await AssertIntegrityRejectedAsync(
            () => ActivateListingsAsync(
                dbContext,
                validListingId,
                unresolvedListingId));
        (await ReadStatusAsync(dbContext, validListingId))
            .Should().Be(ListingStatus.Draft);
        (await ReadStatusAsync(dbContext, unresolvedListingId))
            .Should().Be(ListingStatus.Draft);

        await ConfirmRootAsync(dbContext, unresolvedListingId);
        await ActivateListingsAsync(
            dbContext,
            validListingId,
            unresolvedListingId);
        (await ReadStatusAsync(dbContext, validListingId))
            .Should().Be(ListingStatus.Active);
        (await ReadStatusAsync(dbContext, unresolvedListingId))
            .Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task MigrationLifecycle_FreshRepeatDownExact13FAndReUpHasTwentyEntries()
    {
        await using IsolatedMigrationDatabase control =
            await CreateIsolatedMigrationDatabaseAsync();
        await using IsolatedMigrationDatabase subject =
            await CreateIsolatedMigrationDatabaseAsync();

        IntegrityCatalog thirteenFCatalog;
        await using (RealEstateDbContext controlContext = control.CreateContext())
        {
            IMigrator migrator = controlContext.GetService<IMigrator>();
            await migrator.MigrateAsync(ThirteenFMigration);
            thirteenFCatalog = await ReadIntegrityCatalogAsync(controlContext);
        }

        await using (RealEstateDbContext subjectContext = subject.CreateContext())
        {
            IMigrator migrator = subjectContext.GetService<IMigrator>();

            await migrator.MigrateAsync(CurrentMigration);
            (await CountAppliedMigrationsAsync(subjectContext)).Should().Be(20);
            (await ReadIntegrityCatalogAsync(subjectContext)).Triggers[
                    "TR_Listings_ActivePublicationIntegrity_Update"]
                .Definition.Should().Contain("OLD TABLE AS old_listings");

            await migrator.MigrateAsync(CurrentMigration);
            (await CountAppliedMigrationsAsync(subjectContext)).Should().Be(20);

            await migrator.MigrateAsync(PreviousMigration);
            (await CountAppliedMigrationsAsync(subjectContext)).Should().Be(19);
            IntegrityCatalog downCatalog =
                await ReadIntegrityCatalogAsync(subjectContext);
            downCatalog.Should().BeEquivalentTo(thirteenFCatalog);

            Guid listingId = await InsertDraftListingAsync(subjectContext);
            Guid translationId = await InsertTranslationAsync(
                subjectContext,
                listingId,
                municipality: null,
                addressLine: null);
            await SetStatusAsync(
                subjectContext,
                listingId,
                ListingStatus.Active);
            await ConfirmRootAsync(subjectContext, listingId);
            (await ReadStatusAsync(subjectContext, listingId))
                .Should().Be(ListingStatus.Active);

            await AssertTranslationFreezeRejectedAsync(
                () => subjectContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "Title" = 'Forbidden active change'
                     WHERE "Id" = {translationId}
                     """));

            Guid cascadeListingId =
                await InsertDraftListingAsync(subjectContext);
            await InsertTranslationAsync(
                subjectContext,
                cascadeListingId,
                municipality: null,
                addressLine: null);
            await SetStatusAsync(
                subjectContext,
                cascadeListingId,
                ListingStatus.Active);
            await subjectContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DELETE FROM "Listings"
                 WHERE "Id" = {cascadeListingId}
                 """);
            (await CountTranslationsAsync(
                    subjectContext,
                    cascadeListingId))
                .Should().Be(0);

            await SetStatusAsync(
                subjectContext,
                listingId,
                ListingStatus.Draft);
            long xminBefore = await ReadXminAsync(subjectContext, listingId);
            await subjectContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE "ListingTranslations"
                 SET "Municipality" = 'Centar',
                     "AddressLine" = 'Address'
                 WHERE "Id" = {translationId}
                 """);
            long xminAfter = await ReadXminAsync(subjectContext, listingId);
            xminAfter.Should().NotBe(xminBefore);
            await SetStatusAsync(
                subjectContext,
                listingId,
                ListingStatus.Active);

            await migrator.MigrateAsync(CurrentMigration);
            (await CountAppliedMigrationsAsync(subjectContext)).Should().Be(20);
            (await ReadStatusAsync(subjectContext, listingId))
                .Should().Be(ListingStatus.Active);
        }
    }

    [Fact]
    public async Task IncompatibleUpgrade_FailsAtomicallyWithoutRepairOrHistoryEntry()
    {
        await using IsolatedMigrationDatabase database =
            await CreateIsolatedMigrationDatabaseAsync();
        Guid listingId = Guid.NewGuid();

        await using (RealEstateDbContext setupContext = database.CreateContext())
        {
            IMigrator migrator = setupContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertDraftListingAsync(setupContext, listingId);
            await InsertTranslationAsync(
                setupContext,
                listingId,
                municipality: null,
                addressLine: null);
            await SetStatusAsync(
                setupContext,
                listingId,
                ListingStatus.Active);
        }

        await using (RealEstateDbContext migrationContext = database.CreateContext())
        {
            IMigrator migrator = migrationContext.GetService<IMigrator>();
            Func<Task> action = () => migrator.MigrateAsync(CurrentMigration);

            await AssertIntegrityRejectedAsync(action);
        }

        await using (RealEstateDbContext verificationContext =
                     database.CreateContext())
        {
            (await CountAppliedMigrationsAsync(verificationContext))
                .Should().Be(19);
            (await ReadMigrationAppliedAsync(
                    verificationContext,
                    CurrentMigration))
                .Should().BeFalse();
            (await ReadStatusAsync(verificationContext, listingId))
                .Should().Be(ListingStatus.Active);
            TranslationLocation translation =
                await ReadTranslationLocationAsync(
                    verificationContext,
                    listingId);
            translation.Should().Be(new TranslationLocation(null, null));
            (await ReadLocationSnapshotAsync(
                    verificationContext,
                    listingId))
                .Should().Be(LocationSnapshot.Unresolved);

            IntegrityCatalog catalog =
                await ReadIntegrityCatalogAsync(verificationContext);
            catalog.Functions[
                    "re_assert_active_listing_publication_integrity"]
                .Definition.Should().NotContain("Municipality");
            catalog.Functions[
                    "re_assert_active_listing_publication_integrity"]
                .Definition.Should().NotContain("Latitude");
            catalog.Triggers[
                    "TR_Listings_ActivePublicationIntegrity_Update"]
                .Definition.Should().Contain("NEW TABLE AS new_listings")
                .And.NotContain("OLD TABLE AS old_listings");
        }
    }

    private static readonly string[] RootSnapshotFields =
    [
        "Latitude",
        "Longitude",
        "LocationPrecision",
        "GeocodingProviderKey",
        "GeocodingResultReference",
        "GeocodedDisplayName",
        "LocationConfirmedAtUtc"
    ];

    private static string ConfirmedAssignment(
        string latitude = "41.9981",
        string longitude = "21.4254",
        string precision = "'ExactAddress'",
        string providerKey = "'test-provider'",
        string resultReference = "'test-reference'",
        string displayName = "NULL",
        string confirmationTime =
            "TIMESTAMPTZ '2026-08-24 12:00:00+00'")
    {
        return
            $"\"Latitude\" = {latitude}, " +
            $"\"Longitude\" = {longitude}, " +
            $"\"LocationPrecision\" = {precision}, " +
            $"\"GeocodingProviderKey\" = {providerKey}, " +
            $"\"GeocodingResultReference\" = {resultReference}, " +
            $"\"GeocodedDisplayName\" = {displayName}, " +
            $"\"LocationConfirmedAtUtc\" = {confirmationTime}";
    }

    private static async Task<Guid> InsertPublishableDraftAsync(
        RealEstateDbContext dbContext)
    {
        Guid listingId = await InsertDraftListingAsync(dbContext);
        await InsertTranslationAsync(dbContext, listingId);
        return listingId;
    }

    private static Task<Guid> InsertDraftListingAsync(
        RealEstateDbContext dbContext)
    {
        return InsertDraftListingAsync(dbContext, Guid.NewGuid());
    }

    private static async Task<Guid> InsertDraftListingAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Listings"
                 ("Id", "ListingType", "PropertyType", "Status", "Price",
                  "Currency", "AreaSquareMeters", "CreatedAtUtc")
             VALUES
                 ({listingId}, 'Sale', 'Apartment', 'Draft', 100000,
                  'EUR', 80, {DateTime.UtcNow})
             """);
        return listingId;
    }

    private static async Task<Guid> InsertTranslationAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        string languageCode = "en",
        string title = "Valid title",
        string? city = "Skopje",
        string? description = "Valid description",
        string? municipality = "Centar",
        string? addressLine = "Address")
    {
        Guid translationId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ListingTranslations"
                 ("Id", "ListingId", "LanguageCode", "Title", "City",
                  "Municipality", "AddressLine", "Description")
             VALUES
                 ({translationId}, {listingId}, {languageCode}, {title}, {city},
                  {municipality}, {addressLine}, {description})
             """);
        return translationId;
    }

    private static Task InsertTranslationWithOverrideAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        string field,
        string invalidValue)
    {
        return InsertTranslationAsync(
            dbContext,
            listingId,
            languageCode: field == "LanguageCode" ? invalidValue : "en",
            title: field == "Title" ? invalidValue : "Valid title",
            city: field == "City" ? invalidValue : "Skopje",
            description:
                field == "Description" ? invalidValue : "Valid description",
            municipality:
                field == "Municipality" ? invalidValue : "Centar",
            addressLine: field == "AddressLine" ? invalidValue : "Address");
    }

    private static Task ConfirmRootAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        decimal latitude = 41.9981m,
        decimal longitude = 21.4254m,
        string precision = "ExactAddress",
        string? displayName = null)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Latitude" = {latitude},
                 "Longitude" = {longitude},
                 "LocationPrecision" = {precision},
                 "GeocodingProviderKey" = 'test-provider',
                 "GeocodingResultReference" = 'test-reference',
                 "GeocodedDisplayName" = {displayName},
                 "LocationConfirmedAtUtc" = {new DateTime(
                     2026,
                     8,
                     24,
                     12,
                     0,
                     0,
                     DateTimeKind.Utc)}
             WHERE "Id" = {listingId}
             """);
    }

    private static Task SetStatusAsync(
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

    private static Task ActivateListingsAsync(
        RealEstateDbContext dbContext,
        params Guid[] listingIds)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Status" = 'Active'
             WHERE "Id" = ANY({listingIds})
             """);
    }

    private static async Task ExecuteListingAssignmentAsync(
        RealEstateDbContext dbContext,
        Guid listingId,
        string assignment)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            $"UPDATE public.\"Listings\" SET {assignment} WHERE \"Id\" = @listing_id;";
        AddGuidParameter(command, "listing_id", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private static Task<ListingStatus> ReadStatusAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => listing.Status)
            .SingleAsync();
    }

    private static Task<int> CountTranslationsAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return dbContext.Set<RealEstate.Domain.Entities.ListingTranslation>()
            .AsNoTracking()
            .CountAsync(translation => translation.ListingId == listingId);
    }

    private static Task<TranslationLocation> ReadTranslationLocationAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return dbContext.Set<RealEstate.Domain.Entities.ListingTranslation>()
            .AsNoTracking()
            .Where(translation => translation.ListingId == listingId)
            .Select(translation => new TranslationLocation(
                translation.Municipality,
                translation.AddressLine))
            .SingleAsync();
    }

    private static Task<LocationSnapshot> ReadLocationSnapshotAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new LocationSnapshot(
                listing.Latitude,
                listing.Longitude,
                listing.LocationPrecision == null
                    ? null
                    : listing.LocationPrecision.ToString(),
                listing.GeocodingProviderKey,
                listing.GeocodingResultReference,
                listing.GeocodedDisplayName,
                listing.LocationConfirmedAtUtc))
            .SingleAsync();
    }

    private static async Task<long> ReadXminAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
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
            WHERE "Id" = @listing_id;
            """;
        AddGuidParameter(command, "listing_id", listingId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountAppliedMigrationsAsync(
        RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT count(*)::integer FROM public.\"__EFMigrationsHistory\";";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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

    private static async Task<IntegrityCatalog> ReadIntegrityCatalogAsync(
        RealEstateDbContext dbContext)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var triggers = new Dictionary<string, TriggerCatalogRow>(
            StringComparer.Ordinal);
        await using (DbCommand triggerCommand = connection.CreateCommand())
        {
            triggerCommand.CommandText =
                """
                SELECT trigger.tgname,
                       table_class.relname,
                       (trigger.tgtype & 1) = 0,
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
            triggerCommand.Parameters.Add(new NpgsqlParameter<string[]>(
                "trigger_names",
                J4TriggerExpectations.Keys.ToArray()));
            await using DbDataReader reader =
                await triggerCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                triggers.Add(
                    reader.GetString(0),
                    new TriggerCatalogRow(
                        reader.GetString(1),
                        reader.GetBoolean(2),
                        reader.GetString(3)));
            }
        }

        var functions = new Dictionary<string, FunctionCatalogRow>(
            StringComparer.Ordinal);
        await using (DbCommand functionCommand = connection.CreateCommand())
        {
            functionCommand.CommandText =
                """
                SELECT procedure.proname,
                       pg_get_functiondef(procedure.oid),
                       COALESCE(array_to_string(procedure.proconfig, ','), '')
                FROM pg_catalog.pg_proc AS procedure
                JOIN pg_catalog.pg_namespace AS function_namespace
                  ON function_namespace.oid = procedure.pronamespace
                WHERE function_namespace.nspname = 'public'
                  AND procedure.proname = ANY(@function_names)
                ORDER BY procedure.proname;
                """;
            functionCommand.Parameters.Add(new NpgsqlParameter<string[]>(
                "function_names",
                FunctionNames));
            await using DbDataReader reader =
                await functionCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                functions.Add(
                    reader.GetString(0),
                    new FunctionCatalogRow(
                        reader.GetString(1),
                        reader.GetString(2)));
            }
        }

        return new IntegrityCatalog(triggers, functions);
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

    private static async Task AssertIntegrityRejectedAsync(Func<Task> action)
    {
        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.MessageText.Should().Be(IntegrityViolationMessage);
    }

    private static async Task AssertTranslationFreezeRejectedAsync(
        Func<Task> action)
    {
        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.MessageText.Should().Be(TranslationFreezeMessage);
    }

    private static async Task AssertLocationFreezeRejectedAsync(
        Func<Task> action,
        string fieldFamily)
    {
        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>(
                $"Active {fieldFamily} mutation must be frozen")).Which;
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.MessageText.Should().Be(LocationFreezeMessage);
    }

    private static async Task AssertPostgreSqlRejectedAsync(
        Func<Task> action,
        string? because = null)
    {
        await action.Should().ThrowAsync<PostgresException>(because);
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
        string databaseName = $"re_13j4_{Guid.NewGuid():N}";
        var targetBuilder = new NpgsqlConnectionStringBuilder(
            mainConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        await using var adminConnection = new NpgsqlConnection(
            mainConnectionString);
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
            if (!databaseName.StartsWith("re_13j4_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to drop a database outside the isolated J.4 test namespace.");
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
            command.CommandText =
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE);";
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

    private sealed record FunctionCatalogRow(
        string Definition,
        string Configuration);

    private sealed record IntegrityCatalog(
        IReadOnlyDictionary<string, TriggerCatalogRow> Triggers,
        IReadOnlyDictionary<string, FunctionCatalogRow> Functions);

    private sealed record TranslationLocation(
        string? Municipality,
        string? AddressLine);

    private sealed record LocationSnapshot(
        decimal? Latitude,
        decimal? Longitude,
        string? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc)
    {
        public static LocationSnapshot Unresolved { get; } = new(
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
