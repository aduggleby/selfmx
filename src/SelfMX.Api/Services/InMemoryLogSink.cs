using System.Collections.Concurrent;

namespace SelfMX.Api.Services;

/// <summary>
/// In-memory circular buffer for capturing recent log entries.
/// Useful for remote diagnostics via admin API.
/// </summary>
public class InMemoryLogSink
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private readonly int _maxEntries;

    public InMemoryLogSink(int maxEntries = 2000)
    {
        _maxEntries = maxEntries;
    }

    public void Add(LogEntry entry)
    {
        _logs.Enqueue(entry);

        // Trim if over capacity
        while (_logs.Count > _maxEntries && _logs.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<LogEntry> GetLogs(int count = 1000)
    {
        return _logs.TakeLast(count).ToList();
    }

    public void Clear()
    {
        _logs.Clear();
    }
}

public record LogEntry(
    DateTime Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception = null
);

/// <summary>
/// Logger provider that writes to the in-memory sink.
/// </summary>
public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly InMemoryLogSink _sink;
    private readonly LogLevel _minLevel;

    public InMemoryLoggerProvider(InMemoryLogSink sink, LogLevel minLevel = LogLevel.Information)
    {
        _sink = sink;
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(_sink, categoryName, _minLevel);
    }

    public void Dispose()
    {
    }
}

public class InMemoryLogger : ILogger
{
    private readonly InMemoryLogSink _sink;
    private readonly string _category;
    private readonly LogLevel _minLevel;

    public InMemoryLogger(InMemoryLogSink sink, string category, LogLevel minLevel)
    {
        _sink = sink;
        _category = category;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var entry = new LogEntry(
            DateTime.UtcNow,
            logLevel.ToString(),
            _category,
            formatter(state, exception),
            exception?.ToString()
        );

        _sink.Add(entry);
    }
}
