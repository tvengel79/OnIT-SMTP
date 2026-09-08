using OnIT.Smtp.ConfigTool.Services;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public sealed class MainViewModel
{
    public EntraAppViewModel EntraApp { get; } = new();
    public AllowedSendersViewModel AllowedSenders { get; } = new();
    public IpAllowListViewModel IpAllowList { get; } = new();
    public SmtpListenerViewModel SmtpListener { get; } = new();
    public LoggingViewModel Logging { get; } = new();
    public TestEmailViewModel TestEmail { get; } = new();
    public ServiceStatusViewModel ServiceStatus { get; } = new();
    public DockerExportViewModel DockerExport { get; } = new();
    public RemoteBridgeViewModel RemoteBridge { get; } = new();
    public AboutViewModel About { get; } = new();

    public MainViewModel()
    {
        PipeClientService.Instance.Start();
    }
}
