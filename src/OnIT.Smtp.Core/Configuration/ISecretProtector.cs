namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Encrypts/decrypts secrets (client secrets) at rest. Implementations must be portable --
/// not bound to a specific machine, user profile, or OS keystore -- so a config directory
/// can be copied to another machine (a restore, a migration, seeding the Part 2 container)
/// and still decrypt. See <see cref="PortableSecretProtector"/>, the default implementation
/// used by both the Windows Service and the config tool.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
