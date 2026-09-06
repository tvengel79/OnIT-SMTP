using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using OnIT.Smtp.Core.Ipc;
using OnIT.Smtp.Core.Logging;

namespace OnIT.Smtp.Service.Ipc;

/// <summary>
/// Hosts the local named pipe the config tool connects to for live log streaming and
/// on-demand commands (status, config reload, send-a-test-email-through-the-service).
/// Any local Administrator can connect; the pipe grants no remote access by design.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ControlPipeHost : IAsyncDisposable
{
    private readonly LiveLogSink _liveLogSink;
    private readonly Func<ServiceStatus> _getStatus;
    private readonly Func<CancellationToken, Task<OperationResult>> _reloadConfig;
    private readonly Func<SendTestEmailRequestPayload, CancellationToken, Task<OperationResult>> _sendTestEmail;
    private readonly ILogger<ControlPipeHost> _logger;

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;

    public ControlPipeHost(
        LiveLogSink liveLogSink,
        Func<ServiceStatus> getStatus,
        Func<CancellationToken, Task<OperationResult>> reloadConfig,
        Func<SendTestEmailRequestPayload, CancellationToken, Task<OperationResult>> sendTestEmail,
        ILogger<ControlPipeHost> logger)
    {
        _liveLogSink = liveLogSink;
        _getStatus = getStatus;
        _reloadConfig = reloadConfig;
        _sendTestEmail = sendTestEmail;
        _logger = logger;
    }

    public void Start(CancellationToken serviceLifetimeToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(serviceLifetimeToken);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = CreatePipeInstance();
                await pipe.WaitForConnectionAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept a control pipe connection.");
                continue;
            }

            _ = HandleClientAsync(pipe, ct);
        }
    }

    private static NamedPipeServerStream CreatePipeInstance()
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().User!,
            PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeNames.ServiceControlPipe,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity);
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using var connection = new NdjsonPipeConnection(pipe);
        IDisposable? subscription = null;

        try
        {
            await connection.SendAsync(IpcMessageTypes.LogHistory, _liveLogSink.GetRecentHistory(), ct: ct);

            subscription = _liveLogSink.Subscribe(entry =>
            {
                _ = connection.SendAsync(IpcMessageTypes.LogEntry, entry, ct: CancellationToken.None);
            });

            while (!ct.IsCancellationRequested)
            {
                var envelope = await connection.ReceiveAsync(ct);
                if (envelope is null) break;

                await DispatchAsync(connection, envelope, ct);
            }
        }
        catch (IOException)
        {
            // Client disconnected -- normal when the config tool is closed.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Control pipe client session ended with an error.");
        }
        finally
        {
            subscription?.Dispose();
            pipe.Dispose();
        }
    }

    private async Task DispatchAsync(NdjsonPipeConnection connection, IpcEnvelope request, CancellationToken ct)
    {
        switch (request.Type)
        {
            case IpcMessageTypes.GetStatusRequest:
                await connection.SendAsync(IpcMessageTypes.GetStatusResponse, _getStatus(), request.CorrelationId, ct);
                break;

            case IpcMessageTypes.ReloadConfigRequest:
                var reloadResult = await _reloadConfig(ct);
                await connection.SendAsync(IpcMessageTypes.ReloadConfigResponse, reloadResult, request.CorrelationId, ct);
                break;

            case IpcMessageTypes.SendTestEmailRequest:
                var payload = NdjsonPipeConnection.DeserializePayload<SendTestEmailRequestPayload>(request);
                var sendResult = payload is null
                    ? OperationResult.Fail("Malformed test-email request.")
                    : await _sendTestEmail(payload, ct);
                await connection.SendAsync(IpcMessageTypes.SendTestEmailResponse, sendResult, request.CorrelationId, ct);
                break;

            default:
                _logger.LogWarning("Control pipe received an unknown request type '{Type}'.", request.Type);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        if (_acceptLoopTask is not null)
        {
            try { await _acceptLoopTask; } catch { /* already logged */ }
        }
        _cts.Dispose();
    }
}
