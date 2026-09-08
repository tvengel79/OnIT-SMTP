using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.RemoteApi;

namespace OnIT.Smtp.Bridge;

/// <summary>
/// Maps the bridge's optional HTTPS remote API: POST /api/config to push a new configuration
/// (protects the plaintext client secret with this host's own ISecretProtector, saves it, and
/// reloads -- the same effect as replacing config.json on the mounted volume by hand), and
/// GET /api/status to read live status, mirroring what the local named-pipe IPC already
/// returns to the Windows config tool. Every request must carry the pairing token as a Bearer
/// credential; TLS itself is handled by Kestrel with the certificate
/// RemoteApiCertificateProvider loaded/created at startup -- the client pins its fingerprint
/// rather than validating a certificate chain, since there is no CA here.
/// </summary>
public static class RemoteApiEndpoints
{
    public static void Map(
        WebApplication app,
        string pairingToken,
        ConfigStore configStore,
        ISecretProtector secretProtector,
        BridgeRelayWorker relayWorker)
    {
        app.Use(async (context, next) =>
        {
            if (!IsAuthorized(context.Request, pairingToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized.");
                return;
            }

            await next(context);
        });

        app.MapPost("/api/config", async (PushConfigRequest request, CancellationToken ct) =>
        {
            var config = configStore.Load();

            config.EntraApp.TenantId = request.EntraApp.TenantId;
            config.EntraApp.ApplicationId = request.EntraApp.ApplicationId;
            config.EntraApp.DisplayName = request.EntraApp.DisplayName;
            config.EntraApp.AuthMode = request.EntraApp.AuthMode;
            config.EntraApp.ProtectedClientSecret = request.EntraApp.ClientSecret is null
                ? null
                : secretProtector.Protect(request.EntraApp.ClientSecret);
            config.EntraApp.ClientSecretExpiresOn = request.EntraApp.ClientSecretExpiresOn;
            config.EntraApp.AdminConsentGranted = request.EntraApp.AdminConsentGranted;

            config.AllowedSenders = request.AllowedSenders;
            config.IpAllowRules = request.IpAllowRules;
            config.SmtpListener = request.SmtpListener;
            config.Logging = request.Logging;
            config.Advanced = request.Advanced;
            if (request.SecretExpiryNotifications is not null)
                config.SecretExpiryNotifications = request.SecretExpiryNotifications;

            configStore.Save(config);

            var result = await relayWorker.ReloadConfigAsync(ct);
            return result.Success ? Results.Ok() : Results.Problem(result.ErrorMessage, statusCode: StatusCodes.Status500InternalServerError);
        });

        app.MapGet("/api/status", () => Results.Ok(relayWorker.GetStatus()));
    }

    private static bool IsAuthorized(HttpRequest request, string pairingToken)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.Ordinal)) return false;

        var presentedBytes = Encoding.UTF8.GetBytes(header[prefix.Length..]);
        var expectedBytes = Encoding.UTF8.GetBytes(pairingToken);
        if (presentedBytes.Length != expectedBytes.Length) return false;

        return CryptographicOperations.FixedTimeEquals(presentedBytes, expectedBytes);
    }
}
