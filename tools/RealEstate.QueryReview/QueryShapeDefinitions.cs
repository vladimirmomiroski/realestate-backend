using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;

namespace RealEstate.QueryReview;

internal static class QueryShapeDefinitions
{
    public const string LogicalRunId = "chapter-10f-v1-production-sql";
    public const string FourRootLogicalRunId = "four-root-discovery-v1-production-sql";
    public const string AgencyShapeId = "A1";
    public const string ComparableShapeId = "C1";

    public const string CommercialUnknownFirstPage = "commercial-unknown-first-page";
    public const string CommercialOfficeFirstPage = "commercial-office-first-page";
    public const string CommercialOfficeRootFirstPage = "commercial-office-root-first-page";
    public const string CommercialShopLocation = "commercial-shop-location";
    public const string CommercialOtherDeepPage = "commercial-other-deep-page";
    public const string LandUnknownFirstPage = "land-unknown-first-page";
    public const string LandBuildingPlotFirstPage = "land-building-plot-first-page";
    public const string LandBuildingPlotRootFirstPage = "land-building-plot-root-first-page";
    public const string LandAgriculturalLocation = "land-agricultural-location";
    public const string LandOtherDeepPage = "land-other-deep-page";
    public const string AgencyCommercialShopFirstPage = "agency-commercial-shop-first-page";
    public const string AgencyLandAgriculturalDeepPage = "agency-land-agricultural-deep-page";
    public const string CrossFamilySubtypesEmpty = "cross-family-subtypes-empty";

    private const string N1 = "N1";
    private const string P1 = "P1";
    private const string P2 = "P2";
    private const string R1 = "R1";
    private const string L1 = "L1";
    private const string Q1 = "Q1";

    private static readonly Guid AgencyOneId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    private static readonly Guid SuccessorAgencyId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static IReadOnlyList<string> LegacyShapeIds { get; } =
        [N1, P1, P2, AgencyShapeId, R1, L1, Q1, ComparableShapeId];

    public static IReadOnlyList<string> SuccessorDiscoveryShapeIds { get; } =
    [
        CommercialUnknownFirstPage,
        CommercialOfficeFirstPage,
        CommercialOfficeRootFirstPage,
        CommercialShopLocation,
        CommercialOtherDeepPage,
        LandUnknownFirstPage,
        LandBuildingPlotFirstPage,
        LandBuildingPlotRootFirstPage,
        LandAgriculturalLocation,
        LandOtherDeepPage,
        AgencyCommercialShopFirstPage,
        AgencyLandAgriculturalDeepPage,
        CrossFamilySubtypesEmpty
    ];

    private static readonly Guid ComparableSourceId =
        Guid.Parse(DeterministicProfileSeeder.ComparableSourceId);

    private static readonly Guid[] ExpectedComparableIds =
    [
        ListingId(3003),
        ListingId(3002),
        ListingId(3005),
        ListingId(3004),
        ListingId(3006),
        ListingId(3007)
    ];

    private static readonly IReadOnlyDictionary<string, Guid[]> ExpectedPagedIds =
        new Dictionary<string, Guid[]>(StringComparer.Ordinal)
        {
            [N1] = ListingIds(startOrdinal: 3031, count: 20, step: -1),
            [P1] = ListingIds(startOrdinal: 69_961, count: 20, step: -3_000),
            [P2] = ListingIds(startOrdinal: 68_998, count: 20, step: -3_000),
            [AgencyShapeId] =
            [
                ListingId(3001),
                .. ListingIds(startOrdinal: 69_801, count: 19, step: -1_000)
            ],
            [R1] = ListingIds(startOrdinal: 69_844, count: 20, step: -1_000),
            [L1] = ListingIds(startOrdinal: 1140, count: 20, step: -1),
            [Q1] = ListingIds(startOrdinal: 2120, count: 20, step: -1)
        };

    public static IReadOnlyList<LockedQueryShapeExpectation> GetLockedResultExpectations()
    {
        return
        [
            PagedExpectation(N1, 70_000),
            PagedExpectation(P1, 23_334),
            PagedExpectation(P2, 23_334),
            PagedExpectation(AgencyShapeId, 350),
            PagedExpectation(R1, 1_050),
            PagedExpectation(L1, 140),
            PagedExpectation(Q1, 120),
            new LockedQueryShapeExpectation(
                ComparableShapeId,
                ExpectedTotalCount: 30,
                ExpectedItemCount: 6,
                ExpectedComparableIds)
        ];
    }

    public static async Task<IReadOnlyList<QueryShapeResult>> ExecuteAsync(
        QueryReviewGenerationDefinition generation,
        RealEstateDbContext dbContext,
        ProductionCommandCaptureInterceptor interceptor,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteCoreAsync(
            generation,
            dbContext,
            interceptor,
            cancellationToken);
    }

    public static async Task<IReadOnlyList<QueryShapeResult>> VerifyLockedResultIdentitiesAsync(
        QueryReviewGenerationDefinition generation,
        RealEstateDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteCoreAsync(
            generation,
            dbContext,
            interceptor: null,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<QueryShapeResult>> ExecuteCoreAsync(
        QueryReviewGenerationDefinition generation,
        RealEstateDbContext dbContext,
        ProductionCommandCaptureInterceptor? interceptor,
        CancellationToken cancellationToken)
    {
        var listingRepository = new ListingRepository(dbContext);
        var agencyRepository = new AgencyRepository(dbContext);
        var results = new List<QueryShapeResult>();
        IReadOnlyDictionary<string, GetListingsQuery> legacyQueries =
            GetLegacyListingQueries().ToDictionary(
                input => input.ShapeId,
                input => input.Query,
                StringComparer.Ordinal);

        results.Add(await ExecutePagedAsync(
            N1,
            listingRepository,
            interceptor,
            legacyQueries[N1],
            expectedTotalCount: 70_000,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            P1,
            listingRepository,
            interceptor,
            legacyQueries[P1],
            expectedTotalCount: 23_334,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            P2,
            listingRepository,
            interceptor,
            legacyQueries[P2],
            expectedTotalCount: 23_334,
            expectedItemCount: 20,
            cancellationToken));

        using (interceptor?.BeginShape(AgencyShapeId))
        {
            var agencyExists = await agencyRepository.ExistsAsync(
                AgencyOneId,
                cancellationToken);

            if (!agencyExists)
            {
                throw new SqlCaptureValidationException(
                    $"{AgencyShapeId}: deterministic agency '{AgencyOneId}' was not found.");
            }

            var agencyResult = await listingRepository.GetFilteredReadOnlyAsync(
                legacyQueries[AgencyShapeId],
                cancellationToken);

            results.Add(ValidatePagedResult(
                AgencyShapeId,
                agencyResult,
                expectedTotalCount: 350,
                expectedItemCount: 20));
        }

        results.Add(await ExecutePagedAsync(
            R1,
            listingRepository,
            interceptor,
            legacyQueries[R1],
            expectedTotalCount: 1_050,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            L1,
            listingRepository,
            interceptor,
            legacyQueries[L1],
            expectedTotalCount: 140,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            Q1,
            listingRepository,
            interceptor,
            legacyQueries[Q1],
            expectedTotalCount: 120,
            expectedItemCount: 20,
            cancellationToken));

        using (interceptor?.BeginShape(ComparableShapeId))
        {
            ComparableListingsReadResult comparableResult =
                await listingRepository.GetComparableListingsReadOnlyAsync(
                    ComparableSourceId,
                    "en",
                    6,
                    cancellationToken);

            if (!comparableResult.SourceFound)
            {
                throw new SqlCaptureValidationException(
                    $"{ComparableShapeId}: deterministic source '{ComparableSourceId}' was not found.");
            }

            var actualIds = comparableResult.Items.Select(item => item.Id).ToArray();

            if (!actualIds.SequenceEqual(ExpectedComparableIds))
            {
                throw new SqlCaptureValidationException(
                    $"{ComparableShapeId}: expected comparable order " +
                    $"[{string.Join(", ", ExpectedComparableIds)}], actual " +
                    $"[{string.Join(", ", actualIds)}].");
            }

            results.Add(new QueryShapeResult(
                ComparableShapeId,
                ExpectedTotalCount: 30,
                ActualTotalCount: 30,
                ExpectedItemCount: 6,
                ActualItemCount: actualIds.Length,
                actualIds));
        }

        if (generation == QueryReviewGenerations.FourRootDiscovery)
        {
            await ExecuteSuccessorDiscoveryShapesAsync(
                listingRepository,
                interceptor,
                results,
                cancellationToken);
            DiscoveryQueryShapeManifest.ValidateSuccessorResultIdentity(results);
        }

        if (interceptor is not null)
        {
            ValidateCapturedCommands(generation, interceptor.Commands);
        }

        return results;
    }

    private static async Task ExecuteSuccessorDiscoveryShapesAsync(
        ListingRepository repository,
        ProductionCommandCaptureInterceptor? interceptor,
        ICollection<QueryShapeResult> results,
        CancellationToken cancellationToken)
    {
        foreach ((string shapeId, GetListingsQuery query) in GetSuccessorDiscoveryQueries())
        {
            results.Add(await ExecuteSuccessorPagedAsync(
                shapeId,
                repository,
                interceptor,
                query,
                cancellationToken));
        }
    }

    internal static IReadOnlyList<(string ShapeId, GetListingsQuery Query)>
        GetLegacyListingQueries()
    {
        return
        [
            (N1, NewestQuery()),
            (P1, PriceQuery(ListingSortOption.PriceAsc, "priceAsc").WithQuery(query =>
                query.Currency = "EUR")),
            (P2, PriceQuery(ListingSortOption.PriceDesc, "priceDesc").WithQuery(query =>
                query.Currency = "EUR")),
            (AgencyShapeId, NewestQuery().WithQuery(query =>
                query.AgencyId = AgencyOneId)),
            (R1, NewestQuery().WithQuery(query =>
            {
                query.MinAreaSquareMeters = 80m;
                query.MaxAreaSquareMeters = 89m;
                query.MinRooms = 2m;
                query.MaxRooms = 3m;
            })),
            (L1, NewestQuery().WithQuery(query =>
            {
                query.City = "AuditCity10F";
                query.Municipality = "AuditMunicipality10F";
                query.Neighborhood = "AuditNeighborhood10F";
            })),
            (Q1, NewestQuery().WithQuery(query =>
                query.SearchText = "needle10f"))
        ];
    }

    internal static IReadOnlyList<(string ShapeId, GetListingsQuery Query)>
        GetSuccessorDiscoveryQueries()
    {
        return
        [
            (CommercialUnknownFirstPage, NewestQuery().WithQuery(query =>
                query.CommercialType = CommercialType.Unknown)),
            (CommercialOfficeFirstPage, PriceQuery(ListingSortOption.PriceAsc, "priceAsc")
                .WithQuery(query =>
                {
                    query.CommercialType = CommercialType.Office;
                    query.Currency = "EUR";
                })),
            (CommercialOfficeRootFirstPage, PriceQuery(ListingSortOption.PriceAsc, "priceAsc")
                .WithQuery(query =>
                {
                    query.PropertyType = PropertyType.Commercial;
                    query.CommercialType = CommercialType.Office;
                    query.Currency = "EUR";
                })),
            (CommercialShopLocation, NewestQuery().WithQuery(query =>
                {
                    query.CommercialType = CommercialType.Shop;
                    query.City = "Skopje";
                    query.Municipality = "Centar";
                })),
            (CommercialOtherDeepPage, NewestQuery(page: 25).WithQuery(query =>
                query.CommercialType = CommercialType.Other)),
            (LandUnknownFirstPage, NewestQuery().WithQuery(query =>
                query.LandType = LandType.Unknown)),
            (LandBuildingPlotFirstPage, PriceQuery(ListingSortOption.PriceDesc, "priceDesc")
                .WithQuery(query =>
                {
                    query.LandType = LandType.BuildingPlot;
                    query.Currency = "EUR";
                })),
            (LandBuildingPlotRootFirstPage, PriceQuery(ListingSortOption.PriceDesc, "priceDesc")
                .WithQuery(query =>
                {
                    query.PropertyType = PropertyType.Land;
                    query.LandType = LandType.BuildingPlot;
                    query.Currency = "EUR";
                })),
            (LandAgriculturalLocation, NewestQuery().WithQuery(query =>
                {
                    query.LandType = LandType.AgriculturalLand;
                    query.City = "Skopje";
                    query.Municipality = "Centar";
                })),
            (LandOtherDeepPage, NewestQuery(page: 25).WithQuery(query =>
                query.LandType = LandType.Other)),
            (AgencyCommercialShopFirstPage, NewestQuery().WithQuery(query =>
                {
                    query.AgencyId = SuccessorAgencyId;
                    query.CommercialType = CommercialType.Shop;
                })),
            (AgencyLandAgriculturalDeepPage, NewestQuery(page: 4).WithQuery(query =>
                {
                    query.AgencyId = SuccessorAgencyId;
                    query.LandType = LandType.AgriculturalLand;
                })),
            (CrossFamilySubtypesEmpty, NewestQuery().WithQuery(query =>
                {
                    query.CommercialType = CommercialType.Office;
                    query.LandType = LandType.BuildingPlot;
                }))
        ];
    }

    internal static IReadOnlyList<(string ShapeId, QueryShapeCanonicalInput Input)>
        GetCanonicalShapeInputs()
    {
        var inputs = new List<(string ShapeId, QueryShapeCanonicalInput Input)>(21);

        foreach ((string shapeId, GetListingsQuery query) in GetLegacyListingQueries())
        {
            inputs.Add((
                shapeId,
                CreateCanonicalListingInput(
                    query,
                    operation: shapeId == AgencyShapeId
                        ? "agency-existence-plus-public-listings"
                        : "public-listings",
                    agencyExistenceId: shapeId == AgencyShapeId ? AgencyOneId : null)));
        }

        inputs.Add((
            ComparableShapeId,
            new QueryShapeCanonicalInput(
                Operation: "comparable-listings",
                LanguageCode: "en",
                SearchText: null,
                AgencyId: null,
                ListingType: null,
                PropertyType: null,
                HeatingType: null,
                FurnishingStatus: null,
                Condition: null,
                HasBasement: null,
                HasElevator: null,
                ApartmentType: null,
                HouseType: null,
                CommercialType: null,
                LandType: null,
                MinYardAreaSquareMeters: null,
                MaxYardAreaSquareMeters: null,
                MinPrice: null,
                MaxPrice: null,
                Currency: null,
                MinAreaSquareMeters: null,
                MaxAreaSquareMeters: null,
                MinRooms: null,
                MaxRooms: null,
                Sort: null,
                SortOption: null,
                City: null,
                Municipality: null,
                Neighborhood: null,
                Page: null,
                PageSize: null,
                AgencyExistenceId: null,
                ComparableSourceId: ComparableSourceId,
                ComparableLimit: 6)));

        foreach ((string shapeId, GetListingsQuery query) in GetSuccessorDiscoveryQueries())
        {
            inputs.Add((shapeId, CreateCanonicalListingInput(query, "public-listings")));
        }

        return inputs;
    }

    private static QueryShapeCanonicalInput CreateCanonicalListingInput(
        GetListingsQuery query,
        string operation,
        Guid? agencyExistenceId = null)
    {
        return new QueryShapeCanonicalInput(
            operation,
            query.LanguageCode,
            query.SearchText,
            query.AgencyId,
            query.ListingType,
            query.PropertyType,
            query.HeatingType,
            query.FurnishingStatus,
            query.Condition,
            query.HasBasement,
            query.HasElevator,
            query.ApartmentType,
            query.HouseType,
            query.CommercialType,
            query.LandType,
            query.MinYardAreaSquareMeters,
            query.MaxYardAreaSquareMeters,
            query.MinPrice,
            query.MaxPrice,
            query.Currency,
            query.MinAreaSquareMeters,
            query.MaxAreaSquareMeters,
            query.MinRooms,
            query.MaxRooms,
            query.Sort,
            query.SortOption,
            query.City,
            query.Municipality,
            query.Neighborhood,
            query.Page,
            query.PageSize,
            agencyExistenceId,
            ComparableSourceId: null,
            ComparableLimit: null);
    }

    private static GetListingsQuery NewestQuery(int page = 1)
    {
        return new GetListingsQuery
        {
            LanguageCode = "en",
            Sort = "newest",
            SortOption = ListingSortOption.Newest,
            Page = page,
            PageSize = 20
        };
    }

    private static GetListingsQuery PriceQuery(ListingSortOption option, string sort)
    {
        return new GetListingsQuery
        {
            LanguageCode = "en",
            Sort = sort,
            SortOption = option,
            Page = 1,
            PageSize = 20
        };
    }

    private static GetListingsQuery WithQuery(
        this GetListingsQuery query,
        Action<GetListingsQuery> configure)
    {
        configure(query);
        return query;
    }

    private static async Task<QueryShapeResult> ExecuteSuccessorPagedAsync(
        string shapeId,
        ListingRepository repository,
        ProductionCommandCaptureInterceptor? interceptor,
        GetListingsQuery query,
        CancellationToken cancellationToken)
    {
        using (interceptor?.BeginShape(shapeId))
        {
            PagedResult<Listing> result = await repository.GetFilteredReadOnlyAsync(
                query,
                cancellationToken);
            Guid[] actualIds = result.Items.Select(item => item.Id).ToArray();

            return new QueryShapeResult(
                shapeId,
                ExpectedTotalCount: result.TotalCount,
                ActualTotalCount: result.TotalCount,
                ExpectedItemCount: result.Items.Count,
                ActualItemCount: result.Items.Count,
                actualIds);
        }
    }

    public static void EnsureOutputIsOutsideRepository(string outputDirectory)
    {
        var repositoryRoot = FindRepositoryRoot();

        if (repositoryRoot is null)
        {
            throw new SqlCaptureValidationException(
                "Unable to locate the repository root before validating the capture output path.");
        }

        var fullOutputPath = Path.GetFullPath(outputDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRepositoryRoot = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relativePath = Path.GetRelativePath(fullRepositoryRoot, fullOutputPath);

        if (!relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !string.Equals(relativePath, "..", StringComparison.Ordinal))
        {
            throw new SqlCaptureValidationException(
                $"Capture output '{fullOutputPath}' is inside repository '{fullRepositoryRoot}'.");
        }
    }

    private static async Task<QueryShapeResult> ExecutePagedAsync(
        string shapeId,
        ListingRepository repository,
        ProductionCommandCaptureInterceptor? interceptor,
        GetListingsQuery query,
        int expectedTotalCount,
        int expectedItemCount,
        CancellationToken cancellationToken)
    {
        using (interceptor?.BeginShape(shapeId))
        {
            PagedResult<Listing> result = await repository.GetFilteredReadOnlyAsync(
                query,
                cancellationToken);

            return ValidatePagedResult(
                shapeId,
                result,
                expectedTotalCount,
                expectedItemCount);
        }
    }

    private static QueryShapeResult ValidatePagedResult(
        string shapeId,
        PagedResult<Listing> result,
        int expectedTotalCount,
        int expectedItemCount)
    {
        if (result.TotalCount != expectedTotalCount)
        {
            throw new SqlCaptureValidationException(
                $"{shapeId}: expected total {expectedTotalCount:N0}, actual " +
                $"{result.TotalCount:N0}.");
        }

        if (result.Items.Count != expectedItemCount)
        {
            throw new SqlCaptureValidationException(
                $"{shapeId}: expected {expectedItemCount} page items, actual " +
                $"{result.Items.Count}.");
        }

        Guid[] actualIds = result.Items
            .Select(item => item.Id)
            .ToArray();

        Guid[] expectedIds = ExpectedPagedIds[shapeId];

        if (!actualIds.SequenceEqual(expectedIds))
        {
            throw new SqlCaptureValidationException(
                $"{shapeId}: expected page order " +
                $"[{string.Join(", ", expectedIds)}], actual " +
                $"[{string.Join(", ", actualIds)}].");
        }

        return new QueryShapeResult(
            shapeId,
            expectedTotalCount,
            result.TotalCount,
            expectedItemCount,
            result.Items.Count,
            actualIds);
    }

    private static void ValidateCapturedCommands(
        QueryReviewGenerationDefinition generation,
        IReadOnlyList<CapturedCommand> commands)
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> expectedRoles =
            GetExpectedCommandRoles(generation);

        foreach (var (shapeId, requiredRoles) in expectedRoles)
        {
            var actualRoles = commands
                .Where(command => command.ShapeId == shapeId)
                .GroupBy(command => command.CommandRole, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            if (requiredRoles.Count != actualRoles.Count ||
                requiredRoles.Any(role =>
                    !actualRoles.TryGetValue(role.Key, out var count) || count != role.Value))
            {
                throw new SqlCaptureValidationException(
                    $"{shapeId}: required command roles " +
                    $"[{FormatRoleCounts(requiredRoles)}], actual " +
                    $"[{FormatRoleCounts(actualRoles)}].");
            }
        }

        int expectedCommandCount = expectedRoles.Sum(pair => pair.Value.Values.Sum());
        if (commands.Count != expectedCommandCount)
        {
            throw new SqlCaptureValidationException(
                $"Expected exactly {expectedCommandCount} production commands, captured {commands.Count}.");
        }

        ValidateCommandOrder(commands, expectedRoles);

        ValidateTypedParameters(commands);
        ValidatePublicVisibilityAndOrdering(commands);
        ValidatePagedFiltersAndChildLoading(commands);
        ValidateSetBasedEffectiveTranslationFiltering(commands);
        ValidateTextSearch(commands);
        ValidateComparableCandidateAndChildLoading(commands);

        if (generation == QueryReviewGenerations.FourRootDiscovery)
        {
            ValidateSuccessorSubtypePredicates(commands);
            ValidateSuccessorLocationTranslationFiltering(commands);
        }
    }

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>
        GetExpectedCommandRoles(QueryReviewGenerationDefinition generation)
    {
        var expectedRoles = new Dictionary<string, IReadOnlyDictionary<string, int>>(
            StringComparer.Ordinal)
        {
            [N1] = StandardPagedRoles(),
            [P1] = StandardPagedRoles(),
            [P2] = StandardPagedRoles(),
            [AgencyShapeId] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CommandRoles.AgencyExistence] = 1,
                [CommandRoles.FilteredCount] = 1,
                [CommandRoles.PageRoot] = 1,
                [CommandRoles.TranslationSplit] = 1,
                [CommandRoles.ImageSplit] = 1
            },
            [R1] = StandardPagedRoles(),
            [L1] = StandardPagedRoles(),
            [Q1] = StandardPagedRoles(),
            [ComparableShapeId] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CommandRoles.ComparableSource] = 1,
                [CommandRoles.ComparableRankedRoot] = 1,
                [CommandRoles.ComparableTranslationSplit] = 1,
                [CommandRoles.ComparableImageSplit] = 1
            }
        };

        if (generation == QueryReviewGenerations.FourRootDiscovery)
        {
            foreach (string shapeId in SuccessorDiscoveryShapeIds)
            {
                expectedRoles[shapeId] = shapeId == CrossFamilySubtypesEmpty
                    ? EmptyPagedRoles()
                    : StandardPagedRoles();
            }
        }

        return expectedRoles;
    }

    private static void ValidateCommandOrder(
        IReadOnlyList<CapturedCommand> commands,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> expectedRoles)
    {
        foreach ((string shapeId, IReadOnlyDictionary<string, int> roles) in expectedRoles)
        {
            string[] expected = roles.Keys.ToArray();
            CapturedCommand[] actual = commands
                .Where(command => command.ShapeId == shapeId)
                .OrderBy(command => command.ShapeSequence)
                .ToArray();

            if (!actual.Select(command => command.ShapeSequence)
                    .SequenceEqual(Enumerable.Range(1, expected.Length)) ||
                !actual.Select(command => command.CommandRole).SequenceEqual(expected))
            {
                throw new SqlCaptureValidationException(
                    $"{shapeId}: expected command order [{string.Join(", ", expected)}], " +
                    $"actual [{string.Join(", ", actual.Select(command => command.CommandRole))}].");
            }
        }
    }

    private static void ValidateTypedParameters(IReadOnlyList<CapturedCommand> commands)
    {
        var parameters = commands.SelectMany(command => command.Parameters).ToArray();

        if (parameters.Length == 0)
        {
            throw new SqlCaptureValidationException("No typed production parameters were captured.");
        }

        var incomplete = parameters.Where(parameter =>
                string.IsNullOrWhiteSpace(parameter.Name) ||
                string.IsNullOrWhiteSpace(parameter.ClrType) ||
                string.IsNullOrWhiteSpace(parameter.DbType) ||
                string.IsNullOrWhiteSpace(parameter.NpgsqlDbType))
            .ToArray();

        if (incomplete.Length > 0)
        {
            throw new SqlCaptureValidationException(
                $"{incomplete.Length} captured parameter(s) lack required type metadata.");
        }
    }

    private static void ValidatePublicVisibilityAndOrdering(
        IReadOnlyList<CapturedCommand> commands)
    {
        var publicCommands = commands.Where(command =>
            command.CommandRole is CommandRoles.FilteredCount or
                CommandRoles.PageRoot or
                CommandRoles.ComparableSource or
                CommandRoles.ComparableRankedRoot);

        foreach (var command in publicCommands)
        {
            if (!command.CommandText.Contains("\"Status\" = 'Active'", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: Active visibility predicate is missing.");
            }
        }

        var orderedCommands = commands.Where(command =>
            command.CommandRole is CommandRoles.PageRoot or
                CommandRoles.ComparableRankedRoot);

        foreach (var command in orderedCommands)
        {
            if (!command.CommandText.Contains("ORDER BY", StringComparison.Ordinal) ||
                !command.CommandText.Contains("\"CreatedAtUtc\" DESC", StringComparison.Ordinal) ||
                !command.CommandText.Contains("\"Id\" DESC", StringComparison.Ordinal) ||
                !command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: deterministic ordering or SQL limit is missing.");
            }
        }

        AssertPriceDirection(commands, P1, descending: false);
        AssertPriceDirection(commands, P2, descending: true);
        AssertPriceDirection(commands, CommercialOfficeFirstPage, descending: false);
        AssertPriceDirection(commands, CommercialOfficeRootFirstPage, descending: false);
        AssertPriceDirection(commands, LandBuildingPlotFirstPage, descending: true);
        AssertPriceDirection(commands, LandBuildingPlotRootFirstPage, descending: true);
    }

    private static void AssertPriceDirection(
        IReadOnlyList<CapturedCommand> commands,
        string shapeId,
        bool descending)
    {
        foreach (var command in commands.Where(command =>
                     command.ShapeId == shapeId &&
                     command.CommandRole == CommandRoles.PageRoot))
        {
            var orderByClauses = command.CommandText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("ORDER BY ", StringComparison.Ordinal))
                .ToArray();

            if (orderByClauses.Length == 0 ||
                orderByClauses.Any(orderByClause =>
                    !HasExpectedPriceOrdering(orderByClause, descending)))
            {
                throw new SqlCaptureValidationException(
                    $"{shapeId}/{command.CommandRole}: expected price direction was not captured.");
            }
        }
    }

    private static void ValidatePagedFiltersAndChildLoading(
        IReadOnlyList<CapturedCommand> commands)
    {
        AssertSelectionPredicate(commands, P1, "\"Currency\" =");
        AssertSelectionPredicate(commands, P2, "\"Currency\" =");
        AssertSelectionPredicate(commands, AgencyShapeId, "\"AgencyId\" =");
        AssertSelectionPredicate(commands, AgencyCommercialShopFirstPage, "\"AgencyId\" =");
        AssertSelectionPredicate(commands, AgencyLandAgriculturalDeepPage, "\"AgencyId\" =");

        AssertSelectionPredicate(commands, R1, "\"AreaSquareMeters\" >=");
        AssertSelectionPredicate(commands, R1, "\"AreaSquareMeters\" <=");
        AssertSelectionPredicate(commands, R1, "\"Rooms\" IS NOT NULL");
        AssertSelectionPredicate(commands, R1, "\"Rooms\" >=");
        AssertSelectionPredicate(commands, R1, "\"Rooms\" <=");

        AssertSelectionPredicate(commands, L1, "\"City\" ILIKE");
        AssertSelectionPredicate(commands, L1, "\"Municipality\" ILIKE");
        AssertSelectionPredicate(commands, L1, "\"Neighborhood\" ILIKE");
        AssertSelectionPredicate(commands, L1, "\"LanguageCode\" ILIKE");
        AssertSelectionPredicate(commands, L1, "COLLATE \"C\"");
        AssertSelectionPredicate(commands, L1, "GROUP BY");
        AssertSelectionPredicate(commands, L1, "min(CASE");

        AssertSelectionPredicate(commands, Q1, "\"LanguageCode\" ILIKE");
        AssertSelectionPredicate(commands, Q1, "COLLATE \"C\"");
        AssertSelectionPredicate(commands, Q1, "GROUP BY");
        AssertSelectionPredicate(commands, Q1, "min(CASE");

        foreach (string locationShape in new[]
                 {
                     CommercialShopLocation,
                     LandAgriculturalLocation
                 })
        {
            AssertSelectionPredicate(commands, locationShape, "\"City\" ILIKE");
            AssertSelectionPredicate(commands, locationShape, "\"Municipality\" ILIKE");
            AssertSelectionPredicate(commands, locationShape, "\"LanguageCode\" ILIKE");
            AssertSelectionPredicate(commands, locationShape, "COLLATE \"C\"");
            AssertSelectionPredicate(commands, locationShape, "GROUP BY");
            AssertSelectionPredicate(commands, locationShape, "min(CASE");
        }

        foreach (CapturedCommand command in commands.Where(command =>
                     command.CommandRole is CommandRoles.TranslationSplit or
                         CommandRoles.ImageSplit))
        {
            string childTable = command.CommandRole == CommandRoles.TranslationSplit
                ? "ListingTranslations"
                : "ListingImages";

            if (!command.CommandText.Contains(
                    $"INNER JOIN \"{childTable}\"",
                    StringComparison.Ordinal) ||
                !command.CommandText.Contains("ANY", StringComparison.Ordinal) ||
                command.Parameters.Count != 1 ||
                !string.Equals(
                    command.Parameters[0].ClrType,
                    typeof(Guid[]).FullName,
                    StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: child loading is not " +
                    "restricted to one selected-page UUID array. " +
                    $"Parameters={command.Parameters.Count}; " +
                    $"ClrTypes=[{string.Join(", ", command.Parameters.Select(parameter => parameter.ClrType))}]; " +
                    $"NpgsqlTypes=[{string.Join(", ", command.Parameters.Select(parameter => parameter.NpgsqlDbType))}].");
            }

            if (command.CommandText.Contains(
                    "\"Status\" = 'Active'",
                    StringComparison.Ordinal) ||
                command.CommandText.Contains(" ILIKE ", StringComparison.Ordinal) ||
                command.CommandText.Contains("COLLATE \"C\"", StringComparison.Ordinal) ||
                command.CommandText.Contains("CASE", StringComparison.Ordinal) ||
                command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: child loading repeats " +
                    "public candidate selection or pagination.");
            }
        }
    }

    private static void AssertSelectionPredicate(
        IReadOnlyList<CapturedCommand> commands,
        string shapeId,
        string expectedSql)
    {
        foreach (CapturedCommand command in commands.Where(command =>
                     command.ShapeId == shapeId &&
                     (command.CommandRole is CommandRoles.FilteredCount or
                         CommandRoles.PageRoot)))
        {
            if (!command.CommandText.Contains(expectedSql, StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{shapeId}/{command.CommandRole}: expected selection predicate " +
                    $"'{expectedSql}' is missing.");
            }
        }
    }

    private static void ValidateSetBasedEffectiveTranslationFiltering(
        IReadOnlyList<CapturedCommand> commands)
    {
        foreach (CapturedCommand command in commands.Where(command =>
                     (command.ShapeId is L1 or Q1) &&
                     (command.CommandRole is CommandRoles.FilteredCount or
                         CommandRoles.PageRoot)))
        {
            string sql = command.CommandText;
            string matchingPredicate = command.ShapeId == Q1
                ? "l0.\"Title\" ILIKE"
                : "l0.\"City\" ILIKE";

            int selectedKeyJoin = sql.LastIndexOf(
                "\"LanguageSelectionKey\"",
                StringComparison.Ordinal);
            int matchingPredicatePosition = sql.IndexOf(
                matchingPredicate,
                StringComparison.Ordinal);

            if (!sql.Contains("END ||", StringComparison.Ordinal) ||
                !sql.Contains("GROUP BY", StringComparison.Ordinal) ||
                CountOccurrences(sql, "\"ListingId\" IN (") < 2 ||
                CountOccurrences(sql, "\"Status\" = 'Active'") < 3 ||
                selectedKeyJoin < 0 ||
                matchingPredicatePosition <= selectedKeyJoin)
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: effective-translation " +
                    "selection is not candidate-restricted and completed before matching.");
            }

            if (sql.Contains("ORDER BY CASE", StringComparison.Ordinal) ||
                sql.Contains("LIMIT 1", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: the broad correlated " +
                    "effective-translation probe remains in corrected SQL.");
            }
        }
    }

    private static int CountOccurrences(string value, string expected)
    {
        int count = 0;
        int position = 0;

        while ((position = value.IndexOf(
                   expected,
                   position,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += expected.Length;
        }

        return count;
    }

    private static bool HasExpectedPriceOrdering(string orderByClause, bool descending)
    {
        var orderingKeys = orderByClause["ORDER BY ".Length..]
            .Split(',', StringSplitOptions.TrimEntries);

        if (orderingKeys.Length < 3)
        {
            return false;
        }

        var priceKey = orderingKeys[0];
        var hasExpectedPriceDirection = descending
            ? priceKey.EndsWith(".\"Price\" DESC", StringComparison.Ordinal)
            : priceKey.EndsWith(".\"Price\"", StringComparison.Ordinal) &&
              !priceKey.EndsWith(".\"Price\" DESC", StringComparison.Ordinal);

        return hasExpectedPriceDirection &&
               orderingKeys[1].EndsWith(".\"CreatedAtUtc\" DESC", StringComparison.Ordinal) &&
               orderingKeys[2].EndsWith(".\"Id\" DESC", StringComparison.Ordinal);
    }

    private static void ValidateTextSearch(IReadOnlyList<CapturedCommand> commands)
    {
        foreach (var command in commands.Where(command =>
                     command.ShapeId == Q1 &&
                     (command.CommandRole is CommandRoles.FilteredCount or
                         CommandRoles.PageRoot)))
        {
            foreach (var field in new[] { "Title", "City", "Municipality", "Neighborhood" })
            {
                if (!command.CommandText.Contains(
                        $"\"{field}\" ILIKE",
                        StringComparison.Ordinal))
                {
                    throw new SqlCaptureValidationException(
                        $"{Q1}/{command.CommandRole}: locked q field '{field}' is missing.");
                }
            }

            if (command.CommandText.Contains("\"Description\" ILIKE", StringComparison.Ordinal) ||
                command.CommandText.Contains("\"AddressLine\" ILIKE", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{Q1}/{command.CommandRole}: an excluded q field appears in the predicate.");
            }
        }
    }

    private static void ValidateComparableCandidateAndChildLoading(
        IReadOnlyList<CapturedCommand> commands)
    {
        CapturedCommand rankedRoot = commands.Single(command =>
            command.ShapeId == ComparableShapeId &&
            command.CommandRole == CommandRoles.ComparableRankedRoot);

        string rankedSql = rankedRoot.CommandText;
        int selectedKeyJoin = rankedSql.LastIndexOf(
            "\"LanguageSelectionKey\"",
            StringComparison.Ordinal);

        int cityMatch = rankedSql.IndexOf(
            "\"City\" ILIKE @cityPattern",
            StringComparison.Ordinal);

        string[] scalarEligibilityPredicates =
        [
            "\"Status\" = 'Active'",
            "\"Id\" <>",
            "\"ListingType\" =",
            "\"PropertyType\" =",
            "\"Currency\" =",
            "\"Price\" > 0.0",
            "\"AreaSquareMeters\" > 0.0"
        ];

        if (scalarEligibilityPredicates.Any(predicate =>
                !rankedSql.Contains(predicate, StringComparison.Ordinal)) ||
            CountOccurrences(rankedSql, "\"ListingId\" IN (") < 2 ||
            CountOccurrences(rankedSql, "\"Status\" = 'Active'") < 3 ||
            !rankedSql.Contains("END ||", StringComparison.Ordinal) ||
            !rankedSql.Contains("GROUP BY", StringComparison.Ordinal) ||
            selectedKeyJoin < 0 ||
            cityMatch <= selectedKeyJoin)
        {
            throw new SqlCaptureValidationException(
                $"{ComparableShapeId}/{rankedRoot.CommandRole}: effective-translation " +
                "selection is not restricted by the complete scalar-eligible candidate relation.");
        }

        if (rankedSql.Contains(
                "ROW_NUMBER() OVER(PARTITION BY",
                StringComparison.Ordinal))
        {
            throw new SqlCaptureValidationException(
                $"{ComparableShapeId}/{rankedRoot.CommandRole}: global translation " +
                "ranking remains in the corrected comparable root.");
        }

        foreach (string rankingFragment in new[]
                 {
                     "abs(",
                     "\"Price\" /",
                     "\"CreatedAtUtc\" DESC",
                     "\"Id\" DESC"
                 })
        {
            if (!rankedSql.Contains(rankingFragment, StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{ComparableShapeId}/{rankedRoot.CommandRole}: locked comparable " +
                    $"ranking fragment '{rankingFragment}' is missing.");
            }
        }

        foreach (var command in commands.Where(command =>
                     command.CommandRole is CommandRoles.ComparableTranslationSplit or
                          CommandRoles.ComparableImageSplit))
        {
            string childTable = command.CommandRole ==
                CommandRoles.ComparableTranslationSplit
                    ? "ListingTranslations"
                    : "ListingImages";

            if (!command.CommandText.Contains(
                    $"INNER JOIN \"{childTable}\"",
                    StringComparison.Ordinal) ||
                !command.CommandText.Contains("ANY", StringComparison.Ordinal) ||
                !command.CommandText.Contains("ORDER BY", StringComparison.Ordinal) ||
                !command.CommandText.Contains("\"ListingId\"", StringComparison.Ordinal) ||
                (command.CommandRole == CommandRoles.ComparableImageSplit &&
                 !command.CommandText.Contains("\"SortOrder\"", StringComparison.Ordinal)) ||
                command.Parameters.Count != 1 ||
                !string.Equals(
                    command.Parameters[0].ClrType,
                    typeof(Guid[]).FullName,
                    StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{ComparableShapeId}/{command.CommandRole}: child loading is not " +
                    "restricted to one selected-comparable UUID array with deterministic child ordering.");
            }

            if (command.CommandText.Contains("\"Status\" = 'Active'", StringComparison.Ordinal) ||
                command.CommandText.Contains(" ILIKE ", StringComparison.Ordinal) ||
                command.CommandText.Contains("COLLATE \"C\"", StringComparison.Ordinal) ||
                command.CommandText.Contains("CASE", StringComparison.Ordinal) ||
                command.CommandText.Contains("GROUP BY", StringComparison.Ordinal) ||
                command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{ComparableShapeId}/{command.CommandRole}: child loading repeats " +
                    "comparable candidate selection, ranking, or limiting.");
            }
        }
    }

    internal static void ValidateSuccessorSubtypePredicates(
        IReadOnlyList<CapturedCommand> commands)
    {
        string[] commercialShapes =
        [
            CommercialUnknownFirstPage,
            CommercialOfficeFirstPage,
            CommercialOfficeRootFirstPage,
            CommercialShopLocation,
            CommercialOtherDeepPage,
            AgencyCommercialShopFirstPage,
            CrossFamilySubtypesEmpty
        ];
        string[] landShapes =
        [
            LandUnknownFirstPage,
            LandBuildingPlotFirstPage,
            LandBuildingPlotRootFirstPage,
            LandAgriculturalLocation,
            LandOtherDeepPage,
            AgencyLandAgriculturalDeepPage,
            CrossFamilySubtypesEmpty
        ];

        foreach (string shapeId in commercialShapes)
        {
            AssertSubtypeRootAndChildPredicate(
                commands,
                shapeId,
                "Commercial",
                "ListingCommercialDetails",
                "CommercialType");
        }

        foreach (string shapeId in landShapes)
        {
            AssertSubtypeRootAndChildPredicate(
                commands,
                shapeId,
                "Land",
                "ListingLandDetails",
                "LandType");
        }
    }

    private static void ValidateSuccessorLocationTranslationFiltering(
        IReadOnlyList<CapturedCommand> commands)
    {
        foreach (CapturedCommand command in commands.Where(command =>
                     (command.ShapeId is CommercialShopLocation or LandAgriculturalLocation) &&
                     (command.CommandRole is CommandRoles.FilteredCount or CommandRoles.PageRoot)))
        {
            string sql = command.CommandText;
            int selectedKeyJoin = sql.LastIndexOf(
                "\"LanguageSelectionKey\"",
                StringComparison.Ordinal);
            int matchingPredicatePosition = sql.IndexOf(
                ".\"City\" ILIKE",
                StringComparison.Ordinal);

            if (!sql.Contains("END ||", StringComparison.Ordinal) ||
                !sql.Contains("GROUP BY", StringComparison.Ordinal) ||
                CountOccurrences(sql, "\"ListingId\" IN (") < 2 ||
                CountOccurrences(sql, "\"Status\" = 'Active'") < 3 ||
                selectedKeyJoin < 0 ||
                matchingPredicatePosition <= selectedKeyJoin)
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: effective-translation selection " +
                    "must finish over the subtype-restricted Active candidate relation before " +
                    "city/municipality matching.");
            }

            if (sql.Contains("ORDER BY CASE", StringComparison.Ordinal) ||
                sql.Contains("LIMIT 1", StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{command.ShapeId}/{command.CommandRole}: broad correlated effective-" +
                    "translation selection is not accepted.");
            }
        }
    }

    private static void AssertSubtypeRootAndChildPredicate(
        IReadOnlyList<CapturedCommand> commands,
        string shapeId,
        string rootValue,
        string childTable,
        string subtypeColumn)
    {
        CapturedCommand[] selectionCommands = commands.Where(command =>
                command.ShapeId == shapeId &&
                command.CommandRole is CommandRoles.FilteredCount or CommandRoles.PageRoot)
            .ToArray();

        if (selectionCommands.Length != 2)
        {
            throw new SqlCaptureValidationException(
                $"{shapeId}: subtype selection must emit exactly count and page-root commands.");
        }

        foreach (CapturedCommand command in selectionCommands)
        {
            if (!command.CommandText.Contains(
                    $"\"PropertyType\" = '{rootValue}'",
                    StringComparison.Ordinal) ||
                !command.CommandText.Contains(
                    $"\"{childTable}\"",
                    StringComparison.Ordinal) ||
                !command.CommandText.Contains(
                    $"\"{subtypeColumn}\" =",
                    StringComparison.Ordinal))
            {
                throw new SqlCaptureValidationException(
                    $"{shapeId}/{command.CommandRole}: explicit {rootValue} root equality and " +
                    $"matching {childTable}.{subtypeColumn} predicate are both required.");
            }
        }
    }

    private static IReadOnlyDictionary<string, int> StandardPagedRoles()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CommandRoles.FilteredCount] = 1,
            [CommandRoles.PageRoot] = 1,
            [CommandRoles.TranslationSplit] = 1,
            [CommandRoles.ImageSplit] = 1
        };
    }

    private static IReadOnlyDictionary<string, int> EmptyPagedRoles()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CommandRoles.FilteredCount] = 1,
            [CommandRoles.PageRoot] = 1
        };
    }

    private static string FormatRoleCounts(IReadOnlyDictionary<string, int> roles)
    {
        return string.Join(
            ", ",
            roles.OrderBy(role => role.Key, StringComparer.Ordinal)
                .Select(role => $"{role.Key}={role.Value}"));
    }

    private static Guid ListingId(int ordinal)
    {
        return Guid.Parse($"40000000-0000-0000-0000-{ordinal:x12}");
    }

    private static Guid[] ListingIds(
        int startOrdinal,
        int count,
        int step)
    {
        return Enumerable.Range(0, count)
            .Select(index => ListingId(startOrdinal + index * step))
            .ToArray();
    }

    private static LockedQueryShapeExpectation PagedExpectation(
        string shapeId,
        int expectedTotalCount)
    {
        return new LockedQueryShapeExpectation(
            shapeId,
            expectedTotalCount,
            ExpectedItemCount: 20,
            ExpectedPagedIds[shapeId]);
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
}

internal sealed class SqlCaptureValidationException(string message)
    : InvalidOperationException(message);
