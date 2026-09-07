namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Configures the "client secret is about to expire" email alerts sent by the Windows
/// Service (see SecretExpiryWorker). Manual renewal only for now -- see
/// EntraAppManager.RenewSecretAsync and the config tool's "Renew secret now" button.
/// </summary>
public sealed class SecretExpiryNotificationSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Mailbox addresses to notify. Empty means notifications are effectively off.</summary>
    public List<string> Recipients { get; set; } = new();

    /// <summary>
    /// Mailbox the notification is sent from. Falls back to the first enabled allowed sender
    /// when unset -- notification sends are not themselves subject to the allowed-senders
    /// relay restriction (same as the config tool's test-send).
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>
    /// Thresholds (days-until-expiry) already notified for the CURRENT client secret. Reset
    /// to empty whenever a new secret is created or renewed.
    /// </summary>
    public List<int> NotifiedThresholdDays { get; set; } = new();
}
