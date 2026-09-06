namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// Encrypts/decrypts secrets (client secrets) at rest. The Windows implementation uses
/// DPAPI scoped to the local machine (so the service, running as LocalSystem/a service
/// account, can decrypt independently of any interactive user). Part 2's Linux/Docker
/// bridge supplies its own implementation (e.g. a key file or container secret).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
