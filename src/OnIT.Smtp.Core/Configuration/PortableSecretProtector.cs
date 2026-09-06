using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// AES-256-GCM secret protector using a locally stored key file rather than an OS/user/
/// machine-bound mechanism (e.g. Windows DPAPI). This is deliberate: the whole point of
/// keeping config.json portable is that an operator can copy %ProgramData%\OnIT-SMTP (or
/// the equivalent Docker bridge config directory) to a different machine -- a restore, a
/// migration, seeding the Part 2 container -- and have the client secret decrypt there
/// without re-entering anything or redoing Entra admin consent. DPAPI's LocalMachine scope
/// cannot do that: ciphertext it produces is only decryptable on the machine that made it.
///
/// The key file (<see cref="KeyFilePath"/>) travels with config.json as part of "the
/// config" -- back up, copy, or transfer both together. Protection against casual exposure
/// of config.json alone (e.g. an accidental commit, a partial backup) comes from the secret
/// being ciphertext without the key; protection against someone with full access to the
/// machine/folder comes from OS file permissions on the key file (NTFS ACLs on Windows,
/// 0600 elsewhere), not from any per-machine cryptographic binding.
/// </summary>
public sealed class PortableSecretProtector : ISecretProtector
{
    private const int KeySizeBytes = 32; // AES-256
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly string _keyFilePath;
    private readonly Lazy<byte[]> _key;

    public PortableSecretProtector(string? keyFilePath = null)
    {
        _keyFilePath = keyFilePath ?? DefaultKeyFilePath;
        _key = new Lazy<byte[]>(LoadOrCreateKey);
    }

    public static string DefaultKeyFilePath => Path.Combine(ConfigPaths.RootDirectory, "secret.key");

    public string Protect(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key.Value, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var output = new byte[NonceSizeBytes + cipherBytes.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, output, NonceSizeBytes, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, output, NonceSizeBytes + cipherBytes.Length, TagSizeBytes);

        return Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedText)
    {
        var input = Convert.FromBase64String(protectedText);
        if (input.Length < NonceSizeBytes + TagSizeBytes)
            throw new FormatException("Protected value is too short to be valid.");

        var nonce = input.AsSpan(0, NonceSizeBytes);
        var cipherLength = input.Length - NonceSizeBytes - TagSizeBytes;
        var cipherBytes = input.AsSpan(NonceSizeBytes, cipherLength);
        var tag = input.AsSpan(NonceSizeBytes + cipherLength, TagSizeBytes);

        var plainBytes = new byte[cipherLength];
        using (var aesGcm = new AesGcm(_key.Value, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    private byte[] LoadOrCreateKey()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_keyFilePath)!);

        if (File.Exists(_keyFilePath))
        {
            var existing = File.ReadAllBytes(_keyFilePath);
            if (existing.Length == KeySizeBytes) return existing;
            throw new InvalidOperationException($"The key file at '{_keyFilePath}' is not a valid {KeySizeBytes}-byte key.");
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        File.WriteAllBytes(_keyFilePath, key);
        RestrictKeyFilePermissions(_keyFilePath);
        return key;
    }

    /// <summary>Best-effort lockdown so only administrators/the owner can read the key file.</summary>
    private static void RestrictKeyFilePermissions(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsAcl.Restrict(path);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // Best-effort: never fail key creation because permission hardening didn't work
            // (e.g. non-NTFS volume, restricted container filesystem).
        }
    }

    private static class WindowsAcl
    {
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void Restrict(string path)
        {
            var fileInfo = new FileInfo(path);
            var security = fileInfo.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            fileInfo.SetAccessControl(security);
        }
    }
}
