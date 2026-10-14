using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Njord.Tests.Shared;

// Thread-safe ILogger that keeps every entry (level, rendered message, exception) for assertions.
public sealed class RecordingLogger : ILogger
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public IReadOnlyList<LogEntry> Errors => [.. _entries.Where(e => e.Level == LogLevel.Error)];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
}

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
