namespace OnIT.Smtp.Core.Entra;

public sealed class SecretRenewalResult
{
    public required string ClientSecret { get; init; }
    public required DateTimeOffset ClientSecretExpiresOn { get; init; }
}
