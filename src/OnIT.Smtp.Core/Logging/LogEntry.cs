namespace OnIT.Smtp.Core.Logging;

/// <summary>Plain, serializable log record shared between the file log and the live named-pipe stream.</summary>
public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string Level { get; init; } = "Information";
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
}
