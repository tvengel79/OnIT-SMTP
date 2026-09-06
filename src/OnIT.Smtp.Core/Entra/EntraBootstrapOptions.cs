namespace OnIT.Smtp.Core.Entra;

/// <summary>
/// Options for the delegated, interactive sign-in used by the config tool to manage app
/// registrations. TenantId is always the specific customer tenant (GUID or verified domain,
/// e.g. "contoso.onmicrosoft.com") the operator enters on the Entra App tab -- sign-in
/// always targets that tenant directly, never a "common"/"organizations" multi-tenant
/// endpoint, so nothing about this step reaches outside the customer's own tenant.
/// </summary>
public sealed class EntraBootstrapOptions
{
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Overrides GraphWellKnown.DefaultSignInClientId. Set this to a customer-registered,
    /// single-tenant app's client ID if the operator wants their own branding on the
    /// one-time consent screen instead of Microsoft's first-party "Microsoft Graph
    /// PowerShell" app. Optional -- leave unset for the zero-registration default.
    /// </summary>
    public string? ClientIdOverride { get; set; }

    public string ClientId => string.IsNullOrWhiteSpace(ClientIdOverride) ? GraphWellKnown.DefaultSignInClientId : ClientIdOverride!;

    public string RedirectUri { get; set; } = "http://localhost";

    /// <summary>Fall back to device-code sign-in (useful when the config tool runs on a headless/RDP box without a browser).</summary>
    public bool UseDeviceCode { get; set; }
}
