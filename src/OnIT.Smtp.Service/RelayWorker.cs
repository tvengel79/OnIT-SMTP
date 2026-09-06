using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Ipc;
using OnIT.Smtp.Core.Mail;
using OnIT.Smtp.Core.Networking;
using OnIT.Smtp.Core.Smtp;
using OnIT.Smtp.Service.Ipc;

namespace OnIT.Smtp.Service;

/// <summary>
/// Owns the whole runtime: loads configuration, starts the SMTP listener and the control
/// pipe, applies the allowed-senders policy to each relayed message, and supports a live
/// config reload (triggered by the config tool, or picked up automatically on save).
/// </summary>
public sealed class RelayWorker : BackgroundService
{
    private readonly ConfigStore _configStore;
    private readonly GraphMailService _mailService;
    private readonly ControlPipeHost _controlPipeHost;
    private readonly ILogger<RelayWorker> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly object _configLock = new();
    private AppConfiguration _config = new();
    private SmtpServer? _smtpServer;
    private FileSystemWatcher? _configWatcher;

    public RelayWorker(
        ConfigStore configStore,
        GraphMailService mailService,
        Core.Logging.LiveLogSink liveLogSink,
        ILogger<RelayWorker> logger,
        ILoggerFactory loggerFactory)
    {
        _configStore = configStore;
        _mailService = mailService;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _controlPipeHost = new ControlPipeHost(
            liveLogSink,
            GetStatus,
            (ct) => ReloadConfigAsync(ct),
            SendTestEmailAsync,
            loggerFactory.CreateLogger<ControlPipeHost>());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConfigPaths.EnsureDirectoriesExist();
        _config = _configStore.Load();

        _controlPipeHost.Start(stoppingToken);
        await StartSmtpServerAsync(stoppingToken);
        StartConfigFileWatcher();

        _logger.LogInformation("OnIT-SMTP service started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _configWatcher?.Dispose();
        if (_smtpServer is not null) await _smtpServer.DisposeAsync();
        await _controlPipeHost.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task StartSmtpServerAsync(CancellationToken ct)
    {
        AppConfiguration snapshot;
        lock (_configLock) snapshot = _config;

        var ipAllowList = new IpAllowList(snapshot.IpAllowRules);
        _smtpServer = new SmtpServer(snapshot.SmtpListener, ipAllowList, OnMessageReceivedAsync, _loggerFactory.CreateLogger<SmtpServer>());
        await _smtpServer.StartAsync(ct);
    }

    private async Task<SmtpDeliveryResult> OnMessageReceivedAsync(SmtpTransaction transaction, CancellationToken ct)
    {
        AppConfiguration snapshot;
        lock (_configLock) snapshot = _config;

        if (!SenderPolicy.IsSenderPermitted(snapshot, transaction.MailFrom))
        {
            _logger.LogWarning("Rejected message from {Client}: sender {MailFrom} is not on the allowed-senders list.",
                transaction.ClientAddress, transaction.MailFrom);
            return SmtpDeliveryResult.Reject("Sender is not permitted to relay through this server.");
        }

        try
        {
            var outbound = OutboundMessageBuilder.Build(transaction.MailFrom, transaction.RcptTo, transaction.Data);
            await _mailService.SendAsync(snapshot.EntraApp, outbound, ct);

            _logger.LogInformation("Relayed message from {From} to {To} (client {Client}).",
                transaction.MailFrom, string.Join(", ", transaction.RcptTo), transaction.ClientAddress);
            return SmtpDeliveryResult.Accept();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay message from {From} via Microsoft Graph.", transaction.MailFrom);
            return SmtpDeliveryResult.Reject("Delivery via Microsoft Graph failed. Check the service log for details.");
        }
    }

    private void StartConfigFileWatcher()
    {
        var directory = Path.GetDirectoryName(ConfigPaths.ConfigFilePath)!;
        _configWatcher = new FileSystemWatcher(directory, Path.GetFileName(ConfigPaths.ConfigFilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _configWatcher.Changed += async (_, _) =>
        {
            // Debounce: the config tool can write the file more than once in quick succession.
            await Task.Delay(300);
            await ReloadConfigAsync(CancellationToken.None);
        };
    }

    public async Task<OperationResult> ReloadConfigAsync(CancellationToken ct)
    {
        try
        {
            var newConfig = _configStore.Load();
            var previousListener = _config.SmtpListener;

            lock (_configLock) _config = newConfig;

            var listenerChanged = previousListener.BindAddress != newConfig.SmtpListener.BindAddress
                || previousListener.Port != newConfig.SmtpListener.Port;

            if (listenerChanged && _smtpServer is not null)
            {
                _logger.LogInformation("SMTP listener settings changed; restarting the listener.");
                await _smtpServer.StopAsync();
                await StartSmtpServerAsync(ct);
            }

            _logger.LogInformation("Configuration reloaded.");
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration.");
            return OperationResult.Fail(ex.Message);
        }
    }

    private async Task<OperationResult> SendTestEmailAsync(SendTestEmailRequestPayload payload, CancellationToken ct)
    {
        AppConfiguration snapshot;
        lock (_configLock) snapshot = _config;

        try
        {
            var message = new OutboundMessage
            {
                From = payload.From,
                To = new[] { payload.To },
                Subject = payload.Subject,
                Body = payload.Body,
                IsHtml = false
            };

            await _mailService.SendAsync(snapshot.EntraApp, message, ct);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test email send failed.");
            return OperationResult.Fail(ex.Message);
        }
    }

    private ServiceStatus GetStatus()
    {
        AppConfiguration snapshot;
        lock (_configLock) snapshot = _config;

        var stats = _smtpServer?.Stats;
        return new ServiceStatus
        {
            Listening = _smtpServer?.IsListening ?? false,
            BindAddress = snapshot.SmtpListener.BindAddress,
            Port = snapshot.SmtpListener.Port,
            StartedAt = stats?.StartedAt ?? DateTimeOffset.UtcNow,
            MessagesRelayed = stats?.MessagesRelayed ?? 0,
            MessagesRejected = stats?.MessagesRejected ?? 0,
            ConnectionsRejectedByIpAllowList = stats?.ConnectionsRejectedByIpAllowList ?? 0,
            EntraAppConfigured = snapshot.EntraApp.IsConfigured,
            AdminConsentGranted = snapshot.EntraApp.AdminConsentGranted
        };
    }
}
