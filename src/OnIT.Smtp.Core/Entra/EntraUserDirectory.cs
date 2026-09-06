using Microsoft.Graph;

namespace OnIT.Smtp.Core.Entra;

/// <summary>
/// Browses the tenant's mailbox-enabled users so the config tool's "Allowed Senders"
/// picker can be populated without the operator having to type addresses by hand.
/// </summary>
public sealed class EntraUserDirectory
{
    private readonly GraphServiceClient _graph;

    public EntraUserDirectory(GraphServiceClient graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Returns users that have a mailbox (proxy: a non-null "mail" attribute -- covers
    /// cloud and hybrid Exchange Online mailboxes, which is the common case). Optionally
    /// filter by a display-name/UPN search term for the picker's search box.
    /// </summary>
    public async Task<IReadOnlyList<MailboxUser>> ListMailboxUsersAsync(string? searchTerm = null, CancellationToken ct = default)
    {
        var results = new List<MailboxUser>();

        var page = await _graph.Users.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = "mail ne null";
            rc.QueryParameters.Select = new[] { "id", "displayName", "userPrincipalName", "mail", "accountEnabled" };
            rc.QueryParameters.Top = 999;
            rc.QueryParameters.Orderby = new[] { "displayName" };
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                rc.QueryParameters.Search = $"\"displayName:{searchTerm}\" OR \"userPrincipalName:{searchTerm}\"";
                rc.QueryParameters.Filter = null;
                rc.QueryParameters.Orderby = null;
            }
            rc.Headers.Add("ConsistencyLevel", "eventual");
        }, ct);

        var iterator = Microsoft.Graph.PageIterator<Microsoft.Graph.Models.User, Microsoft.Graph.Models.UserCollectionResponse>
            .CreatePageIterator(_graph, page!, user =>
            {
                results.Add(new MailboxUser
                {
                    Id = user.Id!,
                    DisplayName = user.DisplayName ?? user.UserPrincipalName ?? user.Id!,
                    UserPrincipalName = user.UserPrincipalName ?? string.Empty,
                    Mail = user.Mail,
                    AccountEnabled = user.AccountEnabled ?? false
                });
                return true;
            });

        await iterator.IterateAsync(ct);
        return results;
    }
}
