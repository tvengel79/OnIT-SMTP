using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OnIT.Smtp.Core.RemoteApi;

/// <summary>
/// Loads (or generates, on first boot) the self-signed TLS certificate the bridge's remote
/// API listens with. There is no CA here on purpose: a config tool pairing with a bridge for
/// the first time has no other way to establish trust than pinning this certificate's SHA-256
/// fingerprint -- printed once to the log when the certificate is created -- so the operator
/// copies it into the config tool alongside the pairing token. Same "key file travels with the
/// config, protected by file permissions rather than machine identity" pattern as
/// PortableSecretProtector's secret.key.
/// </summary>
public static class RemoteApiCertificateProvider
{
    private const string Subject = "CN=OnIT-SMTP Bridge";

    public static (X509Certificate2 Certificate, string FingerprintHex, bool WasFreshlyGenerated) LoadOrCreate(string pfxPath)
    {
        if (File.Exists(pfxPath))
        {
            var existing = new X509Certificate2(pfxPath, (string?)null, X509KeyStorageFlags.Exportable);
            return (existing, ComputeFingerprint(existing), false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(pfxPath)!);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(Subject, ecdsa, HashAlgorithmName.SHA256);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("onit-smtp-bridge");
        request.CertificateExtensions.Add(sanBuilder.Build());

        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

        var pfxBytes = certificate.Export(X509ContentType.Pfx);
        File.WriteAllBytes(pfxPath, pfxBytes);
        RestrictFilePermissions(pfxPath);

        // Re-load from the exported bytes rather than returning the freshly-minted certificate
        // directly: round-tripping through PFX is what reliably yields a usable private-key
        // handle for Kestrel across platforms.
        var reloaded = new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable);
        return (reloaded, ComputeFingerprint(reloaded), true);
    }

    public static string ComputeFingerprint(X509Certificate2 certificate)
    {
        var hash = SHA256.HashData(certificate.RawData);
        return string.Join(":", hash.Select(b => b.ToString("X2")));
    }

    private static void RestrictFilePermissions(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // Best-effort: never fail certificate creation because permission hardening didn't work.
        }
    }
}
