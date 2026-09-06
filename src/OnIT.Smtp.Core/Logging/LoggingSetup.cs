using OnIT.Smtp.Core.Configuration;
using Serilog;
using Serilog.Events;

namespace OnIT.Smtp.Core.Logging;

public static class LoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Builds the Serilog logger used by the Windows Service: rolling daily file, plus an
    /// in-process live sink the named-pipe IPC host reads from when the config tool is open.
    /// </summary>
    public static Serilog.Core.Logger CreateLogger(LoggingSettings settings, LiveLogSink liveSink)
    {
        var logDirectory = string.IsNullOrWhiteSpace(settings.LogDirectory) ? ConfigPaths.DefaultLogDirectory : settings.LogDirectory;
        Directory.CreateDirectory(logDirectory);

        var minimumLevel = settings.Verbosity == LogVerbosity.Detailed ? LogEventLevel.Debug : LogEventLevel.Information;

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logDirectory, "onit-smtp-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: settings.RetainedDays,
                outputTemplate: OutputTemplate,
                shared: true);

        if (settings.EnableLiveViewer)
        {
            config = config.WriteTo.Sink(liveSink);
        }

        return config.CreateLogger();
    }
}
