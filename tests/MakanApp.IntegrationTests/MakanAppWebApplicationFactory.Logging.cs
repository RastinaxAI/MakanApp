using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MakanApp.IntegrationTests;

public sealed class SensitiveLogTestControl
{
    private readonly ConcurrentQueue<string> _messages = new();
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public void Record(string message) => _messages.Enqueue(message);

    public void Record(CapturedLogEntry entry, string message)
    {
        _entries.Enqueue(entry);
        _messages.Enqueue(message);
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
        while (_entries.TryDequeue(out _)) { }
    }

    public IReadOnlyCollection<string> Snapshot() => _messages.ToArray();
    public IReadOnlyCollection<CapturedLogEntry> Entries() => _entries.ToArray();
}

public sealed record CapturedLogEntry(
    string Category, LogLevel Level, EventId EventId,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyCollection<object?> Scopes, string? ExceptionText);

public sealed class RecordingTestLoggerProvider(SensitiveLogTestControl control) : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
    public ILogger CreateLogger(string categoryName) => new RecordingTestLogger(categoryName, control, () => _scopeProvider);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class RecordingTestLogger(
        string category, SensitiveLogTestControl control, Func<IExternalScopeProvider> scopes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => scopes().Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, exception);
                var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.GroupBy(item => item.Key).ToDictionary(group => group.Key, group => group.Last().Value)
                    : new Dictionary<string, object?>();
                var scopeValues = new List<object?>();
                scopes().ForEachScope((scope, captured) => captured.Add(scope), scopeValues);
                // تمام inner exception و stack نیز ضبط می‌شود تا تست نشت را پنهان نکند.
                var exceptionText = exception?.ToString();
                control.Record(new CapturedLogEntry(category, logLevel, eventId, properties, scopeValues, exceptionText),
                    exceptionText is null ? message : $"{message} [{exceptionText}]");
            }
        }
    }
}
