using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Networking;

namespace OnIT.Smtp.Core.Smtp;

public sealed class SmtpServerStats
{
    public DateTimeOffset StartedAt { get; init; }
    public long MessagesRelayed;
    public long MessagesRejected;
    public long ConnectionsRejectedByIpAllowList;
}

/// <summary>
/// Listens on the configured TCP port and dispatches each connection to a new
/// <see cref="SmtpSession"/>, after checking the client's address against the IP allow list.
/// </summary>
public sealed class SmtpServer : IAsyncDisposable
{
    private readonly SmtpListenerSettings _settings;
    private volatile IpAllowList _ipAllowList;
    private readonly Func<SmtpTransaction, CancellationToken, Task<SmtpDeliveryResult>> _onMessage;
    private readonly ILogger<SmtpServer> _logger;

    private TcpListener? _listener;
    private CancellationTokenSource? _acceptLoopCts;
    private Task? _acceptLoopTask;

    public SmtpServerStats Stats { get; private set; } = new() { StartedAt = DateTimeOffset.UtcNow };

    public bool IsListening => _listener is not null;

    /// <summary>
    /// Swaps in a freshly built allow list -- e.g. after a config reload -- without needing to
    /// restart the listener. Takes effect for the next connection accepted; in-flight sessions
    /// were already past the allow-list check.
    /// </summary>
    public void UpdateIpAllowList(IpAllowList ipAllowList) => _ipAllowList = ipAllowList;

    public SmtpServer(
        SmtpListenerSettings settings,
        IpAllowList ipAllowList,
        Func<SmtpTransaction, CancellationToken, Task<SmtpDeliveryResult>> onMessage,
        ILogger<SmtpServer> logger)
    {
        _settings = settings;
        _ipAllowList = ipAllowList;
        _onMessage = onMessage;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken serviceLifetimeToken)
    {
        var address = IPAddress.Parse(_settings.BindAddress);
        _listener = new TcpListener(address, _settings.Port);
        _listener.Start();
        Stats = new SmtpServerStats { StartedAt = DateTimeOffset.UtcNow };

        _acceptLoopCts = CancellationTokenSource.CreateLinkedTokenSource(serviceLifetimeToken);
        _acceptLoopTask = AcceptLoopAsync(_acceptLoopCts.Token);

        _logger.LogInformation("SMTP listener started on {Address}:{Port}.", _settings.BindAddress, _settings.Port);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            var clientAddress = remoteEndPoint?.Address ?? IPAddress.None;

            if (!_ipAllowList.IsAllowed(clientAddress, out var matchedRule))
            {
                Interlocked.Increment(ref Stats.ConnectionsRejectedByIpAllowList);
                _logger.LogWarning("Rejected connection from {Client}: not on the IP allow list.", clientAddress);
                return; // Closing without a banner is deliberate -- do not reveal service details to unlisted hosts.
            }

            _logger.LogInformation("Accepted connection from {Client} (matched rule: {Rule}).", clientAddress, matchedRule);

            client.ReceiveTimeout = 0;
            client.SendTimeout = 0;

            try
            {
                await using var stream = client.GetStream();
                var session = new SmtpSession(stream, clientAddress, _settings, WrapOnMessage, _logger);
                await session.RunAsync(ct);
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Connection from {Client} ended abnormally.", clientAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error handling connection from {Client}.", clientAddress);
            }
        }
    }

    private async Task<SmtpDeliveryResult> WrapOnMessage(SmtpTransaction transaction, CancellationToken ct)
    {
        var result = await _onMessage(transaction, ct);
        if (result.Accepted) Interlocked.Increment(ref Stats.MessagesRelayed);
        else Interlocked.Increment(ref Stats.MessagesRejected);
        return result;
    }

    public async Task StopAsync()
    {
        if (_acceptLoopCts is null) return;

        await _acceptLoopCts.CancelAsync();
        _listener?.Stop();

        if (_acceptLoopTask is not null)
        {
            try { await _acceptLoopTask; } catch { /* already logged inside the loop */ }
        }

        _listener = null;
        _logger.LogInformation("SMTP listener stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _acceptLoopCts?.Dispose();
    }
}
