namespace OnIT.Smtp.Core.Entra;

public sealed class MailboxUser
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string UserPrincipalName { get; init; }
    public string? Mail { get; init; }
    public bool AccountEnabled { get; init; }
}
