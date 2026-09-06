namespace OnIT.Smtp.Core.Entra;

public static class GraphWellKnown
{
    /// <summary>The well-known application (client) ID of the "Microsoft Graph" first-party resource.</summary>
    public const string MicrosoftGraphResourceAppId = "00000003-0000-0000-c000-000000000000";

    /// <summary>The application-permission (Role) value we need on the created app: send mail as any/configured mailbox.</summary>
    public const string MailSendAppRoleValue = "Mail.Send";

    /// <summary>
    /// Client (application) ID used purely to bootstrap the delegated, interactive sign-in
    /// so an administrator can manage app registrations, permissions, and admin consent, and
    /// browse mailbox users -- all of it executed directly against the target tenant.
    ///
    /// This is Microsoft's own first-party "Microsoft Graph PowerShell" application. It is
    /// NOT owned by, or registered by, OnIT -- it already exists in every Entra tenant (the
    /// same way `Connect-MgGraph` works out of the box), so there is nothing to pre-register
    /// and nothing that lives outside the customer's own tenant. The operator still has to
    /// consent to it the first time they sign in (it requests high-privilege delegated
    /// permissions, so that consent has to come from a Global/Application Administrator) --
    /// that consent, like everything else, is granted inside the customer's tenant only.
    ///
    /// An operator who would rather see their own branding on that one-time consent screen
    /// can instead register a single-tenant app themselves (a couple of clicks in the Entra
    /// portal, "Accounts in this organizational directory only") with the same delegated
    /// permissions below, and paste its client ID into the config tool's "Sign-in app"
    /// field -- see EntraBootstrapOptions.ClientIdOverride. Either way, everything this app
    /// does after sign-in happens in that same tenant.
    ///
    /// Required delegated permissions:
    ///   Application.ReadWrite.All, AppRoleAssignment.ReadWrite.All,
    ///   Directory.Read.All, User.Read.All
    /// </summary>
    public const string DefaultSignInClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

    public static readonly string[] DelegatedScopes =
    {
        "https://graph.microsoft.com/Application.ReadWrite.All",
        "https://graph.microsoft.com/AppRoleAssignment.ReadWrite.All",
        "https://graph.microsoft.com/Directory.Read.All",
        "https://graph.microsoft.com/User.Read.All"
    };
}
