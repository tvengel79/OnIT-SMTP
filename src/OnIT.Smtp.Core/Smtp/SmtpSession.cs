using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.Core.Smtp;

/// <summary>
/// Handles a single SMTP client connection: EHLO/HELO, MAIL FROM, RCPT TO (repeatable),
/// DATA, RSET, NOOP, QUIT. Deliberately minimal -- no AUTH/STARTTLS, matching the "plain
/// SMTP from trusted LAN clients, gated by IP allow list" design.
/// </summary>
public sealed class SmtpSession
{
    private static readonly Regex AddressPattern = new(@"<(?<addr>[^<>]*)>", RegexOptions.Compiled);

    private readonly SmtpLineReader _reader;
    private readonly Stream _stream;
    private readonly IPAddress _clientAddress;
    private readonly SmtpListenerSettings _settings;
    private readonly Func<SmtpTransaction, CancellationToken, Task<SmtpDeliveryResult>> _onMessage;
    private readonly ILogger _logger;

    private string? _mailFrom;
    private readonly List<string> _rcptTo = new();

    public SmtpSession(
        Stream stream,
        IPAddress clientAddress,
        SmtpListenerSettings settings,
        Func<SmtpTransaction, CancellationToken, Task<SmtpDeliveryResult>> onMessage,
        ILogger logger)
    {
        _stream = stream;
        _reader = new SmtpLineReader(stream);
        _clientAddress = clientAddress;
        _settings = settings;
        _onMessage = onMessage;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await SendAsync($"220 {_settings.ServerHostName} OnIT-SMTP ready", ct);

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.SessionTimeoutSeconds));
                line = await _reader.ReadCommandLineAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("Session from {Client} timed out waiting for a command.", _clientAddress);
                await TrySendAsync("421 Session timed out", ct);
                return;
            }

            if (line is null)
            {
                _logger.LogDebug("Client {Client} closed the connection.", _clientAddress);
                return;
            }

            _logger.LogDebug("{Client} > {Line}", _clientAddress, line);

            if (!await HandleCommandAsync(line, ct)) return;
        }
    }

    private async Task<bool> HandleCommandAsync(string line, CancellationToken ct)
    {
        var spaceIdx = line.IndexOf(' ');
        var verb = (spaceIdx == -1 ? line : line[..spaceIdx]).Trim().ToUpperInvariant();
        var rest = spaceIdx == -1 ? string.Empty : line[(spaceIdx + 1)..].Trim();

        switch (verb)
        {
            case "EHLO":
            case "HELO":
                await SendAsync($"250 {_settings.ServerHostName} greets {(rest.Length == 0 ? "you" : rest)}", ct);
                return true;

            case "MAIL":
                return await HandleMailFromAsync(rest, ct);

            case "RCPT":
                return await HandleRcptToAsync(rest, ct);

            case "DATA":
                return await HandleDataAsync(ct);

            case "RSET":
                _mailFrom = null;
                _rcptTo.Clear();
                await SendAsync("250 OK", ct);
                return true;

            case "NOOP":
                await SendAsync("250 OK", ct);
                return true;

            case "QUIT":
                await SendAsync($"221 {_settings.ServerHostName} closing connection", ct);
                return false;

            default:
                await SendAsync("500 Command not recognized", ct);
                return true;
        }
    }

    private async Task<bool> HandleMailFromAsync(string rest, CancellationToken ct)
    {
        var match = AddressPattern.Match(rest);
        if (!match.Success)
        {
            await SendAsync("501 Syntax error in MAIL FROM, expected MAIL FROM:<address>", ct);
            return true;
        }

        _mailFrom = match.Groups["addr"].Value;
        _rcptTo.Clear();
        await SendAsync("250 OK", ct);
        return true;
    }

    private async Task<bool> HandleRcptToAsync(string rest, CancellationToken ct)
    {
        if (_mailFrom is null)
        {
            await SendAsync("503 Send MAIL FROM first", ct);
            return true;
        }

        var match = AddressPattern.Match(rest);
        if (!match.Success)
        {
            await SendAsync("501 Syntax error in RCPT TO, expected RCPT TO:<address>", ct);
            return true;
        }

        _rcptTo.Add(match.Groups["addr"].Value);
        await SendAsync("250 OK", ct);
        return true;
    }

    private async Task<bool> HandleDataAsync(CancellationToken ct)
    {
        if (_mailFrom is null || _rcptTo.Count == 0)
        {
            await SendAsync("503 Send MAIL FROM and at least one RCPT TO first", ct);
            return true;
        }

        await SendAsync("354 Start mail input; end with <CRLF>.<CRLF>", ct);

        byte[] data;
        try
        {
            data = await _reader.ReadDataAsync(_settings.MaxMessageSizeBytes, ct);
        }
        catch (SmtpMessageTooLargeException)
        {
            await SendAsync("552 Message exceeds the maximum allowed size", ct);
            _mailFrom = null;
            _rcptTo.Clear();
            return true;
        }

        var transaction = new SmtpTransaction
        {
            ClientAddress = _clientAddress.ToString(),
            MailFrom = _mailFrom,
            RcptTo = _rcptTo.ToArray(),
            Data = data
        };

        var result = await _onMessage(transaction, ct);

        if (result.Accepted)
        {
            await SendAsync("250 OK: message queued for delivery", ct);
        }
        else
        {
            await SendAsync($"550 {result.RejectReason ?? "Message rejected"}", ct);
        }

        _mailFrom = null;
        _rcptTo.Clear();
        return true;
    }

    private async Task SendAsync(string response, CancellationToken ct)
    {
        _logger.LogDebug("{Client} < {Response}", _clientAddress, response);
        var bytes = Encoding.ASCII.GetBytes(response + "\r\n");
        await _stream.WriteAsync(bytes, ct);
        await _stream.FlushAsync(ct);
    }

    private async Task TrySendAsync(string response, CancellationToken ct)
    {
        try
        {
            await SendAsync(response, ct);
        }
        catch
        {
            // Best-effort -- the connection may already be gone.
        }
    }
}
