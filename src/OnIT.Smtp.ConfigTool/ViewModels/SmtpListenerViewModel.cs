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

    public SmtpListenerViewModel()
    {
        var settings = ConfigurationContext.Instance.Current.SmtpListener;
        _bindAddress = settings.BindAddress;
        _port = settings.Port;
        _maxMessageSizeMb = settings.MaxMessageSizeBytes / (1024 * 1024);
        _sessionTimeoutSeconds = settings.SessionTimeoutSeconds;
        _serverHostName = settings.ServerHostName;
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
    }
}
