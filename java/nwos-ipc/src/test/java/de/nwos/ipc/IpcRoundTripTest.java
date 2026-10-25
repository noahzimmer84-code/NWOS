package de.nwos.ipc;

import de.nwos.cardinal.ModuleRegistry;
import de.nwos.cardinal.PermissionManager;
import de.nwos.cardinal.TaskRouter;
import de.nwos.contracts.IModule;
import de.nwos.contracts.ModuleHealth;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleMessageOutcome;
import de.nwos.contracts.ModuleStatus;
import de.nwos.contracts.PermissionScope;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.util.Set;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * End-to-End-Test über den echten Loopback-Socket: Handshake, Zustellung,
 * Spoofing-Abwehr und unbekannte Identitäten (die PT1-Fehlerfälle).
 */
class IpcRoundTripTest {

    private IpcServer server;
    private ModuleRegistry registry;

    @BeforeEach
    void startServer() throws IOException {
        registry = new ModuleRegistry();
        var router = new TaskRouter(registry, new PermissionManager(registry));
        registry.register(new EchoModule("nwos.echo"), Set.of(PermissionScope.FILE_SYSTEM_READ));
        registry.grantExternalScope("nwos.client", Set.of(PermissionScope.FILE_SYSTEM_READ));
        registry.grantExternalScope(IpcDefaults.EXTERNAL_OVERLAY_IDENTITY, Set.of(PermissionScope.FILE_SYSTEM_READ));

        server = new IpcServer(router, registry, IpcDefaults.EPHEMERAL_PORT);
        server.start();
    }

    @AfterEach
    void stopServer() {
        server.close();
    }

    @Test
    void deliversMessageEndToEnd() throws IOException {
        try (var client = new IpcClient("nwos.client", server.port())) {
            client.connect();
            var message = ModuleMessage.create("nwos.client", "nwos.echo", "test.echo", "{\"x\":1}", Set.of());
            var result = client.send(message);

            assertEquals(ModuleMessageOutcome.DELIVERED, result.outcome());
            assertEquals("{\"x\":1}", result.response().payloadJson());
        }
    }

    @Test
    void rejectsSpoofedSourceIdentity() throws IOException {
        // Verbindung gehört "nwos.client", behauptet aber, von "nwos.marc" zu
        // kommen -> muss abgewiesen werden (PT1: fehlendes Identity-Enforcement).
        try (var client = new IpcClient("nwos.client", server.port())) {
            client.connect();
            var spoofed = ModuleMessage.create("nwos.marc", "nwos.echo", "test.echo", "{}", Set.of());
            var result = client.send(spoofed);

            assertEquals(ModuleMessageOutcome.DENIED, result.outcome());
            assertTrue(result.reason().contains("Spoofing"));
        }
    }

    @Test
    void rejectsUnknownIdentityAtHandshake() {
        var client = new IpcClient("nwos.evil", server.port());
        var error = assertThrows(IOException.class, client::connect);
        assertTrue(error.getMessage().contains("Handshake"));
    }

    @Test
    void servesMultipleClientsInParallel() throws IOException {
        try (var first = new IpcClient("nwos.client", server.port());
             var second = new IpcClient(IpcDefaults.EXTERNAL_OVERLAY_IDENTITY, server.port())) {
            first.connect();
            second.connect();

            var resultOne = first.send(ModuleMessage.create("nwos.client", "nwos.echo", "test.a", "\"a\"", Set.of()));
            var resultTwo = second.send(ModuleMessage.create(IpcDefaults.EXTERNAL_OVERLAY_IDENTITY,
                    "nwos.echo", "test.b", "\"b\"", Set.of()));

            assertEquals(ModuleMessageOutcome.DELIVERED, resultOne.outcome());
            assertEquals(ModuleMessageOutcome.DELIVERED, resultTwo.outcome());
        }
    }

    private record EchoModule(String id) implements IModule {
        @Override public String getModuleId() { return id; }
        @Override public String getDisplayName() { return id; }
        @Override public CompletableFuture<Void> start() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<Void> stop() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<ModuleHealth> checkHealth() {
            return CompletableFuture.completedFuture(ModuleHealth.of(id, ModuleStatus.HEALTHY, "ok"));
        }
        @Override public CompletableFuture<ModuleMessage> handleMessage(ModuleMessage message) {
            return CompletableFuture.completedFuture(ModuleMessage.create(
                    id, message.sourceModuleId(), message.messageType() + ".result",
                    message.payloadJson(), Set.of()));
        }
    }
}
