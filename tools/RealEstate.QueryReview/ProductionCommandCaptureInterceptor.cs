using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace RealEstate.QueryReview;

internal sealed class ProductionCommandCaptureInterceptor : DbCommandInterceptor
{
    private readonly AsyncLocal<CaptureScope?> _currentScope = new();
    private readonly List<CapturedCommand> _commands = new();
    private readonly object _sync = new();

    public ProductionCommandCaptureInterceptor(string logicalRunId)
    {
        LogicalRunId = logicalRunId;
    }

    public string LogicalRunId { get; }

    public IReadOnlyList<CapturedCommand> Commands
    {
        get
        {
            lock (_sync)
            {
                return _commands.ToArray();
            }
        }
    }

    public IDisposable BeginShape(string shapeId)
    {
        if (_currentScope.Value is not null)
        {
            throw new InvalidOperationException(
                $"A command-capture scope for '{_currentScope.Value.ShapeId}' is already active.");
        }

        var scope = new CaptureScope(shapeId);
        _currentScope.Value = scope;
        return new CaptureScopeLease(this, scope);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Capture(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    private void Capture(DbCommand command)
    {
        var scope = _currentScope.Value
            ?? throw new InvalidOperationException(
                "A production database command executed outside a logical shape scope.");

        var sequence = scope.NextSequence();
        var role = ClassifyRole(scope.ShapeId, sequence, command.CommandText);
        var parameters = command.Parameters
            .Cast<DbParameter>()
            .Select(CaptureParameter)
            .ToArray();

        var captured = new CapturedCommand(
            LogicalRunId,
            scope.ShapeId,
            sequence,
            role,
            command.CommandType.ToString(),
            command.CommandText,
            parameters);

        lock (_sync)
        {
            _commands.Add(captured);
        }
    }

    private static CapturedParameter CaptureParameter(DbParameter parameter)
    {
        var rawValue = parameter.Value;
        var isNull = rawValue is null or DBNull;
        var clrType = isNull
            ? parameter.Value?.GetType().FullName ?? "null"
            : rawValue!.GetType().FullName ?? rawValue.GetType().Name;

        string? npgsqlDbType = null;
        string? dataTypeName = null;

        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlDbType = npgsqlParameter.NpgsqlDbType.ToString();
            dataTypeName = npgsqlParameter.DataTypeName;
        }

        return new CapturedParameter(
            parameter.ParameterName,
            clrType,
            parameter.DbType.ToString(),
            npgsqlDbType,
            dataTypeName,
            parameter.IsNullable,
            isNull,
            IsSensitiveParameter(parameter.ParameterName)
                ? "<redacted>"
                : FormatValue(rawValue));
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null or DBNull => "null",
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset =>
                dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D"),
            byte[] bytes => Convert.ToBase64String(bytes),
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool IsSensitiveParameter(string parameterName)
    {
        return parameterName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               parameterName.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
               parameterName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               parameterName.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static string ClassifyRole(
        string shapeId,
        int shapeSequence,
        string commandText)
    {
        if (shapeId == QueryShapeDefinitions.AgencyShapeId &&
            commandText.Contains("FROM \"Agencies\"", StringComparison.Ordinal))
        {
            return CommandRoles.AgencyExistence;
        }

        if (shapeId == QueryShapeDefinitions.ComparableShapeId)
        {
            if (shapeSequence == 1)
            {
                return CommandRoles.ComparableSource;
            }

            if (commandText.Contains(
                    "INNER JOIN \"ListingTranslations\"",
                    StringComparison.Ordinal))
            {
                return CommandRoles.ComparableTranslationSplit;
            }

            if (commandText.Contains(
                    "INNER JOIN \"ListingImages\"",
                    StringComparison.Ordinal))
            {
                return CommandRoles.ComparableImageSplit;
            }

            return CommandRoles.ComparableRankedRoot;
        }

        if (commandText.Contains("SELECT count(*)::int", StringComparison.Ordinal))
        {
            return CommandRoles.FilteredCount;
        }

        if (commandText.Contains(
                "INNER JOIN \"ListingTranslations\"",
                StringComparison.Ordinal))
        {
            return CommandRoles.TranslationSplit;
        }

        if (commandText.Contains(
                "INNER JOIN \"ListingImages\"",
                StringComparison.Ordinal))
        {
            return CommandRoles.ImageSplit;
        }

        return CommandRoles.PageRoot;
    }

    private void EndScope(CaptureScope scope)
    {
        if (!ReferenceEquals(_currentScope.Value, scope))
        {
            throw new InvalidOperationException("The command-capture scope was disposed out of order.");
        }

        _currentScope.Value = null;
    }

    private sealed class CaptureScope(string shapeId)
    {
        private int _sequence;

        public string ShapeId { get; } = shapeId;

        public int NextSequence() => Interlocked.Increment(ref _sequence);
    }

    private sealed class CaptureScopeLease(
        ProductionCommandCaptureInterceptor owner,
        CaptureScope scope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            owner.EndScope(scope);
            _disposed = true;
        }
    }
}

internal static class CommandRoles
{
    public const string AgencyExistence = "agency-existence";
    public const string FilteredCount = "filtered-count";
    public const string PageRoot = "page-root";
    public const string TranslationSplit = "translation-split";
    public const string ImageSplit = "image-split";
    public const string ComparableSource = "comparable-source";
    public const string ComparableRankedRoot = "comparable-ranked-root";
    public const string ComparableTranslationSplit = "comparable-translation-split";
    public const string ComparableImageSplit = "comparable-image-split";
}
