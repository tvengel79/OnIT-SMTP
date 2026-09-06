namespace OnIT.Smtp.Core.Ipc;

public static class PipeNames
{
    /// <summary>Local named pipe the Windows Service listens on for the config tool to connect to.</summary>
    public const string ServiceControlPipe = "OnIT-SMTP.Control";
}
