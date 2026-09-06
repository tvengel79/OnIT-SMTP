using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace OnIT.Smtp.ConfigTool.Services;

/// <summary>
/// Controls the "OnIT-SMTP" Windows Service. Start/stop go through ServiceController;
/// install/uninstall shell out to sc.exe since ServiceController itself has no create/delete
/// API. All calls require the config tool's own elevation (see app.manifest).
/// </summary>
public static class WindowsServiceController
{
    public const string ServiceName = "OnIT-SMTP";

    public static bool IsInstalled()
    {
        return ServiceController.GetServices().Any(s => string.Equals(s.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase));
    }

    public static ServiceControllerStatus? GetStatus()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            return sc.Status;
        }
        catch (InvalidOperationException)
        {
            return null; // Not installed.
        }
    }

    public static void Start()
    {
        using var sc = new ServiceController(ServiceName);
        if (sc.Status != ServiceControllerStatus.Running)
        {
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        }
    }

    public static void Stop()
    {
        using var sc = new ServiceController(ServiceName);
        if (sc.Status != ServiceControllerStatus.Stopped)
        {
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
    }

    public static (bool Success, string Output) Install(string serviceExecutablePath)
    {
        var binPath = $"\"{serviceExecutablePath}\"";
        var create = RunSc($"create {ServiceName} binPath= {binPath} start= auto DisplayName= \"OnIT-SMTP Bridge\"");
        if (!create.Success) return create;

        var description = RunSc($"description {ServiceName} \"Relays LAN SMTP mail through Microsoft Graph.\"");
        return description.Success ? create : description;
    }

    public static (bool Success, string Output) Uninstall()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
        }
        catch (InvalidOperationException)
        {
            // Not installed / already stopped -- proceed to delete anyway.
        }

        return RunSc($"delete {ServiceName}");
    }

    private static (bool Success, string Output) RunSc(string arguments)
    {
        var psi = new ProcessStartInfo("sc.exe", arguments)
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
