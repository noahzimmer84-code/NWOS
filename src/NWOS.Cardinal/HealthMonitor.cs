using Microsoft.Extensions.Logging;
using NWOS.Contracts;

namespace NWOS.Cardinal;

/// <summary>
/// Überwacht alle registrierten Module per Heartbeat (Architekturdokument
/// Abschnitt 5 / 36: Fehler eines Moduls dürfen nicht das Gesamtsystem
/// mitreißen). Bewusst intervallbasiert statt eng getaktetes Polling, um dem
/// Performance-Grundsatz aus Abschnitt 35 zu genügen ("keine unnötigen
/// Polling-Schleifen") - das Intervall ist konfigurierbar und großzügig
/// bemessen.
/// </summary>
public sealed class HealthMonitor
{
    private readonly ModuleRegistry _registry;
    private readonly ILogger<HealthMonitor> _logger;
    private readonly TimeSpan _interval;

    // Wird pro Modul hochgezählt; nach Überschreiten des Schwellwerts greift
    // Recovery statt endlosem Neuversuch (verhindert Crash-Loops).
    private readonly Dictionary<string, int> _consecutiveFailures = new();
    private const int MaxConsecutiveFailuresBeforeRecovery = 3;

    public event Func<IModule, Task>? ModuleNeedsRecovery;

    public HealthMonitor(ModuleRegistry registry, ILogger<HealthMonitor> logger, TimeSpan? interval = null)
    {
        _registry = registry;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(30);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var module in _registry.AllModules)
            {
                await CheckOneModuleAsync(module, cancellationToken);
            }

            try
            {
                await Task.Delay(_interval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckOneModuleAsync(IModule module, CancellationToken cancellationToken)
    {
        ModuleHealth health;
        try
        {
            health = await module.CheckHealthAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Ein Modul, dessen Health-Check selbst wirft, gilt als nicht
            // reagierend - genau der Fall, den Abschnitt 36 adressiert.
            _logger.LogWarning(ex, "Health-Check für {Module} fehlgeschlagen.", module.ModuleId);
            health = new ModuleHealth(module.ModuleId, ModuleStatus.Unresponsive, ex.Message);
        }

        if (health.Status is ModuleStatus.Unresponsive)
        {
            var failures = _consecutiveFailures.GetValueOrDefault(module.ModuleId) + 1;
            _consecutiveFailures[module.ModuleId] = failures;

            _logger.LogWarning(
                "Modul {Module} nicht erreichbar ({Failures}/{Max}).",
                module.ModuleId, failures, MaxConsecutiveFailuresBeforeRecovery);

            if (failures >= MaxConsecutiveFailuresBeforeRecovery && ModuleNeedsRecovery is not null)
            {
                _logger.LogError(
                    "Modul {Module} überschreitet Fehlerschwelle - Recovery wird ausgelöst. Andere Module bleiben unberührt.",
                    module.ModuleId);
                await ModuleNeedsRecovery.Invoke(module);
                _consecutiveFailures[module.ModuleId] = 0;
            }
        }
        else
        {
            _consecutiveFailures[module.ModuleId] = 0;
        }
    }
}
