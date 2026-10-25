package de.nwos.cardinal;

import de.nwos.contracts.ModuleMessage;

import java.util.concurrent.CompletableFuture;

/**
 * Vermittelt jede Nachricht zwischen Modulen (Architekturdokument Abschnitt 7:
 * "Kein Modul darf ein anderes Modul direkt durchgreifen - jede Nachricht
 * läuft über Cardinal"). Der Router selbst enthält keine Fachlogik der Module,
 * sondern nur: Berechtigung prüfen -> zustellen -> Ergebnis zurückgeben.
 *
 * <p>Port von NWOS.Cardinal.TaskRouter.</p>
 */
public final class TaskRouter {

    private static final System.Logger LOG = System.getLogger(TaskRouter.class.getName());

    private final ModuleRegistry registry;
    private final PermissionManager permissions;

    public TaskRouter(ModuleRegistry registry, PermissionManager permissions) {
        this.registry = registry;
        this.permissions = permissions;
    }

    /**
     * Sendet eine Nachricht an das Zielmodul, sofern die
     * Berechtigungsprüfung dies erlaubt.
     */
    public CompletableFuture<RoutingResult> route(ModuleMessage message) {
        var decision = permissions.evaluate(message);
        if (!decision.allowed()) {
            return CompletableFuture.completedFuture(RoutingResult.denied(decision.reason()));
        }

        if (decision.requiresUserConfirmation()) {
            // TODO(VPT3/4): echte Nutzerabfrage einhängen (z. B. über Alya /
            // Overlay-Benachrichtigung). Bis dahin wird die Aktion bewusst
            // NICHT automatisch durchgewunken, sondern als "wartend"
            // zurückgegeben - sichere Grundeinstellung (Abschnitt 41:
            // "Sicherheit vor Komfort").
            LOG.log(System.Logger.Level.INFO,
                    "Nachricht {0} von {1} an {2} wartet auf Nutzerbestätigung.",
                    message.messageId(), message.sourceModuleId(), message.targetModuleId());
            return CompletableFuture.completedFuture(RoutingResult.pendingUserConfirmation());
        }

        var targetModule = registry.tryGet(message.targetModuleId());
        if (targetModule == null) {
            LOG.log(System.Logger.Level.WARNING,
                    "Zielmodul {0} für Nachricht {1} ist nicht registriert/aktiv.",
                    message.targetModuleId(), message.messageId());
            return CompletableFuture.completedFuture(
                    RoutingResult.denied("Zielmodul '" + message.targetModuleId() + "' ist nicht aktiv."));
        }

        return targetModule.handleMessage(message)
                .thenApply(RoutingResult::delivered)
                .exceptionally(ex -> {
                    // Abschnitt 36: ein abstürzendes Modul darf nicht Cardinal
                    // mitreißen - der Fehler wird isoliert zurückgemeldet.
                    var reason = rootMessage(ex);
                    LOG.log(System.Logger.Level.ERROR,
                            "Modul {0} hat Nachricht {1} nicht verarbeitet - wird isoliert behandelt: {2}",
                            message.targetModuleId(), message.messageId(), reason);
                    return RoutingResult.moduleFailure(reason);
                });
    }

    private static String rootMessage(Throwable throwable) {
        var current = throwable;
        while (current.getCause() != null && current.getCause() != current) {
            current = current.getCause();
        }
        return current.getMessage() == null ? current.getClass().getSimpleName() : current.getMessage();
    }
}
