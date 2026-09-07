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
    /// tool is relaunched.
    /// </summary>
    private static readonly TokenCachePersistenceOptions CacheOptions = new() { Name = "OnIT-SMTP.ConfigTool" };

    /// <summary>
    /// Setting TokenCachePersistenceOptions alone persists the token cache but does not, by
    /// itself, tell a freshly constructed credential which cached account to silently reuse
    /// on the next process launch -- without an AuthenticationRecord it has nothing to look
    /// up and falls back to interactive every time, which is exactly the "still prompts after
    /// every restart" behavior this class previously had despite the cache being persisted.
    /// This is the officially documented pattern for closing that gap: capture the
    /// AuthenticationRecord from the first sign-in and round-trip it to disk ourselves.
    /// </summary>
    private static string AuthRecordPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnIT-SMTP", "delegated-auth-record.json");

    public async Task<(GraphServiceClient Client, TokenCredential Credential)> CreateAsync(
        EntraBootstrapOptions options, Action<string>? deviceCodePrompt = null, CancellationToken ct = default)
    {
        var existingRecord = await TryLoadAuthenticationRecordAsync(ct);
        var scopes = new TokenRequestContext(GraphWellKnown.DelegatedScopes);

        TokenCredential credential;
        AuthenticationRecord? newRecord = null;

        if (options.UseDeviceCode)
        {
            var deviceCodeCredential = new DeviceCodeCredential(new DeviceCodeCredentialOptions
            {
                ClientId = options.ClientId,
                TenantId = options.TenantId,
                TokenCachePersistenceOptions = CacheOptions,
                AuthenticationRecord = existingRecord,
                DeviceCodeCallback = (info, _) =>
                {
                    deviceCodePrompt?.Invoke(info.Message);
                    return Task.CompletedTask;
                }
            });

            if (existingRecord is null)
            {
                newRecord = await deviceCodeCredential.AuthenticateAsync(scopes, ct);
            }

            credential = deviceCodeCredential;
        }
        else
        {
            var browserCredential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                ClientId = options.ClientId,
                TenantId = options.TenantId,
                RedirectUri = new Uri(options.RedirectUri),
                TokenCachePersistenceOptions = CacheOptions,
                AuthenticationRecord = existingRecord
            });

            if (existingRecord is null)
            {
                newRecord = await browserCredential.AuthenticateAsync(scopes, ct);
            }

            credential = browserCredential;
        }

        if (newRecord is not null)
        {
            await TrySaveAuthenticationRecordAsync(newRecord, ct);
        }

        var client = new GraphServiceClient(credential, GraphWellKnown.DelegatedScopes);
        return (client, credential);
    }

    private static async Task<AuthenticationRecord?> TryLoadAuthenticationRecordAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(AuthRecordPath)) return null;
            await using var stream = File.OpenRead(AuthRecordPath);
            return await AuthenticationRecord.DeserializeAsync(stream, ct);
        }
        catch
        {
            // Corrupt, unreadable, or from an incompatible SDK version -- fall back to a
            // fresh interactive sign-in rather than failing the whole operation over this.
            return null;
        }
    }

    private static async Task TrySaveAuthenticationRecordAsync(AuthenticationRecord record, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AuthRecordPath)!);
            await using var stream = File.Create(AuthRecordPath);
            await record.SerializeAsync(stream, ct);
        }
        catch
        {
            // Best-effort: losing this just means the next launch prompts interactively again.
        }
    }
}
