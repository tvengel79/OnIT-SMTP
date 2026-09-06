namespace OnIT.Smtp.Core.Configuration;

public enum GraphAuthMode
{
    ClientSecret,
    Certificate
}

/// <summary>
/// The Entra ID (Azure AD) app registration used to call Microsoft Graph for
/// sending mail. The client secret is stored encrypted at rest via
/// <see cref="ISecretProtector"/> -- <see cref="ProtectedClientSecret"/> is the
/// ciphertext, never the plain value.
/// </summary>
public sealed class EntraAppSettings
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApplicationId) && !string.IsNullOrWhiteSpace(TenantId);

    public string TenantId { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>Object ID of the app registration (needed for delete/permission calls).</summary>
    public string ApplicationObjectId { get; set; } = string.Empty;

    /// <summary>Object ID of the associated service principal (needed for admin-consent app role assignment).</summary>
    public string ServicePrincipalObjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "OnIT-SMTP Bridge";

    public GraphAuthMode AuthMode { get; set; } = GraphAuthMode.ClientSecret;

    /// <summary>
    /// Portably encrypted (base64) client secret -- see <see cref="ISecretProtector"/>.
    /// Travels safely with config.json to another machine (the paired key file must come
    /// along too). Null when using a certificate.
    /// </summary>
    public string? ProtectedClientSecret { get; set; }

    public DateTimeOffset? ClientSecretExpiresOn { get; set; }

    /// <summary>Certificate thumbprint in the local machine store, when AuthMode == Certificate.</summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>True once the Mail.Send application permission has been granted admin consent.</summary>
    public bool AdminConsentGranted { get; set; }

    /// <summary>
    /// The standard Microsoft admin-consent URL for this app. Always available once the app
    /// exists -- opening it and clicking Accept as a Global/Application Administrator is the
    /// simplest way to (re-)grant consent, regardless of whether it was already attempted
    /// automatically.
    /// </summary>
    public string? AdminConsentUrl { get; set; }
}
