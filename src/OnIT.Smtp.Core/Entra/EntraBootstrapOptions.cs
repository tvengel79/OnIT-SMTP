namespace OnIT.Smtp.Core.Entra;

/// <summary>
/// Options for the delegated, interactive sign-in used by the config tool to manage
/// app registrations. TenantId can be "organizations", "common", or a specific tenant
/// GUID/domain -- the operator picks their tenant during the "Create Entra App" wizard.
/// </summary>
public sealed class EntraBootstrapOptions
{
    public string TenantId { get; set; } = "organizations";

    /// <summary>Overrides GraphWellKnown.ConfigToolClientId, e.g. for a self-registered management app.</summary>
    public string? ClientIdOverride { get; set; }

    public string ClientId => string.IsNullOrWhiteSpace(ClientIdOverride) ? GraphWellKnown.ConfigToolClientId : ClientIdOverride!;

    public string RedirectUri { get; set; } = "http://localhost";

    /// <summary>Fall back to device-code sign-in (useful when the config tool runs on a headless/RDP box without a browser).</summary>
    public bool UseDeviceCode { get; set; }
}
