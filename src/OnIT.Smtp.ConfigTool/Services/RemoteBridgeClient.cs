using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using OnIT.Smtp.Core.Ipc;
using OnIT.Smtp.Core.RemoteApi;

namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>
/// Talks to a paired bridge's remote API. TLS trust is pinning, not chain validation -- there
/// is no CA involved, so the certificate presented on each connection must match the
/// fingerprint the operator entered when pairing (read from the bridge's boot log), or the
/// connection is rejected outright.
/// </summary>
public sealed class RemoteBridgeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<(bool Success, string Message)> PushConfigAsync(
        RemoteBridgeConnection bridge, string pairingToken, PushConfigRequest request, CancellationToken ct)
    {
        using var client = CreateClient(bridge, pairingToken);

        try
        {
            var response = await client.PostAsJsonAsync($"https://{bridge.Host}:{bridge.Port}/api/config", request, JsonOptions, ct);
            if (response.IsSuccessStatusCode) return (true, "Configuration pushed and applied.");

            var body = await response.Content.ReadAsStringAsync(ct);
            return (false, $"Push failed ({(int)response.StatusCode}): {body}");
        }
        catch (Exception ex)
        {
            return (false, $"Push failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, ServiceStatus? Status, string Message)> GetStatusAsync(
        RemoteBridgeConnection bridge, string pairingToken, CancellationToken ct)
    {
        using var client = CreateClient(bridge, pairingToken);

        try
        {
            var response = await client.GetAsync($"https://{bridge.Host}:{bridge.Port}/api/status", ct);
            if (!response.IsSuccessStatusCode) return (false, null, $"Status request failed ({(int)response.StatusCode}).");

            var status = await response.Content.ReadFromJsonAsync<ServiceStatus>(JsonOptions, ct);
            return (true, status, "OK");
        }
        catch (Exception ex)
        {
            return (false, null, $"Status request failed: {ex.Message}");
        }
    }

    private static HttpClient CreateClient(RemoteBridgeConnection bridge, string pairingToken)
    {
        var pinnedFingerprint = bridge.CertificateFingerprint.Trim().ToUpperInvariant();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && ComputeFingerprint(certificate) == pinnedFingerprint
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pairingToken);
        return client;
    }

    private static string ComputeFingerprint(X509Certificate2 certificate)
    {
        var hash = SHA256.HashData(certificate.RawData);
        return string.Join(":", hash.Select(b => b.ToString("X2")));
    }
}
