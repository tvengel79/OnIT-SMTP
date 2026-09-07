using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.Core.Runtime;

/// <summary>Enforces AdvancedSettings.RestrictToAllowedSenders against the configured AllowedSenders list.</summary>
public static class SenderPolicy
{
    public static bool IsSenderPermitted(AppConfiguration config, string mailFrom)
    {
        if (!config.Advanced.RestrictToAllowedSenders) return true;

        return config.AllowedSenders.Any(s =>
            s.Enabled && string.Equals(s.UserPrincipalName, mailFrom, StringComparison.OrdinalIgnoreCase));
    }
}
