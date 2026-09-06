using System.Collections.Concurrent;
using System.IO.Pipes;
using OnIT.Smtp.Core.Ipc;
using OnIT.Smtp.Core.Logging;

namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>
/// Maintains a connection to the Windows Service's control pipe, auto-reconnecting whenever
/// the service isn't running yet or gets restarted. Surfaces live log entries and lets tabs
/// issue request/response calls (status, reload, test-send) keyed by a correlation id.
/// </summary>
public sealed class PipeClientService : IDisposable
{
    public static PipeClientService Instance { get; } = new();

    public event Action<LogEntry>? LogEntryReceived;
    public event Action<IReadOnlyList<LogEntry>>? LogHistoryReceived;
    public event Action<bool>? ConnectionStateChanged;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcEnvelope>> _pending = new();
    private NdjsonPipeConnection? _connection;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsConnected { get; private set; }

    private PipeClientService()
    {
    }

    public void Start()
    {
        if (_loopTask is not null) return;
        _cts = new CancellationTokenSource();
        _loopTask = ConnectionLoopAsync(_cts.Token);
    }

    private async Task ConnectionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(".", PipeNames.ServiceControlPipe, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, ct);

                _connection = new NdjsonPipeConnection(pipe);
                SetConnected(true);

                while (!ct.IsCancellationRequested)
                {
                    var envelope = await _connection.ReceiveAsync(ct);
                    if (envelope is null) break;
                    Dispatch(envelope);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Service not running / pipe not ready yet -- keep retrying below.
            }
            finally
            {
                _connection = null;
                SetConnected(false);
            }

            try
            {
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SetConnected(bool connected)
    {
        if (IsConnected == connected) return;
        IsConnected = connected;
        ConnectionStateChanged?.Invoke(connected);
    }

    private void Dispatch(IpcEnvelope envelope)
    {
        if (envelope.CorrelationId is not null && _pending.TryRemove(envelope.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(envelope);
            return;
        }

        switch (envelope.Type)
        {
            case IpcMessageTypes.LogEntry:
                var entry = NdjsonPipeConnection.DeserializePayload<LogEntry>(envelope);
                if (entry is not null) LogEntryReceived?.Invoke(entry);
                break;

            case IpcMessageTypes.LogHistory:
                var history = NdjsonPipeConnection.DeserializePayload<List<LogEntry>>(envelope);
                if (history is not null) LogHistoryReceived?.Invoke(history);
                break;
        }
    }

    public Task<ServiceStatus?> GetStatusAsync(TimeSpan? timeout = null) =>
        SendRequestAsync<object, ServiceStatus>(IpcMessageTypes.GetStatusRequest, new { }, timeout);

    public Task<OperationResult?> ReloadConfigAsync(TimeSpan? timeout = null) =>
        SendRequestAsync<object, OperationResult>(IpcMessageTypes.ReloadConfigRequest, new { }, timeout);

    public Task<OperationResult?> SendTestEmailAsync(SendTestEmailRequestPayload payload, TimeSpan? timeout = null) =>
        SendRequestAsync<SendTestEmailRequestPayload, OperationResult>(IpcMessageTypes.SendTestEmailRequest, payload, timeout);

    private async Task<TResponse?> SendRequestAsync<TPayload, TResponse>(string type, TPayload payload, TimeSpan? timeout)
    {
        var connection = _connection;
        if (connection is null) return default;

        var correlationId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<IpcEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        try
        {
            await connection.SendAsync(type, payload, correlationId);

            using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
            await using var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            var response = await tcs.Task;
            return NdjsonPipeConnection.DeserializePayload<TResponse>(response);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        catch
        {
            return default;
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
