using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlStrongActiveDirectIntegrityMatrixTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string IntegrityViolationMessage =
        "Active listing publication integrity violation.";
    private const string TranslationFreezeMessage =
        "Active listing translations are immutable; unpublish before editing.";
    private const string LocationFreezeMessage =
        "Active listing location is immutable; unpublish before resolving.";

    private const string LanguageConstraint =
        "CK_ListingTranslations_LanguageCode_Canonical";
    private const string TitleConstraint =
        "CK_ListingTranslations_Title_TrimmedNonBlank";
    private const string CityConstraint =
        "CK_ListingTranslations_City_TrimmedNonBlank";
    private const string MunicipalityConstraint =
        "CK_ListingTranslations_Municipality_TrimmedNonBlank";
    private const string AddressLineConstraint =
        "CK_ListingTranslations_AddressLine_TrimmedNonBlank";
    private const string DescriptionConstraint =
        "CK_ListingTranslations_Description_TrimmedNonBlank";
    private const string CoordinatePairConstraint =
        "CK_Listings_Location_CoordinatePair";
    private const string LatitudeRangeConstraint =
        "CK_Listings_Location_LatitudeRange";
    private const string LongitudeRangeConstraint =
        "CK_Listings_Location_LongitudeRange";
    private const string PrecisionConstraint =
        "CK_Listings_Location_PrecisionDefined";
    private const string ProviderConstraint =
        "CK_Listings_Location_ProviderKeyTrimmedNonBlank";
    private const string ResultReferenceConstraint =
        "CK_Listings_Location_ResultReferenceTrimmedNonBlank";
    private const string DisplayNameConstraint =
        "CK_Listings_Location_DisplayNameTrimmedNonBlank";
    private const string SnapshotStateConstraint =
        "CK_Listings_Location_SnapshotState";

    private const string OwnerNotNull = "not_null";
    private const string OwnerRowCheck = "row_check";
    private const string OwnerAggregate = "j4_aggregate";
    private const string OwnerColumnLength = "column_length";

    private readonly CustomWebApplicationFactory _factory;

    public PostgreSqlStrongActiveDirectIntegrityMatrixTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string?, string, string> TranslationCases =>
        new()
        {
            { "LanguageCode", null, OwnerNotNull, "LanguageCode" },
            { "LanguageCode", "", OwnerRowCheck, LanguageConstraint },
            { "LanguageCode", "\u3000", OwnerRowCheck, LanguageConstraint },
            { "LanguageCode", "EN", OwnerRowCheck, LanguageConstraint },
            { "LanguageCode", " en ", OwnerRowCheck, LanguageConstraint },
            { "LanguageCode", "e", OwnerRowCheck, LanguageConstraint },
            { "Title", null, OwnerNotNull, "Title" },
            { "Title", "", OwnerRowCheck, TitleConstraint },
            { "Title", "\u00A0", OwnerRowCheck, TitleConstraint },
            { "Title", " Title ", OwnerRowCheck, TitleConstraint },
            { "City", null, OwnerAggregate, IntegrityViolationMessage },
            { "City", "", OwnerRowCheck, CityConstraint },
            { "City", "\t", OwnerRowCheck, CityConstraint },
            { "City", " City ", OwnerRowCheck, CityConstraint },
            {
                "Municipality",
                null,
                OwnerAggregate,
                IntegrityViolationMessage
            },
            {
                "Municipality",
                "",
                OwnerRowCheck,
                MunicipalityConstraint
            },
            {
                "Municipality",
                "\u2003",
                OwnerRowCheck,
                MunicipalityConstraint
            },
            {
                "Municipality",
                " Municipality ",
                OwnerRowCheck,
                MunicipalityConstraint
            },
            {
                "AddressLine",
                null,
                OwnerAggregate,
                IntegrityViolationMessage
            },
            {
                "AddressLine",
                "",
                OwnerRowCheck,
                AddressLineConstraint
            },
            {
                "AddressLine",
                "\r\n",
                OwnerRowCheck,
                AddressLineConstraint
            },
            {
                "AddressLine",
                " Address ",
                OwnerRowCheck,
                AddressLineConstraint
            },
            {
                "Description",
                null,
                OwnerAggregate,
                IntegrityViolationMessage
            },
            {
                "Description",
                "",
                OwnerRowCheck,
                DescriptionConstraint
            },
            {
                "Description",
                "\u3000",
                OwnerRowCheck,
                DescriptionConstraint
            },
            {
                "Description",
                " Description ",
                OwnerRowCheck,
                DescriptionConstraint
            }
        };

    public static TheoryData<string, string> CanonicalActiveTranslationMutations =>
        new()
        {
            { "LanguageCode", "mk" },
            { "Title", "Changed title" },
            { "City", "Ohrid" },
            { "Municipality", "Karpos" },
            { "AddressLine", "Changed address" },
            { "Description", "Changed description" }
        };

    public static TheoryData<string, string?, string, string> InvalidRootCases =>
        new()
        {
            {
                "unresolved",
                null,
                OwnerAggregate,
                IntegrityViolationMessage
            },
            {
                "legacy_unverified",
                "\"Latitude\" = 41.9981, \"Longitude\" = 21.4254",
                OwnerAggregate,
                IntegrityViolationMessage
            },
            {
                "latitude_only",
                "\"Latitude\" = 41.9981",
                OwnerRowCheck,
                CoordinatePairConstraint
            },
            {
                "longitude_only",
                "\"Longitude\" = 21.4254",
                OwnerRowCheck,
                CoordinatePairConstraint
            },
            {
                "metadata_without_coordinates",
                "\"LocationPrecision\" = 'City', " +
                "\"GeocodingProviderKey\" = 'provider', " +
                "\"GeocodingResultReference\" = 'reference', " +
                "\"LocationConfirmedAtUtc\" = " +
                "TIMESTAMPTZ '2026-08-26 12:00:00+00'",
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "display_without_coordinates",
                "\"GeocodedDisplayName\" = 'Display'",
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "coordinates_and_precision_only",
                "\"Latitude\" = 41.9981, \"Longitude\" = 21.4254, " +
                "\"LocationPrecision\" = 'City'",
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "missing_precision",
                ConfirmedAssignment(precision: "NULL"),
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "undefined_precision",
                ConfirmedAssignment(precision: "'Parcel'"),
                OwnerRowCheck,
                PrecisionConstraint
            },
            {
                "missing_provider_key",
                ConfirmedAssignment(providerKey: "NULL"),
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "empty_provider_key",
                ConfirmedAssignment(providerKey: "''"),
                OwnerRowCheck,
                ProviderConstraint
            },
            {
                "whitespace_provider_key",
                ConfirmedAssignment(providerKey: "chr(8195)"),
                OwnerRowCheck,
                ProviderConstraint
            },
            {
                "untrimmed_provider_key",
                ConfirmedAssignment(
                    providerKey: "chr(160) || 'provider'"),
                OwnerRowCheck,
                ProviderConstraint
            },
            {
                "overlength_provider_key",
                ConfirmedAssignment(providerKey: "repeat('p', 65)"),
                OwnerColumnLength,
                "GeocodingProviderKey"
            },
            {
                "missing_result_reference",
                ConfirmedAssignment(resultReference: "NULL"),
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "empty_result_reference",
                ConfirmedAssignment(resultReference: "''"),
                OwnerRowCheck,
                ResultReferenceConstraint
            },
            {
                "whitespace_result_reference",
                ConfirmedAssignment(resultReference: "chr(12288)"),
                OwnerRowCheck,
                ResultReferenceConstraint
            },
            {
                "untrimmed_result_reference",
                ConfirmedAssignment(
                    resultReference: "'reference' || chr(8239)"),
                OwnerRowCheck,
                ResultReferenceConstraint
            },
            {
                "overlength_result_reference",
                ConfirmedAssignment(resultReference: "repeat('r', 513)"),
                OwnerColumnLength,
                "GeocodingResultReference"
            },
            {
                "missing_confirmation_time",
                ConfirmedAssignment(confirmationTime: "NULL"),
                OwnerRowCheck,
                SnapshotStateConstraint
            },
            {
                "latitude_below_range",
                ConfirmedAssignment(latitude: "-90.000001"),
                OwnerRowCheck,
                LatitudeRangeConstraint
            },
            {
                "latitude_above_range",
                ConfirmedAssignment(latitude: "90.000001"),
                OwnerRowCheck,
                LatitudeRangeConstraint
            },
            {
                "longitude_below_range",
                ConfirmedAssignment(longitude: "-180.000001"),
                OwnerRowCheck,
                LongitudeRangeConstraint
            },
            {
                "longitude_above_range",
                ConfirmedAssignment(longitude: "180.000001"),
                OwnerRowCheck,
                LongitudeRangeConstraint
            },
            {
                "empty_display_name",
                ConfirmedAssignment(displayName: "''"),
                OwnerRowCheck,
                DisplayNameConstraint
            },
            {
                "whitespace_display_name",
                ConfirmedAssignment(displayName: "chr(133)"),
                OwnerRowCheck,
                DisplayNameConstraint
            },
            {
                "untrimmed_display_name",
                ConfirmedAssignment(displayName: "'display' || chr(12288)"),
                OwnerRowCheck,
                DisplayNameConstraint
            },
            {
                "overlength_display_name",
                ConfirmedAssignment(displayName: "repeat('d', 501)"),
                OwnerColumnLength,
                "GeocodedDisplayName"
            }
        };

    public static TheoryData<string, string> AcceptedRootCases => new()
    {
        {
            "minimum_coordinate_boundaries",
            ConfirmedAssignment(latitude: "-90", longitude: "-180")
        },
        {
            "maximum_coordinate_boundaries",
            ConfirmedAssignment(latitude: "90", longitude: "180")
        },
        { "precision_exact_address", ConfirmedAssignment() },
        {
            "precision_street",
            ConfirmedAssignment(precision: "'Street'")
        },
        {
            "precision_neighborhood",
            ConfirmedAssignment(precision: "'Neighborhood'")
        },
        {
            "precision_municipality",
            ConfirmedAssignment(precision: "'Municipality'")
        },
        { "precision_city", ConfirmedAssignment(precision: "'City'") },
        {
            "precision_approximate",
            ConfirmedAssignment(precision: "'Approximate'")
        },
        { "null_display_name", ConfirmedAssignment(displayName: "NULL") },
        {
            "canonical_display_name",
            ConfirmedAssignment(displayName: "'Canonical display'")
        },
        {
            "maximum_unicode_scalar_lengths",
            ConfirmedAssignment(
                providerKey: "repeat(chr(128512), 64)",
                resultReference: "repeat(chr(128512), 512)",
                displayName: "repeat(chr(128512), 500)")
        }
    };

    public static TheoryData<string, string> ActiveRootMutations => new()
    {
        { "latitude", "\"Latitude\" = 42.1" },
        { "longitude", "\"Longitude\" = 22.1" },
        { "precision", "\"LocationPrecision\" = 'Street'" },
        {
            "provider_key",
            "\"GeocodingProviderKey\" = 'changed-provider'"
        },
        {
            "result_reference",
            "\"GeocodingResultReference\" = 'changed-reference'"
        },
        {
            "display_name",
            "\"GeocodedDisplayName\" = 'Changed display'"
        },
        {
            "confirmation_time",
            "\"LocationConfirmedAtUtc\" = " +
            "TIMESTAMPTZ '2026-08-27 12:00:00+00'"
        }
    };

    public static TheoryData<bool, bool> ListingIdMovementCases => new()
    {
        { false, false },
        { true, false },
        { false, true },
        { true, true }
    };

    [Theory]
    [MemberData(nameof(TranslationCases))]
    public async Task TranslationDirectMatrix_RejectsAtOwningLayerAndNeverCommitsMalformedActive(
        string field,
        string? value,
        string owner,
        string diagnostic)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertPublishableDraftAsync(dbContext);
        string? original = await ReadTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            field);

        PostgresException exception;
        if (owner == OwnerAggregate)
        {
            await UpdateTranslationFieldAsync(
                dbContext,
                seed.TranslationId,
                field,
                value);
            exception = await CapturePostgresExceptionAsync(
                () => SetStatusAsync(
                    dbContext,
                    seed.ListingId,
                    ListingStatus.Active));
        }
        else
        {
            exception = await CapturePostgresExceptionAsync(
                () => UpdateTranslationFieldAsync(
                    dbContext,
                    seed.TranslationId,
                    field,
                    value));
        }

        AssertOwnedDiagnostic(exception, owner, diagnostic);
        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Draft);
        (await CountMalformedActiveTranslationsAsync(
                dbContext,
                seed.ListingId))
            .Should().Be(0);

        string? actual = await ReadTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            field);
        actual.Should().Be(owner == OwnerAggregate ? value : original);
    }

    [Theory]
    [MemberData(nameof(CanonicalActiveTranslationMutations))]
    public async Task ActiveTranslationCanonicalMutation_IsFrozenAndUnchanged(
        string field,
        string changedValue)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertValidActiveAsync(dbContext);
        string? before = await ReadTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            field);

        PostgresException exception = await CapturePostgresExceptionAsync(
            () => UpdateTranslationFieldAsync(
                dbContext,
                seed.TranslationId,
                field,
                changedValue));

        AssertTriggerDiagnostic(exception, TranslationFreezeMessage);
        (await ReadTranslationFieldAsync(
                dbContext,
                seed.TranslationId,
                field))
            .Should().Be(before);
        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Active);
    }

    [Theory]
    [MemberData(nameof(InvalidRootCases))]
    public async Task RootDirectMatrix_RejectsAtOwningLayerAndNeverCommitsMalformedActive(
        string caseName,
        string? assignment,
        string owner,
        string diagnostic)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertDraftWithTranslationAsync(dbContext);
        RootSnapshot unresolved = await ReadRootSnapshotAsync(
            dbContext,
            seed.ListingId);
        RootSnapshot beforeRejectedOperation = unresolved;

        PostgresException exception;
        if (owner == OwnerAggregate)
        {
            if (assignment is not null)
            {
                await ExecuteRootAssignmentAsync(
                    dbContext,
                    seed.ListingId,
                    assignment);
                beforeRejectedOperation = await ReadRootSnapshotAsync(
                    dbContext,
                    seed.ListingId);
            }

            exception = await CapturePostgresExceptionAsync(
                () => SetStatusAsync(
                    dbContext,
                    seed.ListingId,
                    ListingStatus.Active));
        }
        else
        {
            exception = await CapturePostgresExceptionAsync(
                () => ExecuteRootAssignmentAsync(
                    dbContext,
                    seed.ListingId,
                    assignment ?? throw new InvalidOperationException(
                        $"Missing assignment for {caseName}.")));
        }

        AssertOwnedDiagnostic(exception, owner, diagnostic);
        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Draft, caseName);
        (await CountMalformedActiveRootsAsync(dbContext, seed.ListingId))
            .Should().Be(0, caseName);

        RootSnapshot actual = await ReadRootSnapshotAsync(
            dbContext,
            seed.ListingId);
        actual.Should().Be(beforeRejectedOperation, caseName);
    }

    [Theory]
    [MemberData(nameof(AcceptedRootCases))]
    public async Task RootAcceptedMatrix_CommitsExactValidBoundaryState(
        string caseName,
        string assignment)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertDraftWithTranslationAsync(dbContext);

        await ExecuteRootAssignmentAsync(
            dbContext,
            seed.ListingId,
            assignment);
        await SetStatusAsync(
            dbContext,
            seed.ListingId,
            ListingStatus.Active);

        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Active, caseName);
        (await CountMalformedActiveRootsAsync(dbContext, seed.ListingId))
            .Should().Be(0, caseName);
        RootSnapshot snapshot = await ReadRootSnapshotAsync(
            dbContext,
            seed.ListingId);
        AssertAcceptedRootCase(snapshot, caseName);
    }

    [Theory]
    [MemberData(nameof(ActiveRootMutations))]
    public async Task ActiveRootMutationMatrix_RejectsAndPreservesCompleteSnapshot(
        string fieldFamily,
        string assignment)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertValidActiveAsync(dbContext);
        RootSnapshot before = await ReadRootSnapshotAsync(
            dbContext,
            seed.ListingId);

        PostgresException exception = await CapturePostgresExceptionAsync(
            () => ExecuteRootAssignmentAsync(
                dbContext,
                seed.ListingId,
                assignment));

        AssertTriggerDiagnostic(exception, LocationFreezeMessage);
        (await ReadRootSnapshotAsync(dbContext, seed.ListingId))
            .Should().Be(before, fieldFamily);
        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task ActiveRootNoOpAssignment_DoesNotProduceFalsePositive()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertValidActiveAsync(dbContext);
        RootSnapshot before = await ReadRootSnapshotAsync(
            dbContext,
            seed.ListingId);

        await ExecuteRootAssignmentAsync(
            dbContext,
            seed.ListingId,
            "\"Latitude\" = \"Latitude\", " +
            "\"Longitude\" = \"Longitude\", " +
            "\"LocationPrecision\" = \"LocationPrecision\", " +
            "\"GeocodingProviderKey\" = \"GeocodingProviderKey\", " +
            "\"GeocodingResultReference\" = \"GeocodingResultReference\", " +
            "\"GeocodedDisplayName\" = \"GeocodedDisplayName\", " +
            "\"LocationConfirmedAtUtc\" = \"LocationConfirmedAtUtc\"");

        (await ReadRootSnapshotAsync(dbContext, seed.ListingId))
            .Should().Be(before);
    }

    [Fact]
    public async Task UnpublishRepairAndRepublish_AllowsDraftTranslationAndRootChange()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertValidActiveAsync(dbContext);

        await SetStatusAsync(
            dbContext,
            seed.ListingId,
            ListingStatus.Draft);
        await UpdateTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            "Municipality",
            null);
        await UpdateTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            "AddressLine",
            null);
        await ExecuteRootAssignmentAsync(
            dbContext,
            seed.ListingId,
            "\"Latitude\" = NULL, \"Longitude\" = NULL, " +
            "\"LocationPrecision\" = NULL, " +
            "\"GeocodingProviderKey\" = NULL, " +
            "\"GeocodingResultReference\" = NULL, " +
            "\"GeocodedDisplayName\" = NULL, " +
            "\"LocationConfirmedAtUtc\" = NULL");

        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Draft);
        await UpdateTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            "Municipality",
            "Aerodrom");
        await UpdateTranslationFieldAsync(
            dbContext,
            seed.TranslationId,
            "AddressLine",
            "Repaired address");
        await ExecuteRootAssignmentAsync(
            dbContext,
            seed.ListingId,
            ConfirmedAssignment(
                latitude: "42.004",
                longitude: "21.409",
                precision: "'Street'",
                providerKey: "'repair-provider'",
                resultReference: "'repair-reference'",
                displayName: "'Repaired display'"));
        await SetStatusAsync(
            dbContext,
            seed.ListingId,
            ListingStatus.Active);

        (await ReadStatusAsync(dbContext, seed.ListingId))
            .Should().Be(ListingStatus.Active);
        (await CountMalformedActiveTranslationsAsync(
                dbContext,
                seed.ListingId))
            .Should().Be(0);
        (await CountMalformedActiveRootsAsync(dbContext, seed.ListingId))
            .Should().Be(0);
    }

    [Fact]
    public async Task MultiRowActivation_MixedValidityRejectsEntireStatement()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed valid = await InsertPublishableDraftAsync(dbContext);
        ListingSeed unresolved = await InsertDraftWithTranslationAsync(dbContext);
        RootSnapshot validRoot = await ReadRootSnapshotAsync(
            dbContext,
            valid.ListingId);
        RootSnapshot unresolvedRoot = await ReadRootSnapshotAsync(
            dbContext,
            unresolved.ListingId);

        PostgresException exception = await CapturePostgresExceptionAsync(
            () => SetStatusesActiveAsync(
                dbContext,
                [valid.ListingId, unresolved.ListingId]));

        AssertTriggerDiagnostic(exception, IntegrityViolationMessage);
        (await ReadStatusesAsync(
                dbContext,
                [valid.ListingId, unresolved.ListingId]))
            .Should().OnlyContain(status => status == ListingStatus.Draft);
        (await ReadRootSnapshotAsync(dbContext, valid.ListingId))
            .Should().Be(validRoot);
        (await ReadRootSnapshotAsync(dbContext, unresolved.ListingId))
            .Should().Be(unresolvedRoot);
    }

    [Fact]
    public async Task MultiRowRootMutation_ActiveFailureRollsBackValidDraftSibling()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed active = await InsertValidActiveAsync(dbContext);
        ListingSeed draft = await InsertPublishableDraftAsync(dbContext);
        RootSnapshot activeBefore = await ReadRootSnapshotAsync(
            dbContext,
            active.ListingId);
        RootSnapshot draftBefore = await ReadRootSnapshotAsync(
            dbContext,
            draft.ListingId);

        PostgresException exception = await CapturePostgresExceptionAsync(
            () => UpdateProviderKeysAsync(
                dbContext,
                [active.ListingId, draft.ListingId],
                "batch-provider"));

        AssertTriggerDiagnostic(exception, LocationFreezeMessage);
        (await ReadRootSnapshotAsync(dbContext, active.ListingId))
            .Should().Be(activeBefore);
        (await ReadRootSnapshotAsync(dbContext, draft.ListingId))
            .Should().Be(draftBefore);
    }

    [Fact]
    public async Task MultiRowTranslationMutation_ActiveFailureRollsBackValidDraftSibling()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed active = await InsertValidActiveAsync(dbContext);
        ListingSeed draft = await InsertPublishableDraftAsync(dbContext);
        string? activeBefore = await ReadTranslationFieldAsync(
            dbContext,
            active.TranslationId,
            "Title");
        string? draftBefore = await ReadTranslationFieldAsync(
            dbContext,
            draft.TranslationId,
            "Title");

        PostgresException exception = await CapturePostgresExceptionAsync(
            () => UpdateTranslationTitlesAsync(
                dbContext,
                [active.TranslationId, draft.TranslationId],
                "Batch title"));

        AssertTriggerDiagnostic(exception, TranslationFreezeMessage);
        (await ReadTranslationFieldAsync(
                dbContext,
                active.TranslationId,
                "Title"))
            .Should().Be(activeBefore);
        (await ReadTranslationFieldAsync(
                dbContext,
                draft.TranslationId,
                "Title"))
            .Should().Be(draftBefore);
    }

    [Fact]
    public async Task ListingDelete_CascadesTranslationsWithoutRequiringDisappearingParent()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed seed = await InsertValidActiveAsync(dbContext);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM "Listings"
             WHERE "Id" = {seed.ListingId}
             """);

        (await dbContext.Listings.AsNoTracking().AnyAsync(
                listing => listing.Id == seed.ListingId))
            .Should().BeFalse();
        (await CountTranslationAsync(dbContext, seed.TranslationId))
            .Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(ListingIdMovementCases))]
    public async Task ListingIdMovement_ValidatesOldAndNewParentsAndFinalState(
        bool sourceActive,
        bool targetActive)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        ListingSeed source = await InsertPublishableDraftAsync(dbContext);
        ListingSeed target = await InsertPublishableDraftAsync(dbContext);
        await UpdateTranslationFieldAsync(
            dbContext,
            target.TranslationId,
            "LanguageCode",
            "mk");
        if (sourceActive)
        {
            await SetStatusAsync(
                dbContext,
                source.ListingId,
                ListingStatus.Active);
        }

        if (targetActive)
        {
            await SetStatusAsync(
                dbContext,
                target.ListingId,
                ListingStatus.Active);
        }

        long sourceXmin = await ReadXminAsync(dbContext, source.ListingId);
        long targetXmin = await ReadXminAsync(dbContext, target.ListingId);

        if (sourceActive || targetActive)
        {
            PostgresException exception = await CapturePostgresExceptionAsync(
                () => MoveTranslationAsync(
                    dbContext,
                    source.TranslationId,
                    target.ListingId));

            AssertTriggerDiagnostic(exception, TranslationFreezeMessage);
            (await ReadTranslationListingIdAsync(
                    dbContext,
                    source.TranslationId))
                .Should().Be(source.ListingId);
            (await CountTranslationsForListingAsync(
                    dbContext,
                    source.ListingId))
                .Should().Be(1);
            (await CountTranslationsForListingAsync(
                    dbContext,
                    target.ListingId))
                .Should().Be(1);
            (await ReadXminAsync(dbContext, source.ListingId))
                .Should().Be(sourceXmin);
            (await ReadXminAsync(dbContext, target.ListingId))
                .Should().Be(targetXmin);
        }
        else
        {
            await MoveTranslationAsync(
                dbContext,
                source.TranslationId,
                target.ListingId);

            (await ReadTranslationListingIdAsync(
                    dbContext,
                    source.TranslationId))
                .Should().Be(target.ListingId);
            (await CountTranslationsForListingAsync(
                    dbContext,
                    source.ListingId))
                .Should().Be(0);
            (await CountTranslationsForListingAsync(
                    dbContext,
                    target.ListingId))
                .Should().Be(2);
            (await ReadXminAsync(dbContext, source.ListingId))
                .Should().NotBe(sourceXmin);
            (await ReadXminAsync(dbContext, target.ListingId))
                .Should().NotBe(targetXmin);
        }
    }

    private static string ConfirmedAssignment(
        string latitude = "41.9981",
        string longitude = "21.4254",
        string precision = "'ExactAddress'",
        string providerKey = "'test-provider'",
        string resultReference = "'test-reference'",
        string displayName = "NULL",
        string confirmationTime =
            "TIMESTAMPTZ '2026-08-26 12:00:00+00'")
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

    private static async Task<ListingSeed> InsertDraftWithTranslationAsync(
        RealEstateDbContext dbContext)
    {
        Guid listingId = await InsertDraftListingAsync(dbContext);
        Guid translationId = await InsertTranslationAsync(dbContext, listingId);
        return new ListingSeed(listingId, translationId);
    }

    private static async Task<ListingSeed> InsertPublishableDraftAsync(
        RealEstateDbContext dbContext)
    {
        ListingSeed seed = await InsertDraftWithTranslationAsync(dbContext);
        await ExecuteRootAssignmentAsync(
            dbContext,
            seed.ListingId,
            ConfirmedAssignment());
        return seed;
    }

    private static async Task<ListingSeed> InsertValidActiveAsync(
        RealEstateDbContext dbContext)
    {
        ListingSeed seed = await InsertPublishableDraftAsync(dbContext);
        await SetStatusAsync(
            dbContext,
            seed.ListingId,
            ListingStatus.Active);
        return seed;
    }

    private static async Task<Guid> InsertDraftListingAsync(
        RealEstateDbContext dbContext)
    {
        Guid listingId = Guid.NewGuid();
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
        Guid listingId)
    {
        Guid translationId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ListingTranslations"
                 ("Id", "ListingId", "LanguageCode", "Title", "City",
                  "Municipality", "AddressLine", "Description")
             VALUES
                 ({translationId}, {listingId}, 'en', 'Valid title', 'Skopje',
                  'Centar', 'Valid address', 'Valid description')
             """);
        return translationId;
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

    private static Task SetStatusesActiveAsync(
        RealEstateDbContext dbContext,
        Guid[] listingIds)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Status" = 'Active'
             WHERE "Id" = ANY({listingIds})
             """);
    }

    private static async Task ExecuteRootAssignmentAsync(
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
            $"UPDATE public.\"Listings\" SET {assignment} " +
            "WHERE \"Id\" = @listing_id;";
        AddGuidParameter(command, "listing_id", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateTranslationFieldAsync(
        RealEstateDbContext dbContext,
        Guid translationId,
        string field,
        string? value)
    {
        string column = TranslationColumn(field);
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            $"UPDATE public.\"ListingTranslations\" SET {column} = @value " +
            "WHERE \"Id\" = @translation_id;";
        DbParameter valueParameter = command.CreateParameter();
        valueParameter.ParameterName = "value";
        valueParameter.DbType = DbType.String;
        valueParameter.Value = value is null ? DBNull.Value : value;
        command.Parameters.Add(valueParameter);
        AddGuidParameter(command, "translation_id", translationId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadTranslationFieldAsync(
        RealEstateDbContext dbContext,
        Guid translationId,
        string field)
    {
        string column = TranslationColumn(field);
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {column} FROM public.\"ListingTranslations\" " +
            "WHERE \"Id\" = @translation_id;";
        AddGuidParameter(command, "translation_id", translationId);
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : (string)value;
    }

    private static string TranslationColumn(string field)
    {
        return field switch
        {
            "LanguageCode" => "\"LanguageCode\"",
            "Title" => "\"Title\"",
            "City" => "\"City\"",
            "Municipality" => "\"Municipality\"",
            "AddressLine" => "\"AddressLine\"",
            "Description" => "\"Description\"",
            _ => throw new ArgumentOutOfRangeException(
                nameof(field),
                field,
                "Unsupported translation field.")
        };
    }

    private static async Task UpdateProviderKeysAsync(
        RealEstateDbContext dbContext,
        Guid[] listingIds,
        string providerKey)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE public.\"Listings\" " +
            "SET \"GeocodingProviderKey\" = @provider_key " +
            "WHERE \"Id\" = ANY(@listing_ids);";
        AddStringParameter(command, "provider_key", providerKey);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>(
            "listing_ids",
            listingIds));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpdateTranslationTitlesAsync(
        RealEstateDbContext dbContext,
        Guid[] translationIds,
        string title)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE public.\"ListingTranslations\" SET \"Title\" = @title " +
            "WHERE \"Id\" = ANY(@translation_ids);";
        AddStringParameter(command, "title", title);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>(
            "translation_ids",
            translationIds));
        await command.ExecuteNonQueryAsync();
    }

    private static Task MoveTranslationAsync(
        RealEstateDbContext dbContext,
        Guid translationId,
        Guid listingId)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "ListingTranslations"
             SET "ListingId" = {listingId}
             WHERE "Id" = {translationId}
             """);
    }

    private static async Task<ListingStatus> ReadStatusAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        string status = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => listing.Status.ToString())
            .SingleAsync();
        return Enum.Parse<ListingStatus>(status);
    }

    private static async Task<ListingStatus[]> ReadStatusesAsync(
        RealEstateDbContext dbContext,
        Guid[] listingIds)
    {
        string[] statuses = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listingIds.Contains(listing.Id))
            .OrderBy(listing => listing.Id)
            .Select(listing => listing.Status.ToString())
            .ToArrayAsync();
        return statuses.Select(Enum.Parse<ListingStatus>).ToArray();
    }

    private static async Task<RootSnapshot> ReadRootSnapshotAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new RootSnapshot(
                listing.Latitude,
                listing.Longitude,
                listing.LocationPrecision,
                listing.GeocodingProviderKey,
                listing.GeocodingResultReference,
                listing.GeocodedDisplayName,
                listing.LocationConfirmedAtUtc))
            .SingleAsync();
    }

    private static async Task<Guid> ReadTranslationListingIdAsync(
        RealEstateDbContext dbContext,
        Guid translationId)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT \"ListingId\" FROM public.\"ListingTranslations\" " +
            "WHERE \"Id\" = @translation_id;";
        AddGuidParameter(command, "translation_id", translationId);
        return (Guid)(await command.ExecuteScalarAsync())!;
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
            "SELECT xmin::text::bigint FROM public.\"Listings\" " +
            "WHERE \"Id\" = @listing_id;";
        AddGuidParameter(command, "listing_id", listingId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static Task<int> CountTranslationAsync(
        RealEstateDbContext dbContext,
        Guid translationId)
    {
        return ExecuteCountAsync(
            dbContext,
            "SELECT count(*)::integer " +
            "FROM public.\"ListingTranslations\" WHERE \"Id\" = @id;",
            translationId);
    }

    private static Task<int> CountTranslationsForListingAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return ExecuteCountAsync(
            dbContext,
            "SELECT count(*)::integer FROM public.\"ListingTranslations\" " +
            "WHERE \"ListingId\" = @id;",
            listingId);
    }

    private static async Task<int> ExecuteCountAsync(
        RealEstateDbContext dbContext,
        string commandText,
        Guid id)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        AddGuidParameter(command, "id", id);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountMalformedActiveTranslationsAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return await ExecuteScalarCountAsync(
            dbContext,
            """
            SELECT count(*)::integer
            FROM public."Listings" AS listing
            JOIN public."ListingTranslations" AS translation
              ON translation."ListingId" = listing."Id"
            WHERE listing."Id" = @listing_id
              AND listing."Status" = 'Active'
              AND (
                  translation."LanguageCode" IS NULL
                  OR translation."LanguageCode" = ''
                  OR translation."Title" IS NULL
                  OR translation."Title" = ''
                  OR translation."City" IS NULL
                  OR translation."Municipality" IS NULL
                  OR translation."AddressLine" IS NULL
                  OR translation."Description" IS NULL
              );
            """,
            listingId);
    }

    private static async Task<int> CountMalformedActiveRootsAsync(
        RealEstateDbContext dbContext,
        Guid listingId)
    {
        return await ExecuteScalarCountAsync(
            dbContext,
            """
            SELECT count(*)::integer
            FROM public."Listings" AS listing
            WHERE listing."Id" = @listing_id
              AND listing."Status" = 'Active'
              AND (
                  listing."Latitude" IS NULL
                  OR listing."Longitude" IS NULL
                  OR listing."LocationPrecision" IS NULL
                  OR listing."GeocodingProviderKey" IS NULL
                  OR listing."GeocodingResultReference" IS NULL
                  OR listing."LocationConfirmedAtUtc" IS NULL
              );
            """,
            listingId);
    }

    private static async Task<int> ExecuteScalarCountAsync(
        RealEstateDbContext dbContext,
        string commandText,
        Guid listingId)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        AddGuidParameter(command, "listing_id", listingId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<PostgresException> CapturePostgresExceptionAsync(
        Func<Task> action)
    {
        return (await action.Should().ThrowAsync<PostgresException>()).Which;
    }

    private static void AssertOwnedDiagnostic(
        PostgresException exception,
        string owner,
        string diagnostic)
    {
        switch (owner)
        {
            case OwnerNotNull:
                exception.SqlState.Should().Be(
                    PostgresErrorCodes.NotNullViolation);
                exception.ColumnName.Should().Be(diagnostic);
                break;
            case OwnerRowCheck:
                exception.SqlState.Should().Be(
                    PostgresErrorCodes.CheckViolation);
                exception.ConstraintName.Should().Be(diagnostic);
                break;
            case OwnerAggregate:
                AssertTriggerDiagnostic(exception, diagnostic);
                break;
            case OwnerColumnLength:
                exception.SqlState.Should().Be(
                    PostgresErrorCodes.StringDataRightTruncation);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(owner),
                    owner,
                    "Unsupported rejection owner.");
        }
    }

    private static void AssertTriggerDiagnostic(
        PostgresException exception,
        string message)
    {
        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.Should().BeNull();
        exception.MessageText.Should().Be(message);
    }

    private static void AssertAcceptedRootCase(
        RootSnapshot snapshot,
        string caseName)
    {
        snapshot.Latitude.Should().NotBeNull();
        snapshot.Longitude.Should().NotBeNull();
        snapshot.Precision.Should().NotBeNull();
        snapshot.ProviderKey.Should().NotBeNullOrEmpty();
        snapshot.ResultReference.Should().NotBeNullOrEmpty();
        snapshot.ConfirmedAtUtc.Should().NotBeNull();

        switch (caseName)
        {
            case "minimum_coordinate_boundaries":
                snapshot.Latitude.Should().Be(-90m);
                snapshot.Longitude.Should().Be(-180m);
                break;
            case "maximum_coordinate_boundaries":
                snapshot.Latitude.Should().Be(90m);
                snapshot.Longitude.Should().Be(180m);
                break;
            case "null_display_name":
                snapshot.DisplayName.Should().BeNull();
                break;
            case "canonical_display_name":
                snapshot.DisplayName.Should().Be("Canonical display");
                break;
            case "maximum_unicode_scalar_lengths":
                snapshot.ProviderKey!.EnumerateRunes().Count().Should().Be(64);
                snapshot.ResultReference!.EnumerateRunes().Count()
                    .Should().Be(512);
                snapshot.DisplayName!.EnumerateRunes().Count().Should().Be(500);
                break;
            default:
                if (caseName.StartsWith(
                        "precision_",
                        StringComparison.Ordinal))
                {
                    string expected = caseName["precision_".Length..]
                        .Replace("_", string.Empty, StringComparison.Ordinal);
                    string.Equals(
                            snapshot.Precision!.Value.ToString(),
                            expected,
                            StringComparison.OrdinalIgnoreCase)
                        .Should().BeTrue();
                }

                break;
        }
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

    private static void AddStringParameter(
        DbCommand command,
        string name,
        string value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ListingSeed(Guid ListingId, Guid TranslationId);

    private sealed record RootSnapshot(
        decimal? Latitude,
        decimal? Longitude,
        LocationPrecision? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc);
}
