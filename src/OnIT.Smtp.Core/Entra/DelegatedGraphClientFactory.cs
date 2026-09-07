using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;

namespace OnIT.Smtp.Core.Entra;

/// <summary>
/// Builds a delegated (signed-in-admin) GraphServiceClient for app-registration management,
/// as opposed to the app-only client credentials client used for actually sending mail
/// (see OnIT.Smtp.Core.Mail.GraphMailService).
/// </summary>
public sealed class DelegatedGraphClientFactory
{
    /// <summary>
    /// Persists the MSAL token cache to disk (DPAPI-protected under the signed-in Windows
    /// user's profile) so the operator isn't prompted to sign in again every time the config
    /// tool is relaunched -- only EntraSessionService's in-memory cache was previously scoped
    /// to a single process run, which meant every restart forced a fresh interactive sign-in
    /// even though the tenant/scopes hadn't changed.
    /// </summary>
    private static readonly TokenCachePersistenceOptions CacheOptions = new() { Name = "OnIT-SMTP.ConfigTool" };

    public (GraphServiceClient Client, TokenCredential Credential) Create(EntraBootstrapOptions options, Action<string>? deviceCodePrompt = null)
    {
        TokenCredential credential = options.UseDeviceCode
            ? new DeviceCodeCredential(new DeviceCodeCredentialOptions
            {
                ClientId = options.ClientId,
                TenantId = options.TenantId,
                TokenCachePersistenceOptions = CacheOptions,
                DeviceCodeCallback = (info, _) =>
                {
                    deviceCodePrompt?.Invoke(info.Message);
                    return Task.CompletedTask;
                }
            })
            : new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                ClientId = options.ClientId,
                TenantId = options.TenantId,
                RedirectUri = new Uri(options.RedirectUri),
                TokenCachePersistenceOptions = CacheOptions
            });

        var client = new GraphServiceClient(credential, GraphWellKnown.DelegatedScopes);
        return (client, credential);
    }
}
