package de.nwos.cardinal;

import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.PermissionDecision;
import de.nwos.contracts.PermissionScope;
import org.junit.jupiter.api.Test;

import java.util.EnumSet;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class PermissionManagerTest {

    private final ModuleRegistry registry = new ModuleRegistry();
    private final PermissionManager permissions = new PermissionManager(registry);

    @Test
    void deniedWhenScopeNotGranted() {
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.FILE_SYSTEM_READ));

        var decision = permissions.evaluate(message("nwos.sender", "test.write", Set.of(PermissionScope.FILE_SYSTEM_WRITE)));

        assertFalse(decision.allowed());
        assertTrue(decision.reason().contains("besitzt"));
    }

    @Test
    void allowedWhenScopeGranted() {
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.FILE_SYSTEM_READ));

        var decision = permissions.evaluate(message("nwos.sender", "test.read", Set.of(PermissionScope.FILE_SYSTEM_READ)));

        assertTrue(decision.allowed());
        assertFalse(decision.requiresUserConfirmation());
    }

    @Test
    void processControlRequiresUserConfirmation() {
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.PROCESS_CONTROL));

        var decision = permissions.evaluate(message("nwos.sender", "test.kill", Set.of(PermissionScope.PROCESS_CONTROL)));

        assertTrue(decision.allowed());
        assertTrue(decision.requiresUserConfirmation());
    }

    @Test
    void marcIsDeniedGeneralNetworkEvenWhenGranted() {
        // Feste Sicherheitsregel (Abschnitt 9): Marc bekommt allgemeinen
        // Netzwerkzugriff nicht - selbst wenn eine Konfiguration ihn fälschlich vergibt.
        registry.grantExternalScope("nwos.marc", EnumSet.allOf(PermissionScope.class));

        var decision = permissions.evaluate(message("nwos.marc", "test.web", Set.of(PermissionScope.NETWORK_GENERAL)));

        assertFalse(decision.allowed());
        assertTrue(decision.reason().contains("fester Sicherheitsregel"));
    }

    @Test
    void declaredScopeIsOnlyALowerBound() {
        // Absender deklariert leeren Scope, aber der MessageType erfordert
        // PROCESS_CONTROL (marc.launchApp) - die Prüfung darf sich nicht
        // überlisten lassen.
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.FILE_SYSTEM_READ));

        var decision = permissions.evaluate(message("nwos.sender", "marc.launchApp", Set.of()));

        assertFalse(decision.allowed());
    }

    @Test
    void unknownSenderHasNoScopes() {
        var decision = permissions.evaluate(message("nwos.unbekannt", "test.read", Set.of(PermissionScope.FILE_SYSTEM_READ)));

        assertFalse(decision.allowed());
    }

    private ModuleMessage message(String source, String messageType, Set<PermissionScope> scope) {
        return ModuleMessage.create(source, "nwos.ziel", messageType, "{}", scope);
    }
}
