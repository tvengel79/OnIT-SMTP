using Azure.Core;
using Azure.Identity;
using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.Core.Mail;

/// <summary>
/// Builds the app-only (client credentials) TokenCredential used to actually send mail --
/// distinct from the delegated, interactive credential used by EntraAppManager to manage
/// app registrations.
/// </summary>
public sealed class GraphCredentialFactory
{
    private readonly ISecretProtector _secretProtector;

    public GraphCredentialFactory(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public TokenCredential Create(EntraAppSettings settings)
    {
        if (!settings.IsConfigured)
            throw new InvalidOperationException("The Entra app is not configured yet.");

        return settings.AuthMode switch
        {
            GraphAuthMode.ClientSecret => CreateFromSecret(settings),
            GraphAuthMode.Certificate => CreateFromCertificate(settings),
            _ => throw new NotSupportedException($"Unsupported auth mode '{settings.AuthMode}'.")
        };
    }

    private TokenCredential CreateFromSecret(EntraAppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
            throw new InvalidOperationException("No client secret is stored for this app registration.");

        var secret = _secretProtector.Unprotect(settings.ProtectedClientSecret);
        return new ClientSecretCredential(settings.TenantId, settings.ApplicationId, secret);
    }

    private static TokenCredential CreateFromCertificate(EntraAppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CertificateThumbprint))
            throw new InvalidOperationException("No certificate thumbprint is configured for this app registration.");

        using var store = new System.Security.Cryptography.X509Certificates.X509Store(
            System.Security.Cryptography.X509Certificates.StoreName.My,
            System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
        store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);

        var matches = store.Certificates.Find(
            System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
            settings.CertificateThumbprint, validOnly: false);

        if (matches.Count == 0)
            throw new InvalidOperationException($"Certificate with thumbprint '{settings.CertificateThumbprint}' was not found in LocalMachine\\My.");

        return new ClientCertificateCredential(settings.TenantId, settings.ApplicationId, matches[0]);
    }
}
