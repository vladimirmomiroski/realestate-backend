using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Repositories;

namespace RealEstate.QueryReview;

internal static class QueryShapeDefinitions
{
    public const string LogicalRunId = "chapter-10f-v1-production-sql";
    public const string AgencyShapeId = "A1";
    public const string ComparableShapeId = "C1";

    private const string N1 = "N1";
    private const string P1 = "P1";
    private const string P2 = "P2";
    private const string R1 = "R1";
    private const string L1 = "L1";
    private const string Q1 = "Q1";

    private static readonly Guid AgencyOneId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

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

    public static async Task<IReadOnlyList<QueryShapeResult>> ExecuteAsync(
        RealEstateDbContext dbContext,
        ProductionCommandCaptureInterceptor interceptor,
        CancellationToken cancellationToken = default)
    {
        var listingRepository = new ListingRepository(dbContext);
        var agencyRepository = new AgencyRepository(dbContext);
        var results = new List<QueryShapeResult>();

        results.Add(await ExecutePagedAsync(
            N1,
            listingRepository,
            interceptor,
            new GetListingsQuery
            {
                LanguageCode = "en",
                Sort = "newest",
                SortOption = ListingSortOption.Newest,
                Page = 1,
                PageSize = 20
            },
            expectedTotalCount: 70_000,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            P1,
            listingRepository,
            interceptor,
            new GetListingsQuery
            {
                LanguageCode = "en",
                Currency = "EUR",
                Sort = "priceAsc",
                SortOption = ListingSortOption.PriceAsc,
                Page = 1,
                PageSize = 20
            },
            expectedTotalCount: 23_334,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            P2,
            listingRepository,
            interceptor,
            new GetListingsQuery
            {
                LanguageCode = "en",
                Currency = "EUR",
                Sort = "priceDesc",
                SortOption = ListingSortOption.PriceDesc,
                Page = 1,
                PageSize = 20
            },
            expectedTotalCount: 23_334,
            expectedItemCount: 20,
            cancellationToken));

        using (interceptor.BeginShape(AgencyShapeId))
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
                new GetListingsQuery
                {
                    AgencyId = AgencyOneId,
                    LanguageCode = "en",
                    Sort = "newest",
                    SortOption = ListingSortOption.Newest,
                    Page = 1,
                    PageSize = 20
                },
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
            new GetListingsQuery
            {
                LanguageCode = "en",
                MinAreaSquareMeters = 80m,
                MaxAreaSquareMeters = 89m,
                MinRooms = 2m,
                MaxRooms = 3m,
                Sort = "newest",
                SortOption = ListingSortOption.Newest,
                Page = 1,
                PageSize = 20
            },
            expectedTotalCount: 1_050,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            L1,
            listingRepository,
            interceptor,
            new GetListingsQuery
            {
                LanguageCode = "en",
                City = "AuditCity10F",
                Municipality = "AuditMunicipality10F",
                Neighborhood = "AuditNeighborhood10F",
                Sort = "newest",
                SortOption = ListingSortOption.Newest,
                Page = 1,
                PageSize = 20
            },
            expectedTotalCount: 140,
            expectedItemCount: 20,
            cancellationToken));

        results.Add(await ExecutePagedAsync(
            Q1,
            listingRepository,
            interceptor,
            new GetListingsQuery
            {
                LanguageCode = "en",
                SearchText = "needle10f",
                Sort = "newest",
                SortOption = ListingSortOption.Newest,
                Page = 1,
                PageSize = 20
            },
            expectedTotalCount: 120,
            expectedItemCount: 20,
            cancellationToken));

        using (interceptor.BeginShape(ComparableShapeId))
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

        ValidateCapturedCommands(interceptor.Commands);
        return results;
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
        ProductionCommandCaptureInterceptor interceptor,
        GetListingsQuery query,
        int expectedTotalCount,
        int expectedItemCount,
        CancellationToken cancellationToken)
    {
        using (interceptor.BeginShape(shapeId))
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

        return new QueryShapeResult(
            shapeId,
            expectedTotalCount,
            result.TotalCount,
            expectedItemCount,
            result.Items.Count,
            result.Items.Select(item => item.Id).ToArray());
    }

    private static void ValidateCapturedCommands(IReadOnlyList<CapturedCommand> commands)
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

        if (commands.Count != 33)
        {
            throw new SqlCaptureValidationException(
                $"Expected exactly 33 production commands, captured {commands.Count}.");
        }

        ValidateTypedParameters(commands);
        ValidatePublicVisibilityAndOrdering(commands);
        ValidateTextSearch(commands);
        ValidateComparableLimitBeforeIncludes(commands);
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
            command.CommandRole != CommandRoles.AgencyExistence);

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
                CommandRoles.TranslationSplit or
                CommandRoles.ImageSplit or
                CommandRoles.ComparableRankedRoot or
                CommandRoles.ComparableTranslationSplit or
                CommandRoles.ComparableImageSplit);

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
    }

    private static void AssertPriceDirection(
        IReadOnlyList<CapturedCommand> commands,
        string shapeId,
        bool descending)
    {
        foreach (var command in commands.Where(command =>
                     command.ShapeId == shapeId &&
                     command.CommandRole != CommandRoles.FilteredCount))
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
        foreach (var command in commands.Where(command => command.ShapeId == Q1))
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

    private static void ValidateComparableLimitBeforeIncludes(
        IReadOnlyList<CapturedCommand> commands)
    {
        foreach (var command in commands.Where(command =>
                     command.CommandRole is CommandRoles.ComparableTranslationSplit or
                         CommandRoles.ComparableImageSplit))
        {
            var limitPosition = command.CommandText.IndexOf("LIMIT", StringComparison.Ordinal);
            var includePosition = command.CommandText.IndexOf(
                command.CommandRole == CommandRoles.ComparableTranslationSplit
                    ? "INNER JOIN \"ListingTranslations\""
                    : "INNER JOIN \"ListingImages\"",
                StringComparison.Ordinal);

            if (limitPosition < 0 || includePosition < 0 || limitPosition > includePosition)
            {
                throw new SqlCaptureValidationException(
                    $"{ComparableShapeId}/{command.CommandRole}: candidate LIMIT does not precede aggregate loading.");
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
