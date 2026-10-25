package de.nwos.cardinal;

import de.nwos.contracts.IModule;
import de.nwos.contracts.ModuleHealth;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleMessageOutcome;
import de.nwos.contracts.PermissionScope;
import org.junit.jupiter.api.Test;

import java.util.Set;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class TaskRouterTest {

    private final ModuleRegistry registry = new ModuleRegistry();
    private final TaskRouter router = new TaskRouter(registry, new PermissionManager(registry));

    @Test
    void deliversToRegisteredModule() {
        registry.register(new EchoModule("nwos.echo"), Set.of(PermissionScope.FILE_SYSTEM_READ));
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.FILE_SYSTEM_READ));

        var message = ModuleMessage.create("nwos.sender", "nwos.echo", "test.echo", "{\"x\":1}", Set.of());
        var result = router.route(message).join();

        assertEquals(RoutingOutcome.DELIVERED, result.outcome());
        assertEquals("{\"x\":1}", result.response().payloadJson());
    }

    @Test
    void deniedWhenTargetNotRegistered() {
        registry.grantExternalScope("nwos.sender", Set.of());

        var message = ModuleMessage.create("nwos.sender", "nwos.gibtsnicht", "test.x", "{}", Set.of());
        var result = router.route(message).join();

        assertEquals(RoutingOutcome.DENIED, result.outcome());
        assertTrue(result.reason().contains("nicht aktiv"));
    }

    @Test
    void moduleFailureIsIsolated() {
        // Abschnitt 36: Ein abstürzendes Modul darf Cardinal nicht mitreißen.
        registry.register(new CrashingModule("nwos.crash"), Set.of(PermissionScope.FILE_SYSTEM_READ));
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.FILE_SYSTEM_READ));

        var message = ModuleMessage.create("nwos.sender", "nwos.crash", "test.boom", "{}", Set.of());
        var result = router.route(message).join();

        assertEquals(RoutingOutcome.MODULE_FAILURE, result.outcome());
        assertTrue(result.reason().contains("Absichtlicher Testfehler"));
    }

    @Test
    void userConfirmationGatesSystemChange() {
        registry.register(new EchoModule("nwos.echo"), Set.of(PermissionScope.SYSTEM_CHANGE));
        registry.grantExternalScope("nwos.sender", Set.of(PermissionScope.SYSTEM_CHANGE));

        var message = ModuleMessage.create("nwos.sender", "nwos.echo", "alice.quarantine", "{}", Set.of());
        var result = router.route(message).join();

        assertEquals(RoutingOutcome.PENDING_USER_CONFIRMATION, result.outcome());
        assertNull(result.response());
    }

    /** Minimal-Modul, das den Payload als Antwort zurückgibt. */
    private record EchoModule(String id) implements IModule {
        @Override public String getModuleId() { return id; }
        @Override public String getDisplayName() { return id; }
        @Override public CompletableFuture<Void> start() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<Void> stop() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<ModuleHealth> checkHealth() {
            return CompletableFuture.completedFuture(ModuleHealth.of(id, de.nwos.contracts.ModuleStatus.HEALTHY, "ok"));
        }
        @Override public CompletableFuture<ModuleMessage> handleMessage(ModuleMessage message) {
            return CompletableFuture.completedFuture(ModuleMessage.create(
                    id, message.sourceModuleId(), message.messageType() + ".result",
                    message.payloadJson(), Set.of()));
        }
    }

    /** Modul, das beim Empfang einer Nachricht absichtlich wirft. */
    private record CrashingModule(String id) implements IModule {
        @Override public String getModuleId() { return id; }
        @Override public String getDisplayName() { return id; }
        @Override public CompletableFuture<Void> start() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<Void> stop() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<ModuleHealth> checkHealth() {
            return CompletableFuture.completedFuture(ModuleHealth.of(id, de.nwos.contracts.ModuleStatus.HEALTHY, "ok"));
        }
        @Override public CompletableFuture<ModuleMessage> handleMessage(ModuleMessage message) {
            return CompletableFuture.failedFuture(new IllegalStateException("Absichtlicher Testfehler."));
        }
    }
}
