package de.nwos.cardinal;

import de.nwos.contracts.IModule;
import de.nwos.contracts.IModuleMessenger;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleMessageOutcome;
import de.nwos.contracts.ModuleMessageResult;

import java.util.concurrent.CompletableFuture;

/**
 * Der eigentliche Cardinal-Kern (Architekturdokument Abschnitt 5: "Cardinal
 * ist das Herz von NWOS"). Für den kleinsten stabilen Kern (Abschnitt 13)
 * übernimmt diese Klasse ausschließlich: Module starten, Health-Monitoring
 * laufen lassen, einfache Recovery (Neustart) auslösen, sauber
 * herunterfahren.
 *
 * <p>Bewusst NICHT Teil dieses Kerns: KI-Logik (Alya/Marc/Alice-Fachlogik),
 * Optimierungsentscheidungen, Update-Orchestrierung. Diese kommen als eigene
 * Module hinzu, die sich über die ModuleRegistry registrieren - der Kern
 * selbst muss dafür nicht verändert werden (Modularitätsprinzip,
 * Abschnitt 32).</p>
 *
 * <p>Port von NWOS.Cardinal.CardinalService (ohne Microsoft.Extensions.Hosting).</p>
 */
public final class CardinalService implements IModuleMessenger {

    private static final System.Logger LOG = System.getLogger(CardinalService.class.getName());

    private final ModuleRegistry registry;
    private final TaskRouter router;
    private final HealthMonitor healthMonitor;

    public CardinalService(ModuleRegistry registry, TaskRouter router, HealthMonitor healthMonitor) {
        this.registry = registry;
        this.router = router;
        this.healthMonitor = healthMonitor;
        this.healthMonitor.setRecoveryHandler(this::recoverModule);
    }

    /** Startet alle registrierten Module und danach das Health-Monitoring. */
    public void start() {
        LOG.log(System.Logger.Level.INFO, "Cardinal startet. Aktive Module: {0}", registry.allModules().size());

        for (IModule module : registry.allModules()) {
            try {
                module.start().join();
                LOG.log(System.Logger.Level.INFO, "Modul {0} gestartet.", module.getModuleId());
            } catch (Exception ex) {
                // Ein Modul, das schon beim Start scheitert, darf Cardinal
                // nicht zu Fall bringen (Abschnitt 36).
                LOG.log(System.Logger.Level.ERROR,
                        "Modul {0} konnte nicht gestartet werden - wird als inaktiv behandelt: {1}",
                        module.getModuleId(), ex.getMessage());
            }
        }

        healthMonitor.start();
    }

    /** Beendet alle Module kontrolliert (Abschnitt 37). */
    public void shutdown() {
        healthMonitor.stop();
        for (IModule module : registry.allModules()) {
            try {
                module.stop().join();
            } catch (Exception ex) {
                LOG.log(System.Logger.Level.WARNING,
                        "Fehler beim Stoppen von {0} (wird ignoriert, Shutdown geht weiter): {1}",
                        module.getModuleId(), ex.getMessage());
            }
        }
        LOG.log(System.Logger.Level.INFO, "Cardinal beendet.");
    }

    /**
     * Implementiert IModuleMessenger, damit Module selbst Nachrichten anstoßen
     * können - läuft intern über denselben TaskRouter und damit dieselbe
     * Berechtigungsprüfung wie jede andere Nachricht (Abschnitt 9/31).
     */
    @Override
    public CompletableFuture<ModuleMessageResult> send(ModuleMessage message) {
        return submit(message).thenApply(result -> new ModuleMessageResult(
                toOutcome(result.outcome()), result.response(), result.reason()));
    }

    /** Öffentlich, damit der Host/IPC-Server Nachrichten einreichen kann. */
    public CompletableFuture<RoutingResult> submit(ModuleMessage message) {
        return router.route(message);
    }

    private static ModuleMessageOutcome toOutcome(RoutingOutcome outcome) {
        return switch (outcome) {
            case DELIVERED -> ModuleMessageOutcome.DELIVERED;
            case DENIED -> ModuleMessageOutcome.DENIED;
            case PENDING_USER_CONFIRMATION -> ModuleMessageOutcome.PENDING_USER_CONFIRMATION;
            case MODULE_FAILURE -> ModuleMessageOutcome.MODULE_FAILURE;
        };
    }

    /**
     * Kleinster stabiler Kern: Recovery = kontrollierter Neustart des
     * einzelnen Moduls. Komplexere Strategien (z. B. Rollback auf letzte
     * stabile Version) gehören zum Update-System (Abschnitt 37).
     */
    private CompletableFuture<Void> recoverModule(IModule module) {
        LOG.log(System.Logger.Level.WARNING, "Starte Recovery für Modul {0}.", module.getModuleId());

        var stop = module.stop()
                .exceptionally(ex -> {
                    LOG.log(System.Logger.Level.WARNING,
                            "Fehler beim Stoppen von {0} während Recovery (Neustart folgt trotzdem): {1}",
                            module.getModuleId(), ex.getMessage());
                    return null;
                });

        return stop.thenCompose(ignored -> module.start())
                .thenRun(() -> LOG.log(System.Logger.Level.INFO,
                        "Modul {0} nach Recovery neu gestartet.", module.getModuleId()))
                .exceptionally(ex -> {
                    LOG.log(System.Logger.Level.ERROR,
                            "Recovery für {0} fehlgeschlagen - Modul bleibt inaktiv, restliches System läuft weiter.",
                            module.getModuleId());
                    return null;
                });
    }
}
