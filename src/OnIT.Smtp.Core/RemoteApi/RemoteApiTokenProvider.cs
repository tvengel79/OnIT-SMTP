using System.Security.Cryptography;

namespace OnIT.Smtp.Core.RemoteApi;

/// <summary>
/// Loads (or generates, on first boot) the bearer token the bridge's remote API requires on
/// every request. Printed once to the log when created, the same as the TLS certificate's
/// fingerprint -- an operator copies both, once, into the config tool to pair. Not re-logged
/// on subsequent boots so it doesn't linger in log aggregation beyond that first read; delete
/// the token file to force a fresh one (existing pairings will need to be redone).
/// </summary>
public static class RemoteApiTokenProvider
{
    private const int TokenSizeBytes = 32;

    public static (string Token, bool WasFreshlyGenerated) LoadOrCreate(string tokenPath)
    {
        if (File.Exists(tokenPath))
        {
            return (File.ReadAllText(tokenPath).Trim(), false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(tokenPath)!);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenSizeBytes));
        File.WriteAllText(tokenPath, token);
        RestrictFilePermissions(tokenPath);
        return (token, true);
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
            // Best-effort: never fail token creation because permission hardening didn't work.
        }
    }
}
