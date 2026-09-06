using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Windows DPAPI-backed secret protector, scoped to the local machine so both the
/// Windows Service (running as a service account) and the config tool (running as
/// an interactive admin) can decrypt the same ciphertext.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    // Binds ciphertext to this application so a stray DPAPI blob from another app
    // on the same machine cannot be swapped in.
    private static readonly byte[] Entropy = "OnIT-SMTP.v1"u8.ToArray();

    public string Protect(string plainText)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(cipherBytes);
    }

    public string Unprotect(string protectedText)
    {
        var cipherBytes = Convert.FromBase64String(protectedText);
        var plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.LocalMachine);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
