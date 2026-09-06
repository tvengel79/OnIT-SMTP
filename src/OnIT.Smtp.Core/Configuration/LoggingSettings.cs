namespace OnIT.Smtp.Core.Configuration;

public enum LogVerbosity
{
    /// <summary>High-level events only: service start/stop, connections accepted/rejected, messages sent/failed.</summary>
    Informational,

    /// <summary>Everything Informational includes plus full SMTP protocol chatter and Graph request/response detail.</summary>
    Detailed
}

public sealed class LoggingSettings
{
    public LogVerbosity Verbosity { get; set; } = LogVerbosity.Informational;

    /// <summary>Directory the rolling log files are written to.</summary>
    public string LogDirectory { get; set; } = string.Empty;

    /// <summary>How many days of rolled log files to retain.</summary>
    public int RetainedDays { get; set; } = 14;

    /// <summary>Whether the config tool should stream live log events over the named pipe while open.</summary>
    public bool EnableLiveViewer { get; set; } = true;
}
