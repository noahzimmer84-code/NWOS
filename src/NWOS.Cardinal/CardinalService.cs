using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NWOS.Contracts;

namespace NWOS.Cardinal;

/// <summary>
/// Der eigentliche Cardinal-Prozess (Architekturdokument Abschnitt 5: "Cardinal
/// ist das Herz von NWOS"). Für den kleinsten stabilen Kern (Abschnitt 13)
/// übernimmt diese Klasse ausschließlich: Module starten, Health-Monitoring
/// laufen lassen, einfache Recovery (Neustart) auslösen, sauber herunterfahren.
///
/// Bewusst NICHT Teil dieses Kerns: KI-Logik (Alya/Marc/Alice-Fachlogik),
/// Optimierungsentscheidungen (Abschnitt 7 des Auftrags), Update-Orchestrierung.
/// Diese kommen in späteren Phasen als eigene Module hinzu, die sich über
/// <see cref="ModuleRegistry"/> registrieren - der Kern selbst muss dafür nicht
/// verändert werden (Modularitätsprinzip, Abschnitt 32).
/// </summary>
public sealed class CardinalService : BackgroundService, IModuleMessenger
{
    private readonly ModuleRegistry _registry;
    private readonly TaskRouter _router;
    private readonly HealthMonitor _healthMonitor;
    private readonly ILogger<CardinalService> _logger;

    public CardinalService(
        ModuleRegistry registry,
        TaskRouter router,
        HealthMonitor healthMonitor,
        ILogger<CardinalService> logger)
    {
        _registry = registry;
        _router = router;
        _healthMonitor = healthMonitor;
        _logger = logger;

        _healthMonitor.ModuleNeedsRecovery += HandleModuleRecoveryAsync;
    }

    /// <summary>Öffentlich, damit NWOS.Host Nachrichten einreichen kann (z. B. von der UI).</summary>
    public Task<RoutingResult> SubmitAsync(ModuleMessage message, CancellationToken cancellationToken) =>
        _router.RouteAsync(message, cancellationToken);

    /// <summary>
    /// Implementiert <see cref="IModuleMessenger"/>, damit Module (z. B. Alya)
    /// selbst Nachrichten anstoßen können - läuft intern über denselben
    /// <see cref="TaskRouter"/> und damit dieselbe Berechtigungsprüfung wie
    /// jede andere Nachricht (Abschnitt 9/31).
    /// </summary>
    async Task<ModuleMessageResult> IModuleMessenger.SendAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        var result = await SubmitAsync(message, cancellationToken);
        var outcome = result.Outcome switch
        {
            RoutingOutcome.Delivered => ModuleMessageOutcome.Delivered,
            RoutingOutcome.Denied => ModuleMessageOutcome.Denied,
            RoutingOutcome.PendingUserConfirmation => ModuleMessageOutcome.PendingUserConfirmation,
            RoutingOutcome.ModuleFailure => ModuleMessageOutcome.ModuleFailure,
            _ => throw new NotSupportedException($"Unbekanntes RoutingOutcome '{result.Outcome}'."),
        };

        return new ModuleMessageResult(outcome, result.Response, result.Reason);
    }

    /// <summary>Registrierung ist Aufgabe von NWOS.Host anhand der Nutzerkonfiguration (Abschnitt 4).</summary>
    public void RegisterModule(IModule module, PermissionScope grantedScopes) =>
        _registry.Register(module, grantedScopes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cardinal startet. Aktive Module: {Count}", _registry.AllModules.Count);

        foreach (var module in _registry.AllModules)
        {
            try
            {
                await module.StartAsync(stoppingToken);
                _logger.LogInformation("Modul {Module} gestartet.", module.ModuleId);
            }
            catch (Exception ex)
            {
                // Ein Modul, das schon beim Start scheitert, darf Cardinal nicht
                // zu Fall bringen (Abschnitt 36).
                _logger.LogError(ex, "Modul {Module} konnte nicht gestartet werden - wird als inaktiv behandelt.", module.ModuleId);
            }
        }

        try
        {
            await _healthMonitor.RunAsync(stoppingToken);
        }
        finally
        {
            await ShutdownAllModulesAsync();
        }
    }

    private async Task HandleModuleRecoveryAsync(IModule module)
    {
        // Kleinster stabiler Kern: Recovery = kontrollierter Neustart des
        // einzelnen Moduls. Komplexere Strategien (z. B. Rollback auf letzte
        // stabile Version) gehören zum Update-System (Abschnitt 37) und werden
        // hier bewusst nicht vorgezogen.
        _logger.LogWarning("Starte Recovery für Modul {Module}.", module.ModuleId);
        try
        {
            await module.StopAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Stoppen von {Module} während Recovery (wird ignoriert, Neustart folgt trotzdem).", module.ModuleId);
        }

        try
        {
            await module.StartAsync(CancellationToken.None);
            _logger.LogInformation("Modul {Module} nach Recovery neu gestartet.", module.ModuleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery für {Module} fehlgeschlagen - Modul bleibt inaktiv, restliches System läuft weiter.", module.ModuleId);
        }
    }

    private async Task ShutdownAllModulesAsync()
    {
        foreach (var module in _registry.AllModules)
        {
            try
            {
                await module.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Stoppen von {Module} (wird ignoriert, Shutdown geht weiter).", module.ModuleId);
            }
        }

        _logger.LogInformation("Cardinal beendet.");
    }
}
