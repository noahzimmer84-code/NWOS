using System.IO.Pipes;
using NWOS.Contracts;

namespace NWOS.Ipc;

/// <summary>
/// Client-Gegenstück zu <see cref="IpcServer"/>. Implementiert
/// <see cref="IModuleMessenger"/> - ein externer Prozess (z. B. eine
/// künftige Version von NWOS.DesktopShell.Ui) kann diese Klasse genauso
/// verwenden wie ein In-Process-Modul den von Cardinal injizierten
/// Messenger: <c>await client.SendAsync(message, ct)</c>, ohne wissen zu
/// müssen, dass darunter eine Named Pipe steckt.
///
/// Verbindet sich pro Aufruf neu (siehe Begründung in <see cref="IpcServer"/>) -
/// einfach, robust gegenüber zwischenzeitlich neu gestartetem Cardinal-
/// Prozess, aber nicht die effizienteste Variante für sehr viele Anfragen
/// kurz hintereinander (TODO, siehe IpcServer).
/// </summary>
public sealed class IpcClient : IModuleMessenger
{
    private readonly string _serverName;
    private readonly string _pipeName;
    private readonly TimeSpan _connectTimeout;

    public IpcClient(string? serverName = null, string? pipeName = null, TimeSpan? connectTimeout = null)
    {
        _serverName = serverName ?? ".";
        _pipeName = pipeName ?? IpcDefaults.CardinalPipeName;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<ModuleMessageResult> SendAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        await using var pipeClient = new NamedPipeClientStream(
            _serverName, _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipeClient.ConnectAsync((int)_connectTimeout.TotalMilliseconds, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new ModuleMessageResult(
                ModuleMessageOutcome.ModuleFailure, null,
                $"Konnte innerhalb von {_connectTimeout.TotalSeconds}s keine Verbindung zu Cardinal (Pipe '{_pipeName}') herstellen - läuft der NWOS-Prozess?");
        }

        await PipeFraming.WriteJsonAsync(pipeClient, message, cancellationToken);
        return await PipeFraming.ReadJsonAsync<ModuleMessageResult>(pipeClient, cancellationToken);
    }
}
