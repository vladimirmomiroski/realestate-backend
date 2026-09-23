using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NpgsqlTypes;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Enums;
using RealEstate.QueryReview;

namespace RealEstate.Tests.Unit.QueryReview;

public sealed class DiscoveryQueryShapeManifestTests
{
    [Fact]
    public void SuccessorDefinition_LocksExactInventoryAndTotals()
    {
        QueryShapeContractDefinition definition = DiscoveryQueryShapeManifest.GetDefinition(
            QueryReviewGenerations.FourRootDiscovery);

        definition.ShapeCount.Should().Be(21);
        definition.CommandCount.Should().Be(83);
        definition.TypedParameterCount.Should().Be(190);
        definition.PlanCount.Should().Be(498);
        definition.ProfileInvariantCount.Should().Be(179);
        definition.CommandKeys.Should().OnlyHaveUniqueItems();
        definition.ExpectedManifestSha256.Should().Be(
            DiscoveryQueryShapeManifest.ExpectedSuccessorManifestSha256);
        definition.ExpectedManifestSha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void SuccessorDefinition_LocksEightMappedLegacyAndThirteenDiscoveryShapes()
    {
        QueryShapeDefinitions.LegacyShapeIds.Should().Equal(
            "N1", "P1", "P2", "A1", "R1", "L1", "Q1", "C1");
        QueryShapeDefinitions.SuccessorDiscoveryShapeIds.Should().Equal(
            "commercial-unknown-first-page",
            "commercial-office-first-page",
            "commercial-office-root-first-page",
            "commercial-shop-location",
            "commercial-other-deep-page",
            "land-unknown-first-page",
            "land-building-plot-first-page",
            "land-building-plot-root-first-page",
            "land-agricultural-location",
            "land-other-deep-page",
            "agency-commercial-shop-first-page",
            "agency-land-agricultural-deep-page",
            "cross-family-subtypes-empty");
    }

    [Fact]
    public void SuccessorInputs_AreExactScalarGeneralSearchQueries()
    {
        var inputs = QueryShapeDefinitions.GetSuccessorDiscoveryQueries();

        inputs.Should().HaveCount(13);
        inputs.Select(input => input.ShapeId).Should().Equal(
            QueryShapeDefinitions.SuccessorDiscoveryShapeIds);
        inputs.Should().OnlyContain(input =>
            input.Query.LanguageCode == "en" && input.Query.PageSize == 20);

        var commercialUnknown = Input(QueryShapeDefinitions.CommercialUnknownFirstPage);
        commercialUnknown.CommercialType.Should().Be(CommercialType.Unknown);
        commercialUnknown.PropertyType.Should().BeNull();

        var office = Input(QueryShapeDefinitions.CommercialOfficeFirstPage);
        office.CommercialType.Should().Be(CommercialType.Office);
        office.PropertyType.Should().BeNull();
        office.Currency.Should().Be("EUR");
        office.SortOption.Should().Be(ListingSortOption.PriceAsc);

        Input(QueryShapeDefinitions.CommercialOfficeRootFirstPage)
            .PropertyType.Should().Be(PropertyType.Commercial);
        var commercialLocation = Input(QueryShapeDefinitions.CommercialShopLocation);
        commercialLocation.CommercialType.Should().Be(CommercialType.Shop);
        commercialLocation.City.Should().Be("Skopje");
        commercialLocation.Municipality.Should().Be("Centar");
        var commercialDeep = Input(QueryShapeDefinitions.CommercialOtherDeepPage);
        commercialDeep.CommercialType.Should().Be(CommercialType.Other);
        commercialDeep.Page.Should().Be(25);

        Input(QueryShapeDefinitions.LandUnknownFirstPage)
            .LandType.Should().Be(LandType.Unknown);
        var building = Input(QueryShapeDefinitions.LandBuildingPlotFirstPage);
        building.LandType.Should().Be(LandType.BuildingPlot);
        building.PropertyType.Should().BeNull();
        building.Currency.Should().Be("EUR");
        building.SortOption.Should().Be(ListingSortOption.PriceDesc);
        Input(QueryShapeDefinitions.LandBuildingPlotRootFirstPage)
            .PropertyType.Should().Be(PropertyType.Land);
        var landLocation = Input(QueryShapeDefinitions.LandAgriculturalLocation);
        landLocation.LandType.Should().Be(LandType.AgriculturalLand);
        landLocation.City.Should().Be("Skopje");
        landLocation.Municipality.Should().Be("Centar");
        var landDeep = Input(QueryShapeDefinitions.LandOtherDeepPage);
        landDeep.LandType.Should().Be(LandType.Other);
        landDeep.Page.Should().Be(25);

        Guid agencyId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var agencyCommercial = Input(QueryShapeDefinitions.AgencyCommercialShopFirstPage);
        agencyCommercial.AgencyId.Should().Be(agencyId);
        agencyCommercial.CommercialType.Should().Be(CommercialType.Shop);
        var agencyLand = Input(QueryShapeDefinitions.AgencyLandAgriculturalDeepPage);
        agencyLand.AgencyId.Should().Be(agencyId);
        agencyLand.LandType.Should().Be(LandType.AgriculturalLand);
        agencyLand.Page.Should().Be(4);
        var crossFamily = Input(QueryShapeDefinitions.CrossFamilySubtypesEmpty);
        crossFamily.CommercialType.Should().Be(CommercialType.Office);
        crossFamily.LandType.Should().Be(LandType.BuildingPlot);

        return;

        GetListingsQuery Input(string id) =>
            inputs.Single(input => input.ShapeId == id).Query;
    }

    [Fact]
    public void SuccessorRoles_LockPopulatedAgencyDeepPageAndOnlyCrossFamilyIsEmpty()
    {
        var roles = QueryShapeDefinitions.GetExpectedCommandRoles(
            QueryReviewGenerations.FourRootDiscovery);

        roles[QueryShapeDefinitions.AgencyCommercialShopFirstPage].Keys.Should().Equal(
            CommandRoles.FilteredCount,
            CommandRoles.PageRoot,
            CommandRoles.TranslationSplit,
            CommandRoles.ImageSplit);
        roles[QueryShapeDefinitions.AgencyLandAgriculturalDeepPage].Keys.Should().Equal(
            CommandRoles.FilteredCount,
            CommandRoles.PageRoot,
            CommandRoles.TranslationSplit,
            CommandRoles.ImageSplit);
        roles[QueryShapeDefinitions.CrossFamilySubtypesEmpty].Keys.Should().Equal(
            CommandRoles.FilteredCount,
            CommandRoles.PageRoot);
        roles[QueryShapeDefinitions.AgencyCommercialShopFirstPage].Keys.Should()
            .NotContain(CommandRoles.AgencyExistence);
        roles[QueryShapeDefinitions.AgencyLandAgriculturalDeepPage].Keys.Should()
            .NotContain(CommandRoles.AgencyExistence);
        roles.Where(pair => pair.Value.Keys.SequenceEqual(
                [CommandRoles.FilteredCount, CommandRoles.PageRoot]))
            .Select(pair => pair.Key)
            .Should().Equal(QueryShapeDefinitions.CrossFamilySubtypesEmpty);
    }

    [Fact]
    public void AcceptedAgencyLandDeepPageIdentity_LocksCapturedPageFourResult()
    {
        QueryShapeResultIdentity result =
            DiscoveryQueryShapeManifest.AcceptedAgencyLandDeepPageResult;

        result.ShapeId.Should().Be(QueryShapeDefinitions.AgencyLandAgriculturalDeepPage);
        result.TotalCount.Should().Be(71);
        result.ItemCount.Should().Be(11);
        result.OrderedIds.Should().HaveCount(11);
        result.OrderedIdsSha256.Should().Be(
            "1b0a6a8f792c7adccbb27ecddcfe63f540c26a5b11033cb691b168c946bbf29f");
    }

    [Fact]
    public void CanonicalInputs_LockAllTwentyOneShapesAndHistoricalSubtypeNulls()
    {
        IReadOnlyList<QueryShapeInputIdentity> inputs =
            DiscoveryQueryShapeManifest.CreateInputIdentities();

        inputs.Select(input => input.ShapeId).Should().Equal(
            QueryShapeDefinitions.LegacyShapeIds
                .Concat(QueryShapeDefinitions.SuccessorDiscoveryShapeIds));
        inputs.Should().HaveCount(21);

        foreach (string shapeId in QueryShapeDefinitions.LegacyShapeIds)
        {
            QueryShapeInputIdentity identity = inputs.Single(input => input.ShapeId == shapeId);
            using JsonDocument json = JsonDocument.Parse(identity.CanonicalInputJson);

            json.RootElement.TryGetProperty("CommercialType", out JsonElement commercial)
                .Should().BeTrue();
            commercial.ValueKind.Should().Be(JsonValueKind.Null);
            json.RootElement.TryGetProperty("LandType", out JsonElement land)
                .Should().BeTrue();
            land.ValueKind.Should().Be(JsonValueKind.Null);
            identity.Sha256.Should().Be(Sha256(identity.CanonicalInputJson));
        }
    }

    [Theory]
    [InlineData("N1", "\"Page\":1", "\"Page\":2")]
    [InlineData("N1", "\"CommercialType\":null", "\"CommercialType\":1")]
    [InlineData("C1", "\"LandType\":null", "\"LandType\":1")]
    public void RecordedInputValidation_RejectsHistoricalInputOnlyDrift(
        string shapeId,
        string expectedFragment,
        string changedFragment)
    {
        QueryShapeInputIdentity[] recorded =
            DiscoveryQueryShapeManifest.CreateInputIdentities().ToArray();
        int index = Array.FindIndex(recorded, input => input.ShapeId == shapeId);
        string changedJson = recorded[index].CanonicalInputJson.Replace(
            expectedFragment,
            changedFragment,
            StringComparison.Ordinal);
        changedJson.Should().NotBe(recorded[index].CanonicalInputJson);
        recorded[index] = recorded[index] with
        {
            CanonicalInputJson = changedJson,
            Sha256 = Sha256(changedJson)
        };

        Action act = () => DiscoveryQueryShapeManifest
            .ValidateRecordedInputIdentities(recorded);

        act.Should().Throw<BaselinePlanValidationException>()
            .WithMessage("*canonical query inputs*");
    }

    [Fact]
    public void StructureValidation_RejectsUnknownMissingExtraRoleOrderAndTotalsDrift()
    {
        QueryShapeContractDefinition definition = DiscoveryQueryShapeManifest.GetDefinition(
            QueryReviewGenerations.FourRootDiscovery);
        SqlCaptureRun valid = CreateStructuralCapture(definition);

        Action validAct = () => DiscoveryQueryShapeManifest.ValidateStructure(definition, valid);
        validAct.Should().NotThrow();

        AssertRejected(valid with
        {
            ShapeResults = valid.ShapeResults.Skip(1).ToArray()
        });
        AssertRejected(valid with
        {
            ShapeResults = [.. valid.ShapeResults,
                new QueryShapeResult("unexpected-shape", 0, 0, 0, 0, [])]
        });
        AssertRejected(valid with
        {
            Commands = valid.Commands.Skip(1).ToArray()
        });
        AssertRejected(valid with
        {
            Commands = [.. valid.Commands,
                Command("unexpected-shape", 1, CommandRoles.FilteredCount, "SELECT 1", [])]
        });
        AssertRejected(valid with
        {
            Commands = valid.Commands
                .Select((command, index) => index == 0
                    ? command with { CommandRole = "unexpected-role" }
                    : command)
                .ToArray()
        });
        AssertRejected(valid with
        {
            Commands = [valid.Commands[1], valid.Commands[0], .. valid.Commands.Skip(2)]
        });
        AssertRejected(valid with { GenerationId = "wrong-generation" });
        AssertRejected(valid with { ProfileVersion = "wrong-profile" });

        Action totalDrift = () => DiscoveryQueryShapeManifest.ValidateStructure(
            definition with { CommandCount = definition.CommandCount + 1 },
            valid);
        totalDrift.Should().Throw<BaselinePlanValidationException>();

        return;

        void AssertRejected(SqlCaptureRun capture)
        {
            Action act = () => DiscoveryQueryShapeManifest.ValidateStructure(definition, capture);
            act.Should().Throw<BaselinePlanValidationException>();
        }
    }

    [Fact]
    public void TypedParameterIdentity_RejectsNameTypeValueNullabilityAndOrderDrift()
    {
        CapturedParameter first = Parameter("@subtype", "1", isNullable: false, isNull: false);
        CapturedParameter second = Parameter("@page", "4", isNullable: false, isNull: false);
        CapturedCommand command = Command(
            "shape",
            1,
            CommandRoles.FilteredCount,
            "SELECT @subtype",
            [first, second]);
        QueryCommandIdentity expected = DiscoveryQueryShapeManifest.CreateCommandIdentity(command);

        Action exact = () => DiscoveryQueryShapeManifest.ValidateCommandIdentity(expected, command);
        exact.Should().NotThrow();

        foreach (CapturedParameter changed in new[]
                 {
                     Parameter("@other", "1", false, false),
                     Parameter("@subtype", "2", false, false),
                     Parameter("@subtype", "1", true, false),
                     Parameter("@subtype", "null", true, true),
                     Parameter("@subtype", "1", false, false) with
                     {
                         ClrType = typeof(long).FullName!,
                         DbType = DbType.Int64.ToString(),
                         NpgsqlDbType = NpgsqlDbType.Bigint.ToString()
                     }
                 })
        {
            Action drift = () => DiscoveryQueryShapeManifest.ValidateCommandIdentity(
                expected,
                command with { Parameters = [changed, second] });
            drift.Should().Throw<BaselinePlanValidationException>();
        }

        Action reordered = () => DiscoveryQueryShapeManifest.ValidateCommandIdentity(
            expected,
            command with { Parameters = [second, first] });
        reordered.Should().Throw<BaselinePlanValidationException>();
    }

    [Fact]
    public async Task RawArtifactReader_RejectsMalformedRecordedManifestJson()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"queryreview-malformed-capture-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(path, "{\"QueryShapeManifest\":");

            Func<Task> act = async () =>
                await ExplainRunner.ReadRequiredJsonAsync<SqlCaptureRun>(
                    path,
                    CancellationToken.None);

            await act.Should().ThrowAsync<BaselinePlanValidationException>()
                .WithMessage("*malformed*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SubtypePredicateValidation_RequiresExplicitRootAndChildTypeForBothFamilies()
    {
        List<CapturedCommand> commands = CreateSubtypeSelectionCommands();

        Action exact = () => QueryShapeDefinitions.ValidateSuccessorSubtypePredicates(commands);
        exact.Should().NotThrow();

        int index = commands.FindIndex(command =>
            command.ShapeId == QueryShapeDefinitions.CommercialOfficeFirstPage &&
            command.CommandRole == CommandRoles.FilteredCount);
        commands[index] = commands[index] with
        {
            CommandText = commands[index].CommandText.Replace(
                "\"PropertyType\" = 'Commercial' AND ",
                string.Empty,
                StringComparison.Ordinal)
        };

        Action missingRoot = () => QueryShapeDefinitions
            .ValidateSuccessorSubtypePredicates(commands);
        missingRoot.Should().Throw<SqlCaptureValidationException>()
            .WithMessage("*explicit Commercial root equality*");
    }

    private static SqlCaptureRun CreateStructuralCapture(
        QueryShapeContractDefinition definition)
    {
        string[] shapeIds =
        [.. QueryShapeDefinitions.LegacyShapeIds,
            .. QueryShapeDefinitions.SuccessorDiscoveryShapeIds];
        var results = shapeIds.Select(shapeId =>
            new QueryShapeResult(shapeId, 0, 0, 0, 0, Array.Empty<Guid>())).ToArray();
        var commands = new List<CapturedCommand>();
        var roles = QueryShapeDefinitions.GetExpectedCommandRoles(
            QueryReviewGenerations.FourRootDiscovery);

        foreach ((string shapeId, IReadOnlyDictionary<string, int> shapeRoles) in roles)
        {
            int sequence = 0;
            foreach (string role in shapeRoles.Keys)
            {
                sequence++;
                commands.Add(Command(shapeId, sequence, role, "SELECT 1", []));
            }
        }

        CapturedParameter[] parameters = Enumerable.Range(1, definition.TypedParameterCount)
            .Select(index => Parameter($"@p{index}", index.ToString(), false, false))
            .ToArray();
        commands[0] = commands[0] with { Parameters = parameters };

        return new SqlCaptureRun(
            definition.LogicalRunId,
            definition.ProfileIdentity,
            DeterministicProfileSeeder.CSharpSeed,
            DeterministicProfileSeeder.PostgreSqlSeed,
            "database",
            "16.14",
            results,
            commands,
            definition.GenerationId);
    }

    private static List<CapturedCommand> CreateSubtypeSelectionCommands()
    {
        var commands = new List<CapturedCommand>();
        var roles = QueryShapeDefinitions.GetExpectedCommandRoles(
            QueryReviewGenerations.FourRootDiscovery);

        foreach (string shapeId in QueryShapeDefinitions.SuccessorDiscoveryShapeIds)
        {
            bool commercial = shapeId.StartsWith("commercial-", StringComparison.Ordinal) ||
                              shapeId == QueryShapeDefinitions.AgencyCommercialShopFirstPage ||
                              shapeId == QueryShapeDefinitions.CrossFamilySubtypesEmpty;
            bool land = shapeId.StartsWith("land-", StringComparison.Ordinal) ||
                        shapeId == QueryShapeDefinitions.AgencyLandAgriculturalDeepPage ||
                        shapeId == QueryShapeDefinitions.CrossFamilySubtypesEmpty;
            string sql = "SELECT 1 WHERE ";
            if (commercial)
            {
                sql += "\"PropertyType\" = 'Commercial' AND \"ListingCommercialDetails\"." +
                       "\"CommercialType\" = @commercialType ";
            }

            if (land)
            {
                sql += "\"PropertyType\" = 'Land' AND \"ListingLandDetails\"." +
                       "\"LandType\" = @landType";
            }

            string[] shapeRoles = roles[shapeId].Keys.ToArray();
            foreach ((string role, int index) in shapeRoles.Select((role, index) => (role, index)))
            {
                if (role is CommandRoles.FilteredCount or CommandRoles.PageRoot)
                {
                    commands.Add(Command(shapeId, index + 1, role, sql, []));
                }
            }
        }

        return commands;
    }

    private static CapturedCommand Command(
        string shapeId,
        int sequence,
        string role,
        string sql,
        IReadOnlyList<CapturedParameter> parameters) =>
        new(
            QueryShapeDefinitions.FourRootLogicalRunId,
            shapeId,
            sequence,
            role,
            CommandType.Text.ToString(),
            sql,
            parameters);

    private static CapturedParameter Parameter(
        string name,
        string value,
        bool isNullable,
        bool isNull) =>
        new(
            name,
            typeof(int).FullName!,
            DbType.Int32.ToString(),
            NpgsqlDbType.Integer.ToString(),
            "integer",
            isNullable,
            isNull,
            value);

    private static string Sha256(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
