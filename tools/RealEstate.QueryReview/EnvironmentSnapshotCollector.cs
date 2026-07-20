using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RealEstate.QueryReview;

internal static class EnvironmentSnapshotCollector
{
    private static readonly string[] RequiredPostgreSqlSettings =
    [
        "autovacuum",
        "default_statistics_target",
        "effective_cache_size",
        "effective_io_concurrency",
        "jit",
        "max_parallel_workers_per_gather",
        "random_page_cost",
        "seq_page_cost",
        "shared_buffers",
        "work_mem"
    ];

    public static async Task<TimeSpan> VacuumAnalyzeAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = "VACUUM (ANALYZE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    public static async Task<BaselineEnvironmentSnapshot> CaptureAsync(
        NpgsqlConnection connection,
        string containerName,
        TimeSpan vacuumAnalyzeDuration,
        CancellationToken cancellationToken = default)
    {
        var git = await CaptureGitAsync(cancellationToken);
        var runtime = await CaptureRuntimeAsync(cancellationToken);
        var docker = await CaptureDockerAsync(containerName, cancellationToken);
        var postgreSql = await CapturePostgreSqlAsync(connection, cancellationToken);

        return new BaselineEnvironmentSnapshot(
            DateTime.UtcNow,
            git,
            runtime,
            docker,
            postgreSql,
            typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "unknown",
            typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString() ?? "unknown",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            DeterministicProfileSeeder.ProfileVersion,
            DeterministicProfileSeeder.CSharpSeed,
            DeterministicProfileSeeder.PostgreSqlSeed,
            vacuumAnalyzeDuration);
    }

    private static async Task<GitEnvironmentSnapshot> CaptureGitAsync(
        CancellationToken cancellationToken)
    {
        var commit = (await RunProcessAsync(
            "git",
            ["rev-parse", "HEAD"],
            cancellationToken)).Trim();
        var branch = (await RunProcessAsync(
            "git",
            ["branch", "--show-current"],
            cancellationToken)).Trim();
        var status = SplitLines(await RunProcessAsync(
            "git",
            ["status", "--short", "--untracked-files=all"],
            cancellationToken));

        if (string.IsNullOrWhiteSpace(commit) || string.IsNullOrWhiteSpace(branch))
        {
            throw new BaselinePlanValidationException(
                "Git commit or branch could not be captured for the official baseline.");
        }

        return new GitEnvironmentSnapshot(commit, branch, status);
    }

    private static async Task<RuntimeEnvironmentSnapshot> CaptureRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var sdkVersion = (await RunProcessAsync(
            "dotnet",
            ["--version"],
            cancellationToken)).Trim();
        var runtimes = SplitLines(await RunProcessAsync(
            "dotnet",
            ["--list-runtimes"],
            cancellationToken));

        return new RuntimeEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.Version.ToString(),
            sdkVersion,
            runtimes,
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
    }

    private static async Task<DockerEnvironmentSnapshot> CaptureDockerAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        const string containerFormat =
            "{{.Id}}|{{.Image}}|{{.Name}}|{{.Created}}|{{.Config.Image}}|" +
            "{{.State.Status}}|{{.HostConfig.Memory}}|{{.HostConfig.NanoCpus}}|" +
            "{{.HostConfig.CpuQuota}}|{{.HostConfig.CpuPeriod}}";
        var containerValues = (await RunProcessAsync(
                "docker",
                ["inspect", "--format", containerFormat, containerName],
                cancellationToken))
            .Trim()
            .Split('|');

        if (containerValues.Length != 10)
        {
            throw new BaselinePlanValidationException(
                $"Docker inspect returned {containerValues.Length} values; expected 10.");
        }

        const string dockerInfoFormat =
            "{{.ServerVersion}}|{{.OperatingSystem}}|{{.OSType}}|{{.Architecture}}|" +
            "{{.NCPU}}|{{.MemTotal}}";
        var dockerValues = (await RunProcessAsync(
                "docker",
                ["info", "--format", dockerInfoFormat],
                cancellationToken))
            .Trim()
            .Split('|');

        if (dockerValues.Length != 6)
        {
            throw new BaselinePlanValidationException(
                $"Docker info returned {dockerValues.Length} values; expected 6.");
        }

        var repoDigestsJson = (await RunProcessAsync(
            "docker",
            ["image", "inspect", "--format", "{{json .RepoDigests}}", containerValues[1]],
            cancellationToken)).Trim();
        var repoDigests = string.Equals(repoDigestsJson, "null", StringComparison.Ordinal)
            ? []
            : JsonSerializer.Deserialize<string[]>(repoDigestsJson) ?? [];

        return new DockerEnvironmentSnapshot(
            containerName,
            containerValues[0],
            containerValues[1],
            containerValues[4],
            containerValues[3],
            containerValues[5],
            ParseInt64(containerValues[6], "container memory limit"),
            ParseInt64(containerValues[7], "container NanoCPUs"),
            ParseInt64(containerValues[8], "container CPU quota"),
            ParseInt64(containerValues[9], "container CPU period"),
            dockerValues[0],
            dockerValues[1],
            dockerValues[2],
            dockerValues[3],
            ParseInt32(dockerValues[4], "Docker CPU count"),
            ParseInt64(dockerValues[5], "Docker memory"),
            repoDigests);
    }

    private static async Task<PostgreSqlEnvironmentSnapshot> CapturePostgreSqlAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var identity = await ReadIdentityAsync(connection, cancellationToken);
        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var extensions = await ReadExtensionsAsync(connection, cancellationToken);
        var relations = await ReadRelationsAsync(connection, cancellationToken);
        var indexes = await ReadIndexesAsync(connection, cancellationToken);
        var tableStatistics = await ReadTableStatisticsAsync(connection, cancellationToken);

        return new PostgreSqlEnvironmentSnapshot(
            identity.Version,
            identity.ServerVersion,
            identity.ServerVersionNumber,
            identity.Database,
            identity.DatabaseSizeBytes,
            identity.ActiveVacuumCount,
            settings,
            extensions,
            relations,
            indexes,
            tableStatistics);
    }

    private static async Task<IReadOnlyList<PostgreSqlSettingSnapshot>> ReadSettingsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, setting, unit, source
            FROM pg_settings
            WHERE name = ANY (@names)
            ORDER BY name;
            """;
        command.Parameters.AddWithValue("names", RequiredPostgreSqlSettings);
        var values = new List<PostgreSqlSettingSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new PostgreSqlSettingSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)));
        }

        if (values.Count != RequiredPostgreSqlSettings.Length)
        {
            throw new BaselinePlanValidationException(
                $"Captured {values.Count} PostgreSQL settings; expected " +
                $"{RequiredPostgreSqlSettings.Length}.");
        }

        return values;
    }

    private static async Task<IReadOnlyList<PostgreSqlExtensionSnapshot>> ReadExtensionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT extname, extversion FROM pg_extension ORDER BY extname;";
        var values = new List<PostgreSqlExtensionSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new PostgreSqlExtensionSnapshot(reader.GetString(0), reader.GetString(1)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<PostgreSqlRelationSnapshot>> ReadRelationsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT n.nspname,
                   c.relname,
                   c.relkind::text,
                   pg_relation_size(c.oid),
                   pg_total_relation_size(c.oid)
            FROM pg_class AS c
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relkind IN ('r', 'i')
            ORDER BY n.nspname, c.relname;
            """;
        var values = new List<PostgreSqlRelationSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new PostgreSqlRelationSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt64(4)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<PostgreSqlIndexSnapshot>> ReadIndexesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.schemaname,
                   i.tablename,
                   i.indexname,
                   i.indexdef,
                   pg_relation_size((quote_ident(i.schemaname) || '.' ||
                                     quote_ident(i.indexname))::regclass)
            FROM pg_indexes AS i
            WHERE i.schemaname = 'public'
            ORDER BY i.tablename, i.indexname;
            """;
        var values = new List<PostgreSqlIndexSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new PostgreSqlIndexSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4)));
        }

        return values;
    }

    private static async Task<IReadOnlyList<PostgreSqlTableStatisticsSnapshot>> ReadTableStatisticsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT schemaname,
                   relname,
                   n_live_tup,
                   n_dead_tup,
                   last_vacuum,
                   last_autovacuum,
                   last_analyze,
                   last_autoanalyze,
                   vacuum_count,
                   autovacuum_count,
                   analyze_count,
                   autoanalyze_count
            FROM pg_stat_user_tables
            ORDER BY schemaname, relname;
            """;
        var values = new List<PostgreSqlTableStatisticsSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new PostgreSqlTableStatisticsSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                GetNullableDateTime(reader, 4),
                GetNullableDateTime(reader, 5),
                GetNullableDateTime(reader, 6),
                GetNullableDateTime(reader, 7),
                reader.GetInt64(8),
                reader.GetInt64(9),
                reader.GetInt64(10),
                reader.GetInt64(11)));
        }

        return values;
    }

    private static async Task<PostgreSqlIdentityRow> ReadIdentityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT version(),
                   current_setting('server_version'),
                   current_setting('server_version_num'),
                   current_database(),
                   pg_database_size(current_database()),
                   (SELECT count(*)::int
                    FROM pg_stat_progress_vacuum
                    WHERE datname = current_database());
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new BaselinePlanValidationException(
                "PostgreSQL returned no environment identity row.");
        }

        return new PostgreSqlIdentityRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetInt32(5));
    }

    private static DateTime? GetNullableDateTime(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new BaselinePlanValidationException(
                $"Failed to start required environment command '{fileName}'.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await standardOutput;
        var error = await standardError;

        if (process.ExitCode != 0)
        {
            throw new BaselinePlanValidationException(
                $"Environment command '{fileName}' exited {process.ExitCode}: {error.Trim()}");
        }

        return output;
    }

    private static IReadOnlyList<string> SplitLines(string value)
    {
        return value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static long ParseInt64(string value, string label)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new BaselinePlanValidationException(
                $"Docker returned invalid {label} value '{value}'.");
        }

        return result;
    }

    private static int ParseInt32(string value, string label)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new BaselinePlanValidationException(
                $"Docker returned invalid {label} value '{value}'.");
        }

        return result;
    }

    private sealed record PostgreSqlIdentityRow(
        string Version,
        string ServerVersion,
        string ServerVersionNumber,
        string Database,
        long DatabaseSizeBytes,
        int ActiveVacuumCount);
}
