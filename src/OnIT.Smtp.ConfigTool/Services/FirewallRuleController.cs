using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OnIT.Smtp.ConfigTool.Services;

public enum FirewallRuleStatus
{
    /// <summary>An enabled, allow rule exists for exactly the configured port.</summary>
    Allowed,

    /// <summary>No OnIT-SMTP rule exists at all.</summary>
    NotConfigured,

    /// <summary>A rule exists but for a different port than the one currently configured.</summary>
    WrongPort,

    /// <summary>A rule exists for the right port but is disabled or set to Block.</summary>
    DisabledOrBlocking,

    /// <summary>Could not query the firewall (e.g. netsh unavailable or access denied).</summary>
    Unknown
}

/// <summary>
/// Checks and manages a single, dedicated Windows Firewall inbound rule for the configured
/// SMTP listener port, via netsh (same shell-out pattern as WindowsServiceController). Only
/// ever touches the one rule it owns (by name) -- never scans or modifies unrelated rules.
/// </summary>
public static class FirewallRuleController
{
    private const string RuleName = "OnIT-SMTP SMTP Listener";

    public static FirewallRuleStatus GetStatus(int expectedPort)
    {
        var (success, output) = RunNetsh($"advfirewall firewall show rule name=\"{RuleName}\"");
        if (!success) return FirewallRuleStatus.Unknown;

        if (output.Contains("No rules match the specified criteria", StringComparison.OrdinalIgnoreCase))
        {
            return FirewallRuleStatus.NotConfigured;
        }

        var portMatch = Regex.Match(output, @"LocalPort:\s*(\d+)");
        if (!portMatch.Success) return FirewallRuleStatus.Unknown;

        var rulePort = int.Parse(portMatch.Groups[1].Value);

        var enabledMatch = Regex.Match(output, @"Enabled:\s*(Yes|No)", RegexOptions.IgnoreCase);
        var actionMatch = Regex.Match(output, @"Action:\s*(Allow|Block)", RegexOptions.IgnoreCase);

        var enabled = enabledMatch.Success && string.Equals(enabledMatch.Groups[1].Value, "Yes", StringComparison.OrdinalIgnoreCase);
        var allows = actionMatch.Success && string.Equals(actionMatch.Groups[1].Value, "Allow", StringComparison.OrdinalIgnoreCase);

        if (!enabled || !allows) return FirewallRuleStatus.DisabledOrBlocking;
        return rulePort == expectedPort ? FirewallRuleStatus.Allowed : FirewallRuleStatus.WrongPort;
    }

    /// <summary>Creates (or replaces, if the port changed) the dedicated inbound-allow rule for the given TCP port.</summary>
    public static (bool Success, string Output) EnsureRule(int port)
    {
        // Idempotent: drop any existing rule under our name first, so re-running this after a
        // port change replaces rather than adds a second, stale rule.
        RunNetsh($"advfirewall firewall delete rule name=\"{RuleName}\"");

        return RunNetsh(
            $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP " +
            $"localport={port} profile=any description=\"Allows inbound SMTP relay traffic for the OnIT-SMTP service.\"");
    }

    private static (bool Success, string Output) RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo("netsh.exe", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode == 0, output.Trim());
    }
}
