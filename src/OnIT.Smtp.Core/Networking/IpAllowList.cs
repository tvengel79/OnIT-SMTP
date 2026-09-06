using System.Net;
using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.Core.Networking;

/// <summary>
/// Evaluates whether a connecting SMTP client is permitted to relay mail,
/// based on the configured set of <see cref="IpAllowRule"/> entries.
/// </summary>
public sealed class IpAllowList
{
    private readonly IReadOnlyList<IpAllowRule> _rules;

    public IpAllowList(IEnumerable<IpAllowRule> rules)
    {
        _rules = rules.ToList();
    }

    /// <summary>
    /// True when at least one enabled rule matches. An empty rule set denies everything
    /// by design -- an administrator must explicitly allow at least one address/range.
    /// </summary>
    public bool IsAllowed(IPAddress client, out IpAllowRule? matchedRule)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(client))
            {
                matchedRule = rule;
                return true;
            }
        }

        matchedRule = null;
        return false;
    }

    public bool IsAllowed(IPAddress client) => IsAllowed(client, out _);
}
