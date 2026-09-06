using Microsoft.Graph;
using OnIT.Smtp.Core.Entra;

namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>
/// Caches the delegated, interactively-signed-in GraphServiceClient for the lifetime of the
/// config tool, so an operator only has to sign in once per session no matter how many
/// tabs (create app, check consent, browse mailbox users) need delegated Graph access.
/// </summary>
public sealed class EntraSessionService
{
    public static EntraSessionService Instance { get; } = new();

    private readonly DelegatedGraphClientFactory _factory = new();
    private GraphServiceClient? _client;
    private string? _signedInTenantId;
    private string? _signedInClientId;

    private EntraSessionService()
    {
    }

    public bool IsSignedIn => _client is not null;

    /// <summary>
    /// Set from the Entra App tab's optional "Sign-in app" field if the operator wants to use
    /// their own single-tenant app registration instead of the zero-registration default
    /// (Microsoft's first-party "Microsoft Graph PowerShell" client). Null uses the default.
    /// </summary>
    public string? ClientIdOverride { get; set; }

    public GraphServiceClient EnsureClient(string tenantId, bool useDeviceCode, Action<string>? deviceCodePrompt = null)
    {
        var effectiveClientId = string.IsNullOrWhiteSpace(ClientIdOverride) ? GraphWellKnown.DefaultSignInClientId : ClientIdOverride;

        if (_client is not null
            && string.Equals(_signedInTenantId, tenantId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_signedInClientId, effectiveClientId, StringComparison.OrdinalIgnoreCase))
        {
            return _client;
        }

        var options = new EntraBootstrapOptions { TenantId = tenantId, UseDeviceCode = useDeviceCode, ClientIdOverride = ClientIdOverride };
        var (client, _) = _factory.Create(options, deviceCodePrompt);
        _client = client;
        _signedInTenantId = tenantId;
        _signedInClientId = effectiveClientId;
        return client;
    }

    public void SignOut()
    {
        _client = null;
        _signedInTenantId = null;
        _signedInClientId = null;
    }
}
