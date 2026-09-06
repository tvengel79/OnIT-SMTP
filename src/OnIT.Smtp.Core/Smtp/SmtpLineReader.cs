using System.Text;

namespace OnIT.Smtp.Core.Smtp;

/// <summary>
/// A small buffered CRLF line reader over a network stream, used for both SMTP command
/// lines and the DATA phase (where it also performs RFC 5321 dot-unstuffing and detects
/// the terminating "." line). Deliberately simple/synchronous-buffer-based rather than
/// using System.IO.Pipelines -- this is a LAN relay, not a high-throughput MTA.
/// </summary>
public sealed class SmtpLineReader
{
    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[8192];
    private int _bufferStart;
    private int _bufferLength;

    public SmtpLineReader(Stream stream)
    {
        _stream = stream;
    }

    /// <summary>Reads a single CRLF-terminated ASCII command line. Returns null on clean connection close.</summary>
    public async Task<string?> ReadCommandLineAsync(CancellationToken ct)
    {
        var lineBytes = await ReadRawLineAsync(ct);
        if (lineBytes is null) return null;
        return Encoding.ASCII.GetString(lineBytes);
    }

    /// <summary>
    /// Reads the DATA payload up to and including the terminating "CRLF.CRLF" sequence,
    /// stripping the terminator and undoing dot-stuffing. Throws SmtpMessageTooLargeException
    /// if maxSizeBytes (0 = unlimited) is exceeded.
    /// </summary>
    public async Task<byte[]> ReadDataAsync(int maxSizeBytes, CancellationToken ct)
    {
        using var output = new MemoryStream();

        while (true)
        {
            var lineBytes = await ReadRawLineAsync(ct) ?? throw new IOException("Connection closed during DATA.");

            if (lineBytes.Length == 1 && lineBytes[0] == (byte)'.')
            {
                break;
            }

            var start = 0;
            if (lineBytes.Length > 0 && lineBytes[0] == (byte)'.')
            {
                start = 1; // Undo dot-stuffing.
            }

            var lineLength = lineBytes.Length - start;
            if (maxSizeBytes > 0 && output.Length + lineLength + 2 > maxSizeBytes)
            {
                throw new SmtpMessageTooLargeException(maxSizeBytes);
            }

            output.Write(lineBytes, start, lineLength);
            output.WriteByte((byte)'\r');
            output.WriteByte((byte)'\n');
        }

        return output.ToArray();
    }

    /// <summary>Reads one line's bytes, excluding the trailing CRLF. Returns null on clean EOF.</summary>
    private async Task<byte[]?> ReadRawLineAsync(CancellationToken ct)
    {
        var lineBuffer = new List<byte>(256);

        while (true)
        {
            if (_bufferStart >= _bufferLength)
            {
                _bufferLength = await _stream.ReadAsync(_buffer, ct);
                _bufferStart = 0;
                if (_bufferLength == 0)
                {
                    return lineBuffer.Count == 0 ? null : lineBuffer.ToArray();
                }
            }

            var b = _buffer[_bufferStart++];

            if (b == (byte)'\n')
            {
                // Strip a preceding \r if present (tolerate bare LF from non-conformant senders).
                if (lineBuffer.Count > 0 && lineBuffer[^1] == (byte)'\r')
                {
                    lineBuffer.RemoveAt(lineBuffer.Count - 1);
                }
                return lineBuffer.ToArray();
            }

            lineBuffer.Add(b);
        }
    }
}

public sealed class SmtpMessageTooLargeException : Exception
{
    public int MaxSizeBytes { get; }

    public SmtpMessageTooLargeException(int maxSizeBytes)
        : base($"Message exceeds the configured maximum size of {maxSizeBytes} bytes.")
    {
        MaxSizeBytes = maxSizeBytes;
    }
}
