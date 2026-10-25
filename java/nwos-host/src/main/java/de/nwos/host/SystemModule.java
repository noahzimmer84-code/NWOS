package de.nwos.host;

import de.nwos.contracts.IModule;
import de.nwos.contracts.ModuleHealth;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleStatus;
import de.nwos.contracts.NwosJson;
import de.nwos.contracts.PermissionScope;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Eingebautes Basismodul des Hosts: beantwortet Status- und Ping-Anfragen
 * (z. B. vom Overlay), damit der IPC-Bus sofort end-to-end nutzbar ist.
 * Alya/Marc/Alice werden als eigene Module in den nächsten Migrationsschritten
 * ergänzt - der Kern ändert sich dafür nicht (Abschnitt 32).
 */
public final class SystemModule implements IModule {

    public static final String MODULE_ID = "nwos.system";

    private final AtomicBoolean running = new AtomicBoolean(false);

    @Override
    public String getModuleId() {
        return MODULE_ID;
    }

    @Override
    public String getDisplayName() {
        return "NWOS System";
    }

    @Override
    public CompletableFuture<Void> start() {
        running.set(true);
        return CompletableFuture.completedFuture(null);
    }

    @Override
    public CompletableFuture<Void> stop() {
        running.set(false);
        return CompletableFuture.completedFuture(null);
    }

    @Override
    public CompletableFuture<ModuleHealth> checkHealth() {
        var status = running.get() ? ModuleStatus.HEALTHY : ModuleStatus.DISABLED;
        return CompletableFuture.completedFuture(
                ModuleHealth.of(MODULE_ID, status, running.get() ? "Läuft." : "Gestoppt."));
    }

    @Override
    public CompletableFuture<ModuleMessage> handleMessage(ModuleMessage message) {
        return switch (message.messageType() == null ? "" : message.messageType()) {
            case "system.status" -> CompletableFuture.completedFuture(respond(message,
                    Map.of("status", running.get() ? "ok" : "stopped",
                            "serverTime", OffsetDateTime.now(ZoneOffset.UTC).toString())));
            case "system.ping" -> CompletableFuture.completedFuture(
                    respond(message, Map.of("echo", message.payloadJson() == null ? "" : message.payloadJson())));
            default -> CompletableFuture.completedFuture(null);
        };
    }

    private ModuleMessage respond(ModuleMessage request, Map<String, Object> payload) {
        try {
            return ModuleMessage.create(MODULE_ID, request.sourceModuleId(),
                    request.messageType() + ".result",
                    NwosJson.mapper().writeValueAsString(payload),
                    Set.of()); // Antwort eines Moduls: keine weiteren Scopes nötig.
        } catch (Exception ex) {
            throw new IllegalStateException("Antwort-Payload konnte nicht serialisiert werden.", ex);
        }
    }
}
