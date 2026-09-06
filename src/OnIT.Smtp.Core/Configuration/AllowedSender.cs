namespace OnIT.Smtp.Core.Configuration;

/// <summary>
/// A mailbox that is permitted to be used as the "from" address for relayed mail.
/// Populated by picking from the tenant's mailbox-enabled users (see EntraUserDirectory),
/// so IDs line up with real Entra objects and survive display-name changes.
/// </summary>
public sealed class AllowedSender
{
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EntraObjectId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
