package de.nwos.cardinal;

import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.PermissionDecision;
import de.nwos.contracts.PermissionScope;

import java.util.Set;

/**
 * Zentrale, verbindliche Berechtigungsprüfung (Architekturdokument
 * Abschnitt 9). Jede Nachricht zwischen Modulen MUSS diesen Manager
 * durchlaufen - der TaskRouter ruft ihn vor jeder Zustellung auf. Es gibt
 * bewusst keinen Umgehungspfad.
 *
 * <p>Port von NWOS.Cardinal.PermissionManager.</p>
 */
public final class PermissionManager {

    private static final System.Logger LOG = System.getLogger(PermissionManager.class.getName());

    /**
     * Feste Regel aus Abschnitt 9: Netzwerkzugriff über die enge Recherche-
     * Whitelist ja, allgemeiner Netzwerkzugriff nein - unabhängig davon, was
     * eine spätere Konfiguration versehentlich zuweisen würde. Bewusst hart
     * codiert, nicht konfigurierbar (Abschnitt 9, Marc-Regel).
     */
    private static final Set<String> MODULES_DENIED_GENERAL_NETWORK = Set.of("nwos.marc");

    private final ModuleRegistry registry;

    public PermissionManager(ModuleRegistry registry) {
        this.registry = registry;
    }

    public PermissionDecision evaluate(ModuleMessage message) {
        // Der vom Absender deklarierte Scope ist nur eine Untergrenze: der
        // tatsächlich nötige Scope ergibt sich zusätzlich aus dem MessageType
        // (MessageScopePolicy). Sonst könnte ein Absender einfach einen
        // leeren Scope angeben und die Prüfung umgehen.
        var effective = message.withAdditionalScope(MessageScopePolicy.minimumScopeFor(message.messageType()));
        var required = effective.requiredScope();
        var granted = registry.getGrantedScopes(effective.sourceModuleId());

        if (required.contains(PermissionScope.NETWORK_GENERAL)
                && MODULES_DENIED_GENERAL_NETWORK.contains(effective.sourceModuleId())) {
            var decision = PermissionDecision.deny(
                    "Modul '" + effective.sourceModuleId() + "' darf laut fester Sicherheitsregel keinen allgemeinen Netzwerkzugriff durchführen (siehe Architektur Abschnitt 9).");
            logDecision(effective, decision);
            return decision;
        }

        if (!granted.containsAll(required)) {
            var decision = PermissionDecision.deny(
                    "Modul '" + effective.sourceModuleId() + "' besitzt die Scopes " + required + " nicht (gewährt: " + granted + ").");
            logDecision(effective, decision);
            return decision;
        }

        // Systemändernde Aktionen sind grundsätzlich erlaubt (Scope vorhanden),
        // erfordern laut Abschnitt 8/25 aber zusätzlich eine Nutzerbestätigung.
        // TODO(VPT4): versionierte Liste "eindeutig sicher automatisierbarer"
        // Aktionen hinterlegen (Abschnitt 8) statt pauschaler Bestätigung.
        var requiresConfirmation = required.contains(PermissionScope.SYSTEM_CHANGE)
                || required.contains(PermissionScope.PROCESS_CONTROL);

        var allowed = PermissionDecision.allow("Scope vorhanden.", requiresConfirmation);
        logDecision(effective, allowed);
        return allowed;
    }

    private void logDecision(ModuleMessage message, PermissionDecision decision) {
        // Abschnitt 8: sicherheitsrelevante Entscheidungen müssen
        // nachvollziehbar protokolliert werden. Zunächst strukturiertes
        // Logging; TODO(VPT4): persistentes, für Alice/Nutzer einsehbares
        // Audit-Log.
        LOG.log(System.Logger.Level.INFO,
                "Permission {0} for {1} -> {2} ({3}, scope={4}): {5}",
                decision.allowed() ? "GRANTED" : "DENIED",
                message.sourceModuleId(), message.targetModuleId(),
                message.messageType(), message.requiredScope(), decision.reason());
    }
}
