using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.QueryReview;

internal static class Program
{
    private const string RequiredDatabasePrefix = "realestate_queryreview";
    private const int RequiredPostgreSqlMajorVersion = 16;

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("RealEstate Chapter 10F query-review tool");

        QueryReviewCommand? command = null;

        try
        {
            if (!QueryReviewOptions.TryParse(args, out var options, out var error))
            {
                Console.Error.WriteLine($"Error: {error}");
                Console.Error.WriteLine(QueryReviewOptions.Usage);
                return 2;
            }

            command = options!.Command;
            return await RunAsync(options);
        }
        finally
        {
            PrintOperationsNotice(command);
        }
    }

    private static async Task<int> RunAsync(QueryReviewOptions options)
    {
        NpgsqlConnectionStringBuilder connectionStringBuilder;

        try
        {
            connectionStringBuilder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Error: The connection string is invalid: {exception.Message}");
            return 2;
        }

        var requestedDatabase = connectionStringBuilder.Database;

        if (string.IsNullOrWhiteSpace(requestedDatabase))
        {
            Console.Error.WriteLine("Error: The connection string must name a database explicitly.");
            return 3;
        }

        if (!requestedDatabase.StartsWith(RequiredDatabasePrefix, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"Error: Database '{requestedDatabase}' is rejected. Its name must start with " +
                $"'{RequiredDatabasePrefix}'.");
            return 3;
        }

        Console.WriteLine("Disposable-database confirmation: accepted.");
        Console.WriteLine($"Future output directory: {options.OutputDirectory}");
        Console.WriteLine("Opening the requested database through RealEstateDbContext and Npgsql...");

        try
        {
            var dbContextOptions = new DbContextOptionsBuilder<RealEstateDbContext>()
                .UseNpgsql(options.ConnectionString)
                .Options;

            await using var dbContext = new RealEstateDbContext(dbContextOptions);
            await dbContext.Database.OpenConnectionAsync();

            var connection = dbContext.Database.GetDbConnection();

            if (connection is not NpgsqlConnection npgsqlConnection)
            {
                Console.Error.WriteLine(
                    $"Error: Expected an Npgsql connection but received '{connection.GetType().FullName}'.");
                return 4;
            }

            var identity = await ReadDatabaseIdentityAsync(npgsqlConnection);

            if (!string.Equals(identity.Database, requestedDatabase, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Error: Connected database identity '{identity.Database}' does not match the " +
                    $"requested database '{requestedDatabase}'.");
                return 4;
            }

            if (!identity.Database.StartsWith(RequiredDatabasePrefix, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Error: Connected database '{identity.Database}' does not have the required " +
                    $"'{RequiredDatabasePrefix}' prefix.");
                return 4;
            }

            if (!int.TryParse(
                    identity.ServerVersionNumber,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var numericServerVersion))
            {
                Console.Error.WriteLine(
                    $"Error: PostgreSQL returned an unrecognized server version number " +
                    $"'{identity.ServerVersionNumber}'.");
                return 4;
            }

            var serverMajorVersion = numericServerVersion / 10_000;

            Console.WriteLine("Connected database identity verified:");
            Console.WriteLine($"  Database: {identity.Database}");
            Console.WriteLine($"  PostgreSQL version: {npgsqlConnection.PostgreSqlVersion}");

            if (serverMajorVersion != RequiredPostgreSqlMajorVersion)
            {
                Console.Error.WriteLine(
                    $"Error: PostgreSQL major version {serverMajorVersion} is rejected. " +
                    $"Chapter 10F requires PostgreSQL {RequiredPostgreSqlMajorVersion}.");
                return 5;
            }

            return options.Command switch
            {
                QueryReviewCommand.Doctor => RunDoctor(),
                QueryReviewCommand.ProfileCreate => await CreateProfileAsync(
                    dbContext,
                    npgsqlConnection),
                QueryReviewCommand.ProfileVerify => await VerifyProfileAsync(npgsqlConnection),
                QueryReviewCommand.CaptureSql => await CaptureSqlAsync(
                    options,
                    identity.Database,
                    npgsqlConnection.PostgreSqlVersion.ToString(),
                    npgsqlConnection),
                _ => throw new InvalidOperationException(
                    $"Unsupported command '{options.Command}'.")
            };
        }
        catch (ProfileInvariantException exception)
        {
            Console.Error.WriteLine($"Profile verification failed: {exception.Message}");
            return 6;
        }
        catch (SqlCaptureValidationException exception)
        {
            Console.Error.WriteLine($"SQL capture validation failed: {exception.Message}");
            return 7;
        }
        catch (Exception exception) when (
            exception is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            Console.Error.WriteLine(
                "Error: The database operation through RealEstateDbContext/Npgsql failed: " +
                exception.Message);
            return 4;
        }
    }

    private static int RunDoctor()
    {
        Console.WriteLine("Doctor result: SUCCESS. PostgreSQL 16 and database safety checks passed.");
        return 0;
    }

    private static async Task<int> CreateProfileAsync(
        RealEstateDbContext dbContext,
        NpgsqlConnection connection)
    {
        Console.WriteLine("Applying existing committed EF migrations...");

        var migrationStopwatch = Stopwatch.StartNew();
        await dbContext.Database.MigrateAsync();
        migrationStopwatch.Stop();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        if (pendingMigrations.Any())
        {
            throw new ProfileInvariantException(
                "Existing EF migrations did not fully apply. Pending migrations: " +
                string.Join(", ", pendingMigrations));
        }

        Console.WriteLine($"Existing migration application duration: {migrationStopwatch.Elapsed}.");
        Console.WriteLine($"Profile version: {DeterministicProfileSeeder.ProfileVersion}");
        Console.WriteLine($"C# seed recorded: {DeterministicProfileSeeder.CSharpSeed}");
        Console.WriteLine($"PostgreSQL seed command: SELECT setseed({DeterministicProfileSeeder.PostgreSqlSeed});");
        Console.WriteLine("Creating deterministic set-based profile...");

        var profileStopwatch = Stopwatch.StartNew();
        var verification = await DeterministicProfileSeeder.CreateAsync(connection);
        profileStopwatch.Stop();

        PrintInvariantTotals(verification);
        Console.WriteLine($"Profile creation duration: {profileStopwatch.Elapsed}.");
        Console.WriteLine("Profile create result: SUCCESS. All exact invariants passed.");
        return 0;
    }

    private static async Task<int> VerifyProfileAsync(NpgsqlConnection connection)
    {
        Console.WriteLine("Running read-only deterministic profile verification...");

        var verification = await ProfileInvariants.VerifyAsync(connection);
        verification.EnsureValid();

        PrintInvariantTotals(verification);
        Console.WriteLine("Profile verify result: SUCCESS. All exact invariants passed.");
        return 0;
    }

    private static async Task<int> CaptureSqlAsync(
        QueryReviewOptions options,
        string database,
        string postgreSqlVersion,
        NpgsqlConnection verificationConnection)
    {
        Console.WriteLine("Verifying the deterministic profile before production SQL capture...");

        var verification = await ProfileInvariants.VerifyAsync(verificationConnection);
        verification.EnsureValid();

        Console.WriteLine(
            $"Profile verification: SUCCESS ({verification.Invariants.Count}/" +
            $"{verification.Invariants.Count} invariants).");

        QueryShapeDefinitions.EnsureOutputIsOutsideRepository(options.OutputDirectory);

        var interceptor = new ProductionCommandCaptureInterceptor(
            QueryShapeDefinitions.LogicalRunId);

        var captureOptions = new DbContextOptionsBuilder<RealEstateDbContext>()
            .UseNpgsql(options.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var captureDbContext = new RealEstateDbContext(captureOptions);

        IReadOnlyList<QueryShapeResult> shapeResults =
            await QueryShapeDefinitions.ExecuteAsync(captureDbContext, interceptor);

        var commands = interceptor.Commands;
        var captureRun = new SqlCaptureRun(
            QueryShapeDefinitions.LogicalRunId,
            DeterministicProfileSeeder.ProfileVersion,
            DeterministicProfileSeeder.CSharpSeed,
            DeterministicProfileSeeder.PostgreSqlSeed,
            database,
            postgreSqlVersion,
            shapeResults,
            commands);

        var outputPath = await SqlCaptureOutput.WriteAsync(
            captureRun,
            options.OutputDirectory);

        Console.WriteLine($"Logical run ID: {captureRun.LogicalRunId}");
        Console.WriteLine("Captured logical shapes and command roles:");

        foreach (var shape in shapeResults)
        {
            var roleCounts = commands
                .Where(command => command.ShapeId == shape.ShapeId)
                .GroupBy(command => command.CommandRole, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}={group.Count()}");

            Console.WriteLine(
                $"  {shape.ShapeId}: {string.Join(", ", roleCounts)}; " +
                $"total={shape.ActualTotalCount?.ToString("N0", CultureInfo.InvariantCulture) ?? "n/a"}; " +
                $"items={shape.ActualItemCount}");
        }

        var parameters = commands.SelectMany(command => command.Parameters).ToArray();
        Console.WriteLine(
            $"Typed parameters: {parameters.Length:N0} captured with CLR, DbType, Npgsql, " +
            "nullability, and exact-value metadata.");
        Console.WriteLine($"Complete SQL capture: {outputPath}");
        Console.WriteLine("Capture SQL result: SUCCESS. All command and result validations passed.");
        return 0;
    }

    private static void PrintInvariantTotals(ProfileVerificationResult verification)
    {
        Console.WriteLine($"Exact invariant totals ({verification.Invariants.Count}):");

        foreach (var invariant in verification.Invariants)
        {
            Console.WriteLine($"  {invariant.Name}: {invariant.Actual:N0}");
        }
    }

    private static async Task<DatabaseIdentity> ReadDatabaseIdentityAsync(
        NpgsqlConnection connection)
    {
        await using var identityCommand = connection.CreateCommand();
        identityCommand.CommandText =
            "SELECT current_database(), current_user, current_setting('server_version_num')";

        await using var reader = await identityCommand.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("PostgreSQL returned no database identity row.");
        }

        return new DatabaseIdentity(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    private static void PrintOperationsNotice(QueryReviewCommand? command)
    {
        Console.WriteLine();

        switch (command)
        {
            case QueryReviewCommand.ProfileCreate:
                Console.WriteLine(
                    "No migration was created, and no SQL capture, EXPLAIN, benchmark, or index " +
                    "operation occurred. This command may only apply existing migrations and seed " +
                    "the deterministic profile.");
                break;

            case QueryReviewCommand.ProfileVerify:
                Console.WriteLine(
                    "Profile verification was read-only. No migration, seeding, SQL capture, " +
                    "EXPLAIN, benchmark, or index operation occurred.");
                break;

            case QueryReviewCommand.CaptureSql:
                Console.WriteLine(
                    "The verified production queries were executed only for typed SQL capture. " +
                    "No migration, seeding, EXPLAIN, performance benchmark, or index operation " +
                    "occurred.");
                break;

            default:
                Console.WriteLine(
                    "No migration, seeding, EXPLAIN, benchmark, or index operation occurred.");
                break;
        }
    }

    private sealed record DatabaseIdentity(
        string Database,
        string User,
        string ServerVersionNumber);
}
