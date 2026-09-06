using System.Windows;
using OnIT.Smtp.ConfigTool.Services;

namespace OnIT.Smtp.ConfigTool;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "OnIT-SMTP.ConfigTool.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("OnIT-SMTP Configuration is already running.", "OnIT-SMTP", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ConfigurationContext.Instance.Load();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PipeClientService.Instance.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
