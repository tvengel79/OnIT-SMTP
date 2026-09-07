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
    /// The Part 2 Docker/Linux bridge has no such IPC channel to view logs remotely, so it
    /// passes <paramref name="includeConsole"/> to also write to stdout for `docker logs`.
    /// </summary>
    public static Serilog.Core.Logger CreateLogger(LoggingSettings settings, LiveLogSink liveSink, bool includeConsole = false)
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

        if (includeConsole)
        {
            config = config.WriteTo.Console(outputTemplate: OutputTemplate);
        }

        return config.CreateLogger();
    }
}
