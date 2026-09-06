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
    /// Set when the signed-in account lacked rights to grant admin consent directly.
    /// The operator (or a Global/Application Administrator) must open this URL and approve.
    /// </summary>
    public string? PendingAdminConsentUrl { get; init; }
}
