namespace OnIT.Smtp.Core.Entra;

public static class GraphWellKnown
{
    /// <summary>The well-known application (client) ID of the "Microsoft Graph" first-party resource.</summary>
    public const string MicrosoftGraphResourceAppId = "00000003-0000-0000-c000-000000000000";

    /// <summary>The application-permission (Role) value we need on the created app: send mail as any/configured mailbox.</summary>
    public const string MailSendAppRoleValue = "Mail.Send";

    /// <summary>
    /// Client (application) ID of the OnIT-SMTP Config Tool's own multi-tenant Entra app
    /// registration, used purely for delegated, interactive sign-in so an administrator can
    /// manage app registrations, permissions and admin consent, and browse mailbox users.
    ///
    /// This is registered once by OnIT (the vendor) in a Microsoft Partner/publisher tenant --
    /// it is NOT created per customer. Fill this in before shipping a build; until then the
    /// config tool will show a setup error asking the operator to supply one (see
    /// EntraBootstrapOptions.ClientId override).
    ///
    /// Required delegated permissions on that registration:
    ///   Application.ReadWrite.All, AppRoleAssignment.ReadWrite.All,
    ///   Directory.Read.All, User.Read.All
    /// Public client / mobile & desktop platform, redirect URI: http://localhost
    /// </summary>
    public const string ConfigToolClientId = "00000000-0000-0000-0000-000000000000";

    public static readonly string[] DelegatedScopes =
    {
        "https://graph.microsoft.com/Application.ReadWrite.All",
        "https://graph.microsoft.com/AppRoleAssignment.ReadWrite.All",
        "https://graph.microsoft.com/Directory.Read.All",
        "https://graph.microsoft.com/User.Read.All"
    };
}
