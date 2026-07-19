using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealEstate.QueryReview;

internal sealed record CapturedParameter(
    string Name,
    string ClrType,
    string DbType,
    string? NpgsqlDbType,
    string? DataTypeName,
    bool IsNullable,
    bool IsNull,
    string Value);

internal sealed record CapturedCommand(
    string LogicalRunId,
    string ShapeId,
    int ShapeSequence,
    string CommandRole,
    string CommandType,
    string CommandText,
    IReadOnlyList<CapturedParameter> Parameters);

internal sealed record QueryShapeResult(
    string ShapeId,
    int? ExpectedTotalCount,
    int? ActualTotalCount,
    int ExpectedItemCount,
    int ActualItemCount,
    IReadOnlyList<Guid> ResultIds);

internal sealed record SqlCaptureRun(
    string LogicalRunId,
    string ProfileVersion,
    int CSharpSeed,
    double PostgreSqlSeed,
    string Database,
    string PostgreSqlVersion,
    IReadOnlyList<QueryShapeResult> ShapeResults,
    IReadOnlyList<CapturedCommand> Commands);

internal static class SqlCaptureOutput
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<string> WriteAsync(
        SqlCaptureRun run,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var runDirectory = Path.GetFullPath(
            Path.Combine(outputDirectory, run.LogicalRunId));

        Directory.CreateDirectory(runDirectory);

        var outputPath = Path.Combine(runDirectory, "captured-commands.json");
        var json = JsonSerializer.Serialize(run, SerializerOptions);

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        return outputPath;
    }
}
