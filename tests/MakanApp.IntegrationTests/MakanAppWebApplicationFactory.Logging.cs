using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MakanApp.IntegrationTests;

public sealed class SensitiveLogTestControl
{
    private readonly ConcurrentQueue<string> _messages = new();

    public void Record(string message) => _messages.Enqueue(message);

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyCollection<string> Snapshot() => _messages.ToArray();
}

public sealed class RecordingTestLoggerProvider(SensitiveLogTestControl control) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RecordingTestLogger(control);

    public void Dispose()
    {
    }

    private sealed class RecordingTestLogger(SensitiveLogTestControl control) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
                control.Record(exception is null
                    ? message
                    : $"{message} [{exception.GetType().Name}: {exception.Message}]");
            }
        }
    }
}
