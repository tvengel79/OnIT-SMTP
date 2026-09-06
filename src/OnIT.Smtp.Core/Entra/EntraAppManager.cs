using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Extensions.Logging;

namespace OnIT.Smtp.Core.Entra;

/// <summary>
/// Creates, configures and deletes the Entra ID app registration used by the SMTP bridge
/// to call Microsoft Graph (app-only, Mail.Send). All calls run under the delegated,
/// interactively signed-in administrator produced by <see cref="DelegatedGraphClientFactory"/>.
/// </summary>
public sealed class EntraAppManager
{
    private readonly GraphServiceClient _graph;
    private readonly ILogger<EntraAppManager> _logger;

    public EntraAppManager(GraphServiceClient graph, ILogger<EntraAppManager> logger)
    {
        _graph = graph;
        _logger = logger;
    }

    /// <summary>
    /// Creates the app registration, service principal, client secret, and required Mail.Send
    /// permission, then arranges for admin consent.
    /// </summary>
    /// <param name="attemptAutomaticConsent">
    /// When true (the default), first tries to grant consent directly via the Graph API using
    /// the signed-in account's own rights -- this needs no further action if it works, but
    /// requires the account to hold a role Graph accepts for app-role assignment (Global
    /// Administrator or Privileged Role Administrator; Application Administrator alone is
    /// sometimes not enough, depending on tenant policy), which can fail unpredictably. When
    /// false, that attempt is skipped entirely and only the browser consent URL is returned --
    /// the simpler, more reliable path: the operator opens it, is prompted to sign in (or
    /// already is) as a Global/Application Administrator, and clicks Accept. Either way, the
    /// returned <see cref="EntraAppProvisionResult.AdminConsentUrl"/> is always populated so
    /// the caller can offer the browser option regardless of how consent was (or wasn't)
    /// granted automatically.
    /// </param>
    public async Task<EntraAppProvisionResult> CreateAppAsync(string tenantId, string displayName, bool attemptAutomaticConsent = true, CancellationToken ct = default)
    {
        _logger.LogInformation("Looking up the Microsoft Graph service principal in this tenant.");
        var graphServicePrincipal = await FindMicrosoftGraphServicePrincipalAsync(ct)
            ?? throw new InvalidOperationException("Could not find the Microsoft Graph service principal in this tenant.");

        var mailSendRole = graphServicePrincipal.AppRoles?.FirstOrDefault(r =>
            string.Equals(r.Value, GraphWellKnown.MailSendAppRoleValue, StringComparison.OrdinalIgnoreCase)
            && r.AllowedMemberTypes?.Contains("Application") == true);

        if (mailSendRole?.Id is null)
            throw new InvalidOperationException($"Microsoft Graph does not expose a '{GraphWellKnown.MailSendAppRoleValue}' application role in this tenant.");

        _logger.LogInformation("Creating app registration '{DisplayName}'.", displayName);
        var application = await _graph.Applications.PostAsync(new Application
        {
            DisplayName = displayName,
            SignInAudience = "AzureADMyOrg",
            RequiredResourceAccess = new List<RequiredResourceAccess>
            {
                new()
                {
                    ResourceAppId = GraphWellKnown.MicrosoftGraphResourceAppId,
                    ResourceAccess = new List<ResourceAccess>
                    {
                        new() { Id = mailSendRole.Id, Type = "Role" }
                    }
                }
            }
        }, cancellationToken: ct) ?? throw new InvalidOperationException("Graph did not return the created application.");

        _logger.LogInformation("Creating service principal for app {AppId}.", application.AppId);
        var servicePrincipal = await _graph.ServicePrincipals.PostAsync(new ServicePrincipal
        {
            AppId = application.AppId
        }, cancellationToken: ct) ?? throw new InvalidOperationException("Graph did not return the created service principal.");

        _logger.LogInformation("Creating client secret.");
        var passwordCredential = await _graph.Applications[application.Id].AddPassword.PostAsync(new()
        {
            PasswordCredential = new PasswordCredential
            {
                DisplayName = "OnIT-SMTP (config tool generated)",
                EndDateTime = DateTimeOffset.UtcNow.AddMonths(24)
            }
        }, cancellationToken: ct) ?? throw new InvalidOperationException("Graph did not return the created client secret.");

        var consentUrl = BuildAdminConsentUrl(tenantId, application.AppId!);
        var consentGranted = attemptAutomaticConsent && await TryGrantAdminConsentAsync(
            graphServicePrincipal.Id!, servicePrincipal.Id!, mailSendRole.Id.Value, ct);

        return new EntraAppProvisionResult
        {
            TenantId = tenantId,
            ApplicationId = application.AppId!,
            ApplicationObjectId = application.Id!,
            ServicePrincipalObjectId = servicePrincipal.Id!,
            DisplayName = application.DisplayName!,
            ClientSecret = passwordCredential.SecretText!,
            ClientSecretExpiresOn = passwordCredential.EndDateTime!.Value,
            AdminConsentGranted = consentGranted,
            AdminConsentUrl = consentUrl
        };
    }

    /// <summary>
    /// Attempts to grant admin consent for the Mail.Send application permission by directly
    /// creating the app role assignment. This requires the signed-in account to hold a role
    /// Graph accepts for this call (typically Global Administrator or Privileged Role
    /// Administrator); it can fail with Forbidden even for a Global Administrator depending on
    /// tenant policy (e.g. Conditional Access, Restricted Management Administrative Units) --
    /// callers should always fall back to the browser consent URL (see
    /// <see cref="BuildAdminConsentUrl"/>) rather than treat a false result as fatal.
    /// </summary>
    private async Task<bool> TryGrantAdminConsentAsync(
        string graphServicePrincipalId, string ourServicePrincipalId, Guid mailSendRoleId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Attempting to grant admin consent for Mail.Send automatically.");
            await _graph.ServicePrincipals[graphServicePrincipalId].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
            {
                PrincipalId = Guid.Parse(ourServicePrincipalId),
                ResourceId = Guid.Parse(graphServicePrincipalId),
                AppRoleId = mailSendRoleId
            }, cancellationToken: ct);

            _logger.LogInformation("Admin consent granted automatically.");
            return true;
        }
        catch (ODataError ex) when (ex.ResponseStatusCode is 403 or 401)
        {
            _logger.LogWarning("The signed-in account cannot grant admin consent directly ({Status}). " +
                                "Use the browser consent link instead.", ex.ResponseStatusCode);
            return false;
        }
    }

    public static string BuildAdminConsentUrl(string tenantId, string appId) =>
        $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/adminconsent?client_id={Uri.EscapeDataString(appId)}";

    /// <summary>
    /// Re-checks whether admin consent has since been granted (e.g. an admin approved the
    /// pending consent URL out of band). Returns true once the app role assignment exists.
    /// </summary>
    public async Task<bool> HasAdminConsentAsync(string servicePrincipalObjectId, CancellationToken ct = default)
    {
        var assignments = await _graph.ServicePrincipals[servicePrincipalObjectId].AppRoleAssignments.GetAsync(cancellationToken: ct);
        return assignments?.Value?.Any(a =>
            string.Equals(a.ResourceDisplayName, "Microsoft Graph", StringComparison.OrdinalIgnoreCase)) == true;
    }

    public async Task DeleteAppAsync(string applicationObjectId, string? servicePrincipalObjectId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(servicePrincipalObjectId))
        {
            try
            {
                _logger.LogInformation("Deleting service principal {Id}.", servicePrincipalObjectId);
                await _graph.ServicePrincipals[servicePrincipalObjectId].DeleteAsync(cancellationToken: ct);
            }
            catch (ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // Already gone -- nothing to do.
            }
        }

        try
        {
            _logger.LogInformation("Deleting app registration {Id}.", applicationObjectId);
            await _graph.Applications[applicationObjectId].DeleteAsync(cancellationToken: ct);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // Already gone -- nothing to do.
        }
    }

    private async Task<ServicePrincipal?> FindMicrosoftGraphServicePrincipalAsync(CancellationToken ct)
    {
        var result = await _graph.ServicePrincipals.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = $"appId eq '{GraphWellKnown.MicrosoftGraphResourceAppId}'";
            rc.QueryParameters.Select = new[] { "id", "appId", "appRoles" };
        }, ct);

        return result?.Value?.FirstOrDefault();
    }
}
