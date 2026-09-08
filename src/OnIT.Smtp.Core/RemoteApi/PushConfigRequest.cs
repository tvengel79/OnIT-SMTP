using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.Core.RemoteApi;

/// <summary>
/// Body of POST /api/config. Mirrors AppConfiguration except the client secret travels as
/// plaintext rather than pre-encrypted ciphertext: the connection is already authenticated
/// (bearer token) and TLS-pinned to the receiving host's own certificate, and the sender's and
/// receiver's secret.key files are independent -- ciphertext made with one could never be
/// decrypted by the other. The receiving host protects the secret with its own
/// ISecretProtector before handing the result to ConfigStore.Save().
/// </summary>
public sealed class PushConfigRequest
{
    public int SchemaVersion { get; init; } = 1;
    public required PushEntraApp EntraApp { get; init; }
    public List<AllowedSender> AllowedSenders { get; init; } = new();
    public List<IpAllowRule> IpAllowRules { get; init; } = new();
    public required SmtpListenerSettings SmtpListener { get; init; }
    public required LoggingSettings Logging { get; init; }
    public required AdvancedSettings Advanced { get; init; }
    public SecretExpiryNotificationSettings? SecretExpiryNotifications { get; init; }
}

public sealed class PushEntraApp
{
    public required string TenantId { get; init; }
    public required string ApplicationId { get; init; }
    public string DisplayName { get; init; } = "OnIT-SMTP Bridge";
    public GraphAuthMode AuthMode { get; init; } = GraphAuthMode.ClientSecret;

    /// <summary>Plaintext client secret -- see the remarks on <see cref="PushConfigRequest"/>. Null when using a certificate.</summary>
    public string? ClientSecret { get; init; }

    public DateTimeOffset? ClientSecretExpiresOn { get; init; }
    public bool AdminConsentGranted { get; init; }
}
