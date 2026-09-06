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
    public (GraphServiceClient Client, TokenCredential Credential) Create(EntraBootstrapOptions options, Action<string>? deviceCodePrompt = null)
    {
        TokenCredential credential = options.UseDeviceCode
            ? new DeviceCodeCredential(new DeviceCodeCredentialOptions
            {
                ClientId = options.ClientId,
                TenantId = options.TenantId,
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
                RedirectUri = new Uri(options.RedirectUri)
            });

        var client = new GraphServiceClient(credential, GraphWellKnown.DelegatedScopes);
        return (client, credential);
    }
}
