using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace RealEstate.QueryReview;

internal static class ExplainRunner
{
    private const string ExplainPrefix =
        "EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, FORMAT JSON)\n";
    private const int ExpectedCommandCount = 33;
    private const int ExpectedParameterCount = 152;
    private const int WarmUpRunsPerCommand = 1;
    private const int MeasuredRunsPerCommand = 5;

    public static async Task<RawBaselineManifest> RunAsync(
        NpgsqlConnection connection,
        NpgsqlConnectionStringBuilder connectionStringBuilder,
        ProductionCaptureSession captureSession,
        BaselineEnvironmentSnapshot environment,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateConnectionSettings(connectionStringBuilder);
        ValidateCaptureSession(captureSession);

        if (environment.PostgreSql.ActiveVacuumCount != 0)
        {
            throw new BaselinePlanValidationException(
                $"An official baseline cannot start while " +
                $"{environment.PostgreSql.ActiveVacuumCount} vacuum operation(s) are active.");
        }

        var startedAtUtc = DateTime.UtcNow;
        var shortCommit = environment.Git.Commit[..Math.Min(8, environment.Git.Commit.Length)];
        var baselineRunId =
            $"{DeterministicProfileSeeder.ProfileVersion}-baseline-" +
            $"{startedAtUtc:yyyyMMddTHHmmssZ}-{shortCommit}";
        var runDirectory = Path.GetFullPath(Path.Combine(outputDirectory, baselineRunId));

        if (Directory.Exists(runDirectory))
        {
            throw new BaselinePlanValidationException(
                $"Baseline output directory '{runDirectory}' already exists.");
        }

        Directory.CreateDirectory(runDirectory);

        var capturedCommandsPath = Path.Combine(runDirectory, "captured-commands.json");
        var environmentPath = Path.Combine(runDirectory, "environment-raw.json");
        await JsonArtifactOutput.WriteAsync(
            capturedCommandsPath,
            captureSession.CaptureRun,
            cancellationToken);
        await JsonArtifactOutput.WriteAsync(environmentPath, environment, cancellationToken);

        var resultSha256 = ComputeSha256(JsonSerializer.Serialize(
            captureSession.CaptureRun.ShapeResults,
            JsonArtifactOutput.SerializerOptions));
        var structuralHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var rowCounts = new Dictionary<string, (long Rows, long Loops)>(StringComparer.Ordinal);
        var samples = new List<RawPlanSample>(ExpectedCommandCount * 6);

        for (var round = 0; round <= MeasuredRunsPerCommand; round++)
        {
            var runKind = round == 0 ? "warmup" : "measured";
            var runNumber = round;

            foreach (var replayableCommand in captureSession.ReplayableCommands)
            {
                var captured = replayableCommand.CapturedCommand;
                var commandKey = CreateCommandKey(captured);
                var sqlSha256 = ComputeSha256(captured.CommandText);
                var parameterSha256 = ComputeParameterSha256(captured.Parameters);
                var planFileName = round == 0 ? "warmup.json" : $"run-{round}.json";
                var relativePlanPath = Path.Combine(
                    "raw-plans",
                    commandKey,
                    planFileName);
                var planPath = Path.Combine(runDirectory, relativePlanPath);
                var execution = await ExecuteExplainAsync(
                    connection,
                    replayableCommand,
                    cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
                await File.WriteAllTextAsync(planPath, execution.RawJson, cancellationToken);

                if (structuralHashes.TryGetValue(commandKey, out var expectedStructuralHash))
                {
                    if (!string.Equals(
                            expectedStructuralHash,
                            execution.StructuralPlanSha256,
                            StringComparison.Ordinal))
                    {
                        throw new BaselinePlanValidationException(
                            $"{commandKey}: structural plan changed between baseline rounds.");
                    }
                }
                else
                {
                    structuralHashes.Add(commandKey, execution.StructuralPlanSha256);
                }

                if (rowCounts.TryGetValue(commandKey, out var expectedRows))
                {
                    if (expectedRows.Rows != execution.ActualRows ||
                        expectedRows.Loops != execution.ActualLoops)
                    {
                        throw new BaselinePlanValidationException(
                            $"{commandKey}: top-level actual rows/loops changed. " +
                            $"Expected {expectedRows.Rows}/{expectedRows.Loops}, actual " +
                            $"{execution.ActualRows}/{execution.ActualLoops}.");
                    }
                }
                else
                {
                    rowCounts.Add(commandKey, (execution.ActualRows, execution.ActualLoops));
                }

                samples.Add(new RawPlanSample(
                    commandKey,
                    captured.ShapeId,
                    captured.ShapeSequence,
                    captured.CommandRole,
                    runKind,
                    runNumber,
                    relativePlanPath.Replace(Path.DirectorySeparatorChar, '/'),
                    sqlSha256,
                    parameterSha256,
                    execution.StructuralPlanSha256,
                    execution.ActualRows,
                    execution.ActualLoops));
            }
        }

        var expectedPlanCount = ExpectedCommandCount *
                                (WarmUpRunsPerCommand + MeasuredRunsPerCommand);

        if (samples.Count != expectedPlanCount)
        {
            throw new BaselinePlanValidationException(
                $"Expected {expectedPlanCount} raw plans, captured {samples.Count}.");
        }

        var manifest = new RawBaselineManifest(
            baselineRunId,
            startedAtUtc,
            DateTime.UtcNow,
            DeterministicProfileSeeder.ProfileVersion,
            DeterministicProfileSeeder.CSharpSeed,
            DeterministicProfileSeeder.PostgreSqlSeed,
            environment.Git.Commit,
            environment.PostgreSql.ServerVersion,
            resultSha256,
            ExpectedCommandCount,
            ExpectedParameterCount,
            WarmUpRunsPerCommand,
            MeasuredRunsPerCommand,
            samples.Count,
            "captured-commands.json",
            "environment-raw.json",
            CredentialScanPassed: true,
            samples);
        var manifestPath = Path.Combine(runDirectory, "manifest.json");
        await JsonArtifactOutput.WriteAsync(manifestPath, manifest, cancellationToken);

        ScanOutputForCredentials(runDirectory, connectionStringBuilder);
        return manifest;
    }

    private static void ValidateConnectionSettings(
        NpgsqlConnectionStringBuilder connectionStringBuilder)
    {
        if (connectionStringBuilder.Multiplexing)
        {
            throw new BaselinePlanValidationException(
                "Official baseline replay rejects Npgsql multiplexing.");
        }

        if (connectionStringBuilder.MaxAutoPrepare > 0)
        {
            throw new BaselinePlanValidationException(
                "Official baseline replay rejects Npgsql auto-prepare.");
        }
    }

    private static void ValidateCaptureSession(ProductionCaptureSession captureSession)
    {
        if (captureSession.CaptureRun.Commands.Count != ExpectedCommandCount ||
            captureSession.ReplayableCommands.Count != ExpectedCommandCount)
        {
            throw new BaselinePlanValidationException(
                $"Expected {ExpectedCommandCount} captured/replayable commands; actual " +
                $"{captureSession.CaptureRun.Commands.Count}/" +
                $"{captureSession.ReplayableCommands.Count}.");
        }

        var parameterCount = captureSession.CaptureRun.Commands
            .Sum(command => command.Parameters.Count);

        if (parameterCount != ExpectedParameterCount)
        {
            throw new BaselinePlanValidationException(
                $"Expected {ExpectedParameterCount} typed parameters, captured {parameterCount}.");
        }

        var commandKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < ExpectedCommandCount; index++)
        {
            var captured = captureSession.CaptureRun.Commands[index];
            var replayable = captureSession.ReplayableCommands[index];

            if (!ReferenceEquals(captured, replayable.CapturedCommand) &&
                captured != replayable.CapturedCommand)
            {
                throw new BaselinePlanValidationException(
                    $"Captured and replayable command {index + 1} do not match.");
            }

            if (!string.Equals(captured.CommandType, CommandType.Text.ToString(), StringComparison.Ordinal) ||
                !captured.CommandText.TrimStart().StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
            {
                throw new BaselinePlanValidationException(
                    $"{captured.ShapeId}/{captured.CommandRole}: only captured SELECT text commands " +
                    "may be replayed.");
            }

            if (captured.Parameters.Count != replayable.Parameters.Count)
            {
                throw new BaselinePlanValidationException(
                    $"{captured.ShapeId}/{captured.CommandRole}: captured/replay parameter counts differ.");
            }

            var commandKey = CreateCommandKey(captured);

            if (!commandKeys.Add(commandKey))
            {
                throw new BaselinePlanValidationException(
                    $"Duplicate command key '{commandKey}'.");
            }
        }
    }

    private static async Task<ExplainExecution> ExecuteExplainAsync(
        NpgsqlConnection connection,
        ReplayableCommand replayableCommand,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandType = CommandType.Text;
        command.CommandText = ExplainPrefix + replayableCommand.CapturedCommand.CommandText;

        foreach (var replayableParameter in replayableCommand.Parameters.OrderBy(parameter => parameter.Ordinal))
        {
            command.Parameters.Add(CreateParameter(replayableParameter));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new BaselinePlanValidationException(
                $"{replayableCommand.CapturedCommand.ShapeId}/" +
                $"{replayableCommand.CapturedCommand.CommandRole}: EXPLAIN returned no plan row.");
        }

        var rawValue = reader.GetValue(0);
        var rawJson = rawValue switch
        {
            string text => text,
            JsonDocument document => document.RootElement.GetRawText(),
            JsonElement element => element.GetRawText(),
            _ => Convert.ToString(rawValue, CultureInfo.InvariantCulture)
                 ?? throw new BaselinePlanValidationException("EXPLAIN returned a null JSON plan.")
        };

        if (await reader.ReadAsync(cancellationToken))
        {
            throw new BaselinePlanValidationException(
                $"{replayableCommand.CapturedCommand.ShapeId}/" +
                $"{replayableCommand.CapturedCommand.CommandRole}: EXPLAIN returned multiple plan rows.");
        }

        using var planDocument = JsonDocument.Parse(rawJson);
        var root = planDocument.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1 ||
            !root[0].TryGetProperty("Plan", out var plan))
        {
            throw new BaselinePlanValidationException(
                $"{replayableCommand.CapturedCommand.ShapeId}/" +
                $"{replayableCommand.CapturedCommand.CommandRole}: invalid EXPLAIN JSON shape.");
        }

        var actualRows = ReadRequiredInt64(plan, "Actual Rows");
        var actualLoops = ReadRequiredInt64(plan, "Actual Loops");
        var structuralJson = CreateStructuralPlanJson(root);

        return new ExplainExecution(
            rawJson,
            ComputeSha256(structuralJson),
            actualRows,
            actualLoops);
    }

    private static NpgsqlParameter CreateParameter(ReplayableParameter source)
    {
        var parameter = new NpgsqlParameter
        {
            ParameterName = source.Name,
            Direction = source.Direction,
            IsNullable = source.IsNullable,
            Size = source.Size,
            Precision = source.Precision,
            Scale = source.Scale,
            Value = CloneValue(source.Value) ?? DBNull.Value
        };

        if (source.NpgsqlDbType.HasValue)
        {
            parameter.NpgsqlDbType = source.NpgsqlDbType.Value;
        }
        else if (!string.IsNullOrWhiteSpace(source.DataTypeName))
        {
            parameter.DataTypeName = source.DataTypeName;
        }
        else
        {
            parameter.DbType = source.DbType;
        }

        return parameter;
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null or DBNull => value,
            byte[] bytes => bytes.ToArray(),
            char[] characters => characters.ToArray(),
            Array array => array.Clone(),
            _ => value
        };
    }

    private static string CreateCommandKey(CapturedCommand command)
    {
        return $"{command.ShapeId}-{command.ShapeSequence:D2}-{command.CommandRole}";
    }

    private static string ComputeParameterSha256(IReadOnlyList<CapturedParameter> parameters)
    {
        return ComputeSha256(JsonSerializer.Serialize(
            parameters,
            JsonArtifactOutput.SerializerOptions));
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }

    private static long ReadRequiredInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var result))
        {
            throw new BaselinePlanValidationException(
                $"EXPLAIN plan is missing numeric '{propertyName}'.");
        }

        return result;
    }

    private static string CreateStructuralPlanJson(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteStructuralElement(writer, root);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStructuralElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject()
                             .Where(property => !IsVolatilePlanProperty(property.Name))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteStructuralElement(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteStructuralElement(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool IsVolatilePlanProperty(string propertyName)
    {
        return propertyName.StartsWith("Actual ", StringComparison.Ordinal) ||
               propertyName.EndsWith(" Blocks", StringComparison.Ordinal) ||
               propertyName.StartsWith("Rows Removed by ", StringComparison.Ordinal) ||
               propertyName is
                   "Execution Time" or
                   "Planning Time" or
                   "I/O Read Time" or
                   "I/O Write Time" or
                   "Peak Memory Usage" or
                   "Sort Space Used" or
                   "Timing" or
                   "Workers" or
                   "Workers Launched";
    }

    private static void ScanOutputForCredentials(
        string runDirectory,
        NpgsqlConnectionStringBuilder connectionStringBuilder)
    {
        var forbiddenValues = new List<string>
        {
            connectionStringBuilder.ConnectionString,
            $"Host={connectionStringBuilder.Host}",
            $"Server={connectionStringBuilder.Host}",
            $"Username={connectionStringBuilder.Username}",
            $"User ID={connectionStringBuilder.Username}",
            "\"Host\":",
            "\"Username\":",
            "\"Password\":"
        };

        if (!string.IsNullOrEmpty(connectionStringBuilder.Password))
        {
            forbiddenValues.Add(connectionStringBuilder.Password);
            forbiddenValues.Add($"Password={connectionStringBuilder.Password}");
        }

        foreach (var file in Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories))
        {
            var contents = File.ReadAllText(file);

            foreach (var forbidden in forbiddenValues.Where(value => !string.IsNullOrEmpty(value)))
            {
                if (contents.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BaselinePlanValidationException(
                        $"Credential scan rejected output file '{file}'.");
                }
            }
        }
    }

    private sealed record ExplainExecution(
        string RawJson,
        string StructuralPlanSha256,
        long ActualRows,
        long ActualLoops);
}

internal sealed class BaselinePlanValidationException(string message)
    : InvalidOperationException(message);
