using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace OnIT.Smtp.Core.Logging;

/// <summary>
/// A Serilog sink that fans log events out to whatever is currently listening in-process
/// (the named-pipe IPC host broadcasts them on to any connected config-tool clients), and
/// keeps a small ring buffer so a client that connects mid-session immediately sees recent
/// history instead of an empty view.
/// </summary>
public sealed class LiveLogSink : ILogEventSink
{
    private const int RingBufferCapacity = 500;

    private readonly ConcurrentQueue<LogEntry> _ringBuffer = new();
    private readonly List<Action<LogEntry>> _subscribers = new();
    private readonly object _subscribersLock = new();

    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString()
        };

        _ringBuffer.Enqueue(entry);
        while (_ringBuffer.Count > RingBufferCapacity && _ringBuffer.TryDequeue(out _))
        {
        }

        Action<LogEntry>[] subscribersSnapshot;
        lock (_subscribersLock)
        {
            subscribersSnapshot = _subscribers.ToArray();
        }

        foreach (var subscriber in subscribersSnapshot)
        {
            try
            {
                subscriber(entry);
            }
            catch
            {
                // A misbehaving subscriber must never take the logger down.
            }
        }
    }

    /// <summary>Returns a snapshot of recently buffered entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> GetRecentHistory() => _ringBuffer.ToArray();

    /// <summary>Subscribes to future log entries. Dispose the result to unsubscribe.</summary>
    public IDisposable Subscribe(Action<LogEntry> onEntry)
    {
        lock (_subscribersLock)
        {
            _subscribers.Add(onEntry);
        }
        return new Unsubscriber(this, onEntry);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly LiveLogSink _owner;
        private readonly Action<LogEntry> _callback;

        public Unsubscriber(LiveLogSink owner, Action<LogEntry> callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            lock (_owner._subscribersLock)
            {
                _owner._subscribers.Remove(_callback);
            }
        }
    }
}
