using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Mail;
using OnIT.Smtp.Core.Networking;
using OnIT.Smtp.Core.Runtime;
using OnIT.Smtp.Core.Smtp;

namespace OnIT.Smtp.Bridge;

/// <summary>
/// Owns the whole Part 2 runtime: loads configuration, starts the SMTP listener, and applies
/// the allowed-senders policy to each relayed message -- the same job as OnIT.Smtp.Service's
/// RelayWorker, minus the Windows-only named-pipe control channel (there is no local WPF
/// config tool to connect to a container). Config is edited by replacing config.json in the
/// mounted volume; this worker picks it up automatically via the same file-watcher approach.
/// </summary>
public sealed class BridgeRelayWorker : BackgroundService
{
    private readonly ConfigStore _configStore;
    private readonly GraphMailService _mailService;
    private readonly ILogger<BridgeRelayWorker> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly object _configLock = new();
    private AppConfiguration _config = new();
    private SmtpServer? _smtpServer;
    private FileSystemWatcher? _configWatcher;

    public BridgeRelayWorker(
        ConfigStore configStore,
        GraphMailService mailService,
        ILogger<BridgeRelayWorker> logger,
        ILoggerFactory loggerFactory)
    {
        _configStore = configStore;
        _mailService = mailService;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConfigPaths.EnsureDirectoriesExist();
        _config = _configStore.Load();

        if (!_config.EntraApp.IsConfigured)
        {
            _logger.LogWarning(
                "No Entra app is configured yet -- seed {ConfigPath} (exported from the Windows config tool) before relaying mail.",
                ConfigPaths.ConfigFilePath);
        }

        await StartSmtpServerAsync(stoppingToken);
        StartConfigFileWatcher();

        _logger.LogInformation("OnIT-SMTP bridge started.");

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
            return SmtpDeliveryResult.Reject("Delivery via Microsoft Graph failed. Check the bridge log for details.");
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
            // Debounce: a bind-mounted config file can be written more than once in quick
            // succession (e.g. a text editor's save, or the volume sync catching up).
            await Task.Delay(300);
            await ReloadConfigAsync(CancellationToken.None);
        };
    }

    private async Task ReloadConfigAsync(CancellationToken ct)
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration.");
        }
    }
}
