using System.Text;
using System.Text.Json;

namespace OnIT.Smtp.Core.Ipc;

/// <summary>
/// Thin newline-delimited-JSON framing over any Stream (a NamedPipeServerStream on the
/// service side, a NamedPipeClientStream on the config-tool side). One IpcEnvelope per line.
/// </summary>
public sealed class NdjsonPipeConnection : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Stream _stream;
    private readonly StreamReader _reader;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public NdjsonPipeConnection(Stream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
    }

    public async Task SendAsync(IpcEnvelope envelope, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(envelope, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(line + "\n");

        await _writeLock.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task SendAsync<TPayload>(string type, TPayload payload, string? correlationId = null, CancellationToken ct = default)
    {
        var envelope = new IpcEnvelope
        {
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            CorrelationId = correlationId
        };
        return SendAsync(envelope, ct);
    }

    /// <summary>Returns null when the remote end closed the connection.</summary>
    public async Task<IpcEnvelope?> ReceiveAsync(CancellationToken ct = default)
    {
        var line = await _reader.ReadLineAsync(ct);
        if (line is null) return null;
        return JsonSerializer.Deserialize<IpcEnvelope>(line, JsonOptions);
    }

    public static TPayload? DeserializePayload<TPayload>(IpcEnvelope envelope) =>
        envelope.PayloadJson is null ? default : JsonSerializer.Deserialize<TPayload>(envelope.PayloadJson, JsonOptions);

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _writeLock.Dispose();
        return _stream is IAsyncDisposable asyncDisposable ? asyncDisposable.DisposeAsync() : default;
    }
}
