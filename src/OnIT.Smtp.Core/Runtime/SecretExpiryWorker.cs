using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Entra;
using OnIT.Smtp.Core.Mail;

namespace OnIT.Smtp.Core.Runtime;

/// <summary>
/// Periodically checks the configured Entra app's client secret expiry and emails the
/// configured recipients as each threshold (30/15/7/3/0 days) is crossed. Renewal itself
/// stays manual -- see the config tool's "Renew secret now" button -- this worker only
/// notifies. Runs independently of the SMTP relay worker so a problem in one never blocks
/// the other. Shared by the Windows Service and the Part 2 Docker/Linux bridge as-is.
/// </summary>
public sealed class SecretExpiryWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly ConfigStore _configStore;
    private readonly GraphMailService _mailService;
    private readonly ILogger<SecretExpiryWorker> _logger;

    public SecretExpiryWorker(ConfigStore configStore, GraphMailService mailService, ILogger<SecretExpiryWorker> logger)
    {
        _configStore = configStore;
        _mailService = mailService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Secret expiry check failed.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var config = _configStore.Load();
        var entraApp = config.EntraApp;
        var notify = config.SecretExpiryNotifications;

        if (!entraApp.IsConfigured || entraApp.ClientSecretExpiresOn is null) return;
        if (!notify.Enabled || notify.Recipients.Count == 0) return;

        var crossed = SecretExpiryPolicy.GetNewlyCrossedThresholds(
            entraApp.ClientSecretExpiresOn, DateTimeOffset.UtcNow, notify.NotifiedThresholdDays);

        if (crossed.Count == 0) return;

        var fromAddress = notify.FromAddress;
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            fromAddress = config.AllowedSenders.FirstOrDefault(s => s.Enabled)?.UserPrincipalName;
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            _logger.LogWarning("Cannot send secret-expiry notification: no From address is configured and no allowed sender is available.");
            return;
        }

        var anySent = false;

        foreach (var thresholdDays in crossed)
        {
            try
            {
                await _mailService.SendAsync(entraApp, BuildMessage(entraApp, fromAddress, notify.Recipients, thresholdDays), ct);
                notify.NotifiedThresholdDays.Add(thresholdDays);
                anySent = true;
                _logger.LogInformation("Sent client secret expiry notification for the {ThresholdDays}-day threshold.", thresholdDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send client secret expiry notification for the {ThresholdDays}-day threshold; will retry next check.", thresholdDays);
            }
        }

        if (anySent) _configStore.Save(config);
    }

    private static OutboundMessage BuildMessage(
        EntraAppSettings entraApp, string fromAddress, IReadOnlyList<string> recipients, int thresholdDays)
    {
        var expiresOn = entraApp.ClientSecretExpiresOn!.Value;

        var subject = thresholdDays == 0
            ? "OnIT-SMTP: client secret has EXPIRED"
            : $"OnIT-SMTP: client secret expires in {thresholdDays} day(s)";

        var body = thresholdDays == 0
            ? $"The Microsoft Graph client secret for the OnIT-SMTP app '{entraApp.DisplayName}' (tenant {entraApp.TenantId}) "
              + $"expired on {expiresOn:yyyy-MM-dd}. Mail relaying will fail until a new secret is issued. "
              + "Open the config tool's Entra App tab and click \"Renew secret now\"."
            : $"The Microsoft Graph client secret for the OnIT-SMTP app '{entraApp.DisplayName}' (tenant {entraApp.TenantId}) "
              + $"expires on {expiresOn:yyyy-MM-dd} ({thresholdDays} day(s) from now). "
              + "Open the config tool's Entra App tab and click \"Renew secret now\" before it expires.";

        return new OutboundMessage
        {
            From = fromAddress,
            To = recipients,
            Subject = subject,
            Body = body,
            IsHtml = false
        };
    }
}
