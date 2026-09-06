namespace OnIT.Smtp.Core.Entra;

public sealed class EntraAppProvisionResult
{
    public required string TenantId { get; init; }
    public required string ApplicationId { get; init; }
    public required string ApplicationObjectId { get; init; }
    public required string ServicePrincipalObjectId { get; init; }
    public required string DisplayName { get; init; }
    public required string ClientSecret { get; init; }
    public required DateTimeOffset ClientSecretExpiresOn { get; init; }

    /// <summary>True if Mail.Send application permission admin consent was granted automatically.</summary>
    public bool AdminConsentGranted { get; init; }

    /// <summary>
    /// The standard Microsoft admin-consent URL for this app, always populated regardless of
    /// whether automatic consent succeeded: opening it in a browser and signing in as (or
    /// already being signed in as) a Global/Application Administrator who clicks Accept grants
    /// consent for the whole tenant without needing any Graph API rights on that session.
    /// </summary>
    public required string AdminConsentUrl { get; init; }
}
