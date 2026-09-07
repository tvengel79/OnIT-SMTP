using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class SmtpListenerViewModel : ObservableObject
{
    [ObservableProperty] private string _bindAddress;
    [ObservableProperty] private int _port;
    [ObservableProperty] private int _maxMessageSizeMb;
    [ObservableProperty] private int _sessionTimeoutSeconds;
    [ObservableProperty] private string _serverHostName;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private string _firewallStatusMessage = string.Empty;
    [ObservableProperty] private bool _firewallNeedsAttention;

    public SmtpListenerViewModel()
    {
        var settings = ConfigurationContext.Instance.Current.SmtpListener;
        _bindAddress = settings.BindAddress;
        _port = settings.Port;
        _maxMessageSizeMb = settings.MaxMessageSizeBytes / (1024 * 1024);
        _sessionTimeoutSeconds = settings.SessionTimeoutSeconds;
        _serverHostName = settings.ServerHostName;

        RefreshFirewallStatus();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = ConfigurationContext.Instance.Current.SmtpListener;
        settings.BindAddress = BindAddress;
        settings.Port = Port;
        settings.MaxMessageSizeBytes = MaxMessageSizeMb * 1024 * 1024;
        settings.SessionTimeoutSeconds = SessionTimeoutSeconds;
        settings.ServerHostName = ServerHostName;
        ConfigurationContext.Instance.Save();

        if (PipeClientService.Instance.IsConnected)
        {
            var result = await PipeClientService.Instance.ReloadConfigAsync();
            StatusMessage = result?.Success == true
                ? "Saved and applied immediately."
                : $"Saved. The service could not reload automatically: {result?.ErrorMessage ?? "no response"}.";
        }
        else
        {
            StatusMessage = "Saved. It will take effect the next time the service starts (it is not currently reachable).";
        }

        RefreshFirewallStatus();
    }

    [RelayCommand]
    private void CheckFirewall()
    {
        RefreshFirewallStatus();
    }

    [RelayCommand]
    private void AddFirewallRule()
    {
        var (success, output) = FirewallRuleController.EnsureRule(Port);
        if (!success)
        {
            FirewallStatusMessage = $"Failed to add the firewall rule: {output}";
            FirewallNeedsAttention = true;
            return;
        }

        RefreshFirewallStatus();
    }

    private void RefreshFirewallStatus()
    {
        var status = FirewallRuleController.GetStatus(Port);

        (FirewallStatusMessage, FirewallNeedsAttention) = status switch
        {
            FirewallRuleStatus.Allowed => ($"Windows Firewall allows inbound TCP {Port} (OnIT-SMTP rule active).", false),
            FirewallRuleStatus.NotConfigured => ($"Windows Firewall has no rule allowing inbound TCP {Port} -- LAN clients outside this machine won't be able to connect. Click \"Add firewall rule\".", true),
            FirewallRuleStatus.WrongPort => ($"The OnIT-SMTP firewall rule is for a different port than {Port} (probably left over from a previous setting). Click \"Add firewall rule\" to fix it.", true),
            FirewallRuleStatus.DisabledOrBlocking => ($"The OnIT-SMTP firewall rule exists for port {Port} but is disabled or set to block. Click \"Add firewall rule\" to fix it.", true),
            _ => ("Could not determine the current Windows Firewall status.", true)
        };
    }
}
