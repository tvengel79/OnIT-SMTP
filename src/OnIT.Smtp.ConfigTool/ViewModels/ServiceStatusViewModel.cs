using System.IO;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Ipc;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class ServiceStatusViewModel : ObservableObject
{
    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private string _windowsServiceStatus = "Unknown";
    [ObservableProperty] private bool _isConnectedToControlPipe;
    [ObservableProperty] private ServiceStatus? _liveStatus;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    private readonly DispatcherTimer _refreshTimer;

    public ServiceStatusViewModel()
    {
        RefreshInstallState();

        PipeClientService.Instance.ConnectionStateChanged += connected =>
            Application.Current.Dispatcher.BeginInvoke(() => IsConnectedToControlPipe = connected);
        IsConnectedToControlPipe = PipeClientService.Instance.IsConnected;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await RefreshLiveStatusAsync();
        _refreshTimer.Start();
    }

    private void RefreshInstallState()
    {
        IsInstalled = WindowsServiceController.IsInstalled();
        var status = WindowsServiceController.GetStatus();
        WindowsServiceStatus = status?.ToString() ?? "Not installed";
    }

    private async Task RefreshLiveStatusAsync()
    {
        if (!PipeClientService.Instance.IsConnected) return;
        LiveStatus = await PipeClientService.Instance.GetStatusAsync(TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    private void InstallService()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "..", "OnIT.Smtp.Service", "OnIT.Smtp.Service.exe");
        exePath = Path.GetFullPath(exePath);

        if (!File.Exists(exePath))
        {
            StatusMessage = $"Could not find the service executable at '{exePath}'. Deploy the service first, then retry.";
            return;
        }

        var (success, output) = WindowsServiceController.Install(exePath);
        StatusMessage = success ? "Service installed." : $"Install failed: {output}";
        RefreshInstallState();
    }

    [RelayCommand]
    private void UninstallService()
    {
        if (MessageBox.Show("Uninstall the OnIT-SMTP Windows Service?", "Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var (success, output) = WindowsServiceController.Uninstall();
        StatusMessage = success ? "Service uninstalled." : $"Uninstall failed: {output}";
        RefreshInstallState();
    }

    [RelayCommand]
    private void StartService()
    {
        try
        {
            WindowsServiceController.Start();
            StatusMessage = "Service started.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start failed: {ex.Message}";
        }
        RefreshInstallState();
    }

    [RelayCommand]
    private void StopService()
    {
        try
        {
            WindowsServiceController.Stop();
            StatusMessage = "Service stopped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop failed: {ex.Message}";
        }
        RefreshInstallState();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        RefreshInstallState();
        await RefreshLiveStatusAsync();
    }
}
