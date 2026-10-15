using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NWOS.Contracts;
using NWOS.Cardinal;

namespace NWOS.Ipc;

public static class IpcDefaults
{
    public const string CardinalPipeName = "NWOS.Cardinal";
    public const string ExternalCallerIdentity = "NWOS.ExternalCaller";
}

[SupportedOSPlatform("windows")]
public class IpcServer
{
    private readonly IModuleMessenger? _messenger;
    private readonly TaskRouter? _taskRouter;
    private readonly string _pipeName;
    private readonly ILogger<IpcServer>? _logger;

    private IModuleMessenger ActiveMessenger => _taskRouter != null ? (IModuleMessenger)(object)_taskRouter : (_messenger ?? throw new InvalidOperationException("Kein Messenger konfiguriert."));

    public IpcServer(TaskRouter taskRouter, string pipeName, ILogger<IpcServer>? logger = null)
    {
        _taskRouter = taskRouter;
        _pipeName = pipeName;
        _logger = logger;
    }

    public IpcServer(IModuleMessenger messenger, string pipeName, ILogger<IpcServer>? logger = null)
    {
        _messenger = messenger;
        _pipeName = pipeName;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(cancellationToken);

                await HandleClientAsync(serverStream, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                serverStream?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler im IPC-Server-Loop.");
                serverStream?.Dispose();
                try { await Task.Delay(10, cancellationToken); } catch { }
            }
            finally
            {
                serverStream?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<ModuleMessage>(stream, cancellationToken: cancellationToken);
            if (request == null) return;

            var result = await ActiveMessenger.SendAsync(request, cancellationToken);

            await JsonSerializer.SerializeAsync(stream, result, cancellationToken: cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fehler beim Verarbeiten einer IPC-Client-Verbindung.");
        }
    }
}