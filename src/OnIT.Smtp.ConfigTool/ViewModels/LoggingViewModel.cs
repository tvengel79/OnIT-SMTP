using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Logging;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class LoggingViewModel : ObservableObject
{
    private const int MaxDisplayedEntries = 2000;

    [ObservableProperty] private LogVerbosity _verbosity;
    [ObservableProperty] private string _logDirectory;
    [ObservableProperty] private int _retainedDays;
    [ObservableProperty] private bool _enableLiveViewer;
    [ObservableProperty] private bool _isConnectedToService;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public IReadOnlyList<LogVerbosity> VerbosityLevels { get; } = Enum.GetValues<LogVerbosity>();
    public ObservableCollection<LogEntry> LiveEntries { get; } = new();

    public LoggingViewModel()
    {
        var settings = ConfigurationContext.Instance.Current.Logging;
        _verbosity = settings.Verbosity;
        _logDirectory = string.IsNullOrWhiteSpace(settings.LogDirectory) ? ConfigPaths.DefaultLogDirectory : settings.LogDirectory;
        _retainedDays = settings.RetainedDays;
        _enableLiveViewer = settings.EnableLiveViewer;

        IsConnectedToService = PipeClientService.Instance.IsConnected;
        PipeClientService.Instance.ConnectionStateChanged += connected =>
            Application.Current.Dispatcher.BeginInvoke(() => IsConnectedToService = connected);
        PipeClientService.Instance.LogEntryReceived += entry =>
            Application.Current.Dispatcher.BeginInvoke(() => AppendEntry(entry));
        PipeClientService.Instance.LogHistoryReceived += history =>
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LiveEntries.Clear();
                foreach (var entry in history) AppendEntry(entry);
            });
    }

    private void AppendEntry(LogEntry entry)
    {
        LiveEntries.Add(entry);
        while (LiveEntries.Count > MaxDisplayedEntries) LiveEntries.RemoveAt(0);
    }

    [RelayCommand]
    private void BrowseLogDirectory()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = LogDirectory };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            LogDirectory = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void OpenLogDirectory()
    {
        if (Directory.Exists(LogDirectory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogDirectory) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = ConfigurationContext.Instance.Current.Logging;
        settings.Verbosity = Verbosity;
        settings.LogDirectory = LogDirectory;
        settings.RetainedDays = RetainedDays;
        settings.EnableLiveViewer = EnableLiveViewer;
        ConfigurationContext.Instance.Save();

        if (PipeClientService.Instance.IsConnected)
        {
            var result = await PipeClientService.Instance.ReloadConfigAsync();
            StatusMessage = result?.Success == true ? "Saved and applied." : "Saved. Restart the service to fully apply logging changes.";
        }
        else
        {
            StatusMessage = "Saved. It will take effect the next time the service starts.";
        }
    }

    [RelayCommand]
    private void ClearLiveView()
    {
        LiveEntries.Clear();
    }
}
