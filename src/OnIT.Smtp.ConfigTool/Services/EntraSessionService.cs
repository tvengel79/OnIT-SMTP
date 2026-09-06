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

    private EntraSessionService()
    {
    }

    public bool IsSignedIn => _client is not null;

    public GraphServiceClient EnsureClient(string tenantId, bool useDeviceCode, Action<string>? deviceCodePrompt = null)
    {
        if (_client is not null && string.Equals(_signedInTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return _client;
        }

        var options = new EntraBootstrapOptions { TenantId = tenantId, UseDeviceCode = useDeviceCode };
        var (client, _) = _factory.Create(options, deviceCodePrompt);
        _client = client;
        _signedInTenantId = tenantId;
        return client;
    }

    public void SignOut()
    {
        _client = null;
        _signedInTenantId = null;
    }
}
