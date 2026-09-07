namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Root configuration document, persisted as JSON under ProgramData. This same shape
/// is exported (minus Windows-only secret protection) as the seed config for the
/// Part 2 Docker bridge -- see Docker export in the config tool.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>Bumped whenever the schema changes so ConfigStore can migrate old files.</summary>
    public int SchemaVersion { get; set; } = 1;

    public EntraAppSettings EntraApp { get; set; } = new();
    public List<AllowedSender> AllowedSenders { get; set; } = new();
    public List<IpAllowRule> IpAllowRules { get; set; } = new();
    public SmtpListenerSettings SmtpListener { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public SecretExpiryNotificationSettings SecretExpiryNotifications { get; set; } = new();
}
