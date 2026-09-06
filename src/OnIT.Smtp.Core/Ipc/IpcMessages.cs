using OnIT.Smtp.Core.Logging;

namespace OnIT.Smtp.Core.Ipc;

/// <summary>
/// Multiplexing envelope for the newline-delimited-JSON protocol spoken over the control
/// named pipe. "Type" picks which payload shape "PayloadJson" holds -- see IpcMessageTypes.
/// </summary>
public sealed class IpcEnvelope
{
    public required string Type { get; init; }
    public string? PayloadJson { get; init; }

    /// <summary>Correlates a request with its response; unused (null) for server push messages like LogEntry.</summary>
    public string? CorrelationId { get; init; }
}

public static class IpcMessageTypes
{
    /// <summary>Server -> client push, payload = LogEntry, sent for every log event once connected.</summary>
    public const string LogEntry = "LogEntry";

    /// <summary>Server -> client push on connect, payload = LogEntry[] (recent ring-buffer history).</summary>
    public const string LogHistory = "LogHistory";

    public const string GetStatusRequest = "GetStatusRequest";
    public const string GetStatusResponse = "GetStatusResponse";

    public const string ReloadConfigRequest = "ReloadConfigRequest";
    public const string ReloadConfigResponse = "ReloadConfigResponse";

    public const string SendTestEmailRequest = "SendTestEmailRequest";
    public const string SendTestEmailResponse = "SendTestEmailResponse";
}

public sealed class ServiceStatus
{
    public bool Listening { get; init; }
    public string? BindAddress { get; init; }
    public int Port { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public long MessagesRelayed { get; init; }
    public long MessagesRejected { get; init; }
    public long ConnectionsRejectedByIpAllowList { get; init; }
    public bool EntraAppConfigured { get; init; }
    public bool AdminConsentGranted { get; init; }
}

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static OperationResult Ok() => new() { Success = true };
    public static OperationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public sealed class SendTestEmailRequestPayload
{
    public required string From { get; init; }
    public required string To { get; init; }
    public string Subject { get; init; } = "OnIT-SMTP test message";
    public string Body { get; init; } = "This is a test message sent from the OnIT-SMTP config tool.";
}
