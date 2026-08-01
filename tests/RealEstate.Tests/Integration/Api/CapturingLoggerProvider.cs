using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RealEstate.Tests.Integration.Api;

internal sealed class CapturingLoggerProvider
    : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();
    private IExternalScopeProvider _scopeProvider =
        new LoggerExternalScopeProvider();

    public IReadOnlyList<CapturedLogEntry> Entries =>
        _entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(this, categoryName);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
    }

    private void Capture<TState>(
        string category,
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = new Dictionary<string, object?>(
            StringComparer.Ordinal);

        AddProperties(properties, state);

        var scopeProperties = new Dictionary<string, object?>(
            StringComparer.Ordinal);

        _scopeProvider.ForEachScope(
            static (scope, target) => AddProperties(target, scope),
            scopeProperties);

        _entries.Enqueue(
            new CapturedLogEntry(
                category,
                level,
                eventId,
                formatter(state, exception),
                exception,
                properties,
                scopeProperties));
    }

    private static void AddProperties(
        IDictionary<string, object?> target,
        object? state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return;
        }

        foreach ((string key, object? value) in values)
        {
            target[key] = value;
        }
    }

    private sealed class CapturingLogger(
        CapturingLoggerProvider provider,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return provider._scopeProvider.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            provider.Capture(
                category,
                logLevel,
                eventId,
                state,
                exception,
                formatter);
        }
    }
}

internal sealed record CapturedLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyDictionary<string, object?> ScopeProperties);
