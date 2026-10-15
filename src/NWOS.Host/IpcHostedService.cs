using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NWOS.Cardinal;
using NWOS.Ipc;

namespace NWOS.Host;

/// <summary>
/// Startet <see cref="IpcServer"/> als Hintergrunddienst, sobald
/// Cardinal bereit ist (Abschnitt 36).
/// </summary>
public sealed class IpcHostedService : BackgroundService
{
    private readonly TaskRouter _taskRouter;
    private readonly ILogger<IpcHostedService> _logger;

    public IpcHostedService(TaskRouter taskRouter, ILogger<IpcHostedService> logger)
    {
        _taskRouter = taskRouter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var server = new IpcServer(_taskRouter, IpcDefaults.CardinalPipeName, _logger as ILogger<IpcServer>);

        try
        {
            _logger.LogInformation("IPC-Server gestartet (Pipe '{PipeName}').", IpcDefaults.CardinalPipeName);

            // Server im Hintergrund laufen lassen
            await server.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal beim Beenden des Dienstes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPC-Server unerwartet beendet - andere NWOS-Funktionen laufen unbeeinträchtigt weiter (Abschnitt 36).");
        }
    }
}