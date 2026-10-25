package de.nwos.contracts;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

/**
 * Einheitliches, versioniertes Nachrichtenformat für die Kommunikation über
 * Cardinal (Architekturdokument Abschnitt 7: "strukturierte, versionierte
 * Events"). Payload ist bewusst als einfacher String (z. B. JSON) gehalten,
 * damit die Contracts keine Abhängigkeit auf konkrete Fach-DTOs der Module
 * braucht - das hält die Modulgrenzen sauber.
 *
 * <p>Port von NWOS.Contracts.ModuleMessage.</p>
 */
public record ModuleMessage(
        String messageId,
        String sourceModuleId,
        String targetModuleId,
        String messageType,
        String payloadJson,
        Set<PermissionScope> requiredScope,
        OffsetDateTime createdAt,
        int schemaVersion) {

    private static final int CURRENT_SCHEMA_VERSION = 1;

    public ModuleMessage {
        requiredScope = requiredScope == null ? Set.of() : Set.copyOf(requiredScope);
        if (createdAt == null) createdAt = OffsetDateTime.now(ZoneOffset.UTC);
        if (schemaVersion <= 0) schemaVersion = CURRENT_SCHEMA_VERSION;
    }

    public static ModuleMessage create(
            String sourceModuleId,
            String targetModuleId,
            String messageType,
            String payloadJson,
            Set<PermissionScope> requiredScope) {
        return new ModuleMessage(
                UUID.randomUUID().toString().replace("-", ""),
                sourceModuleId,
                targetModuleId,
                messageType,
                payloadJson,
                requiredScope,
                OffsetDateTime.now(ZoneOffset.UTC),
                CURRENT_SCHEMA_VERSION);
    }

    /**
     * Vereinigt den deklarierten Scope mit zusätzlichen Scopes (z. B. dem
     * Minimum aus der MessageScopePolicy). Der deklarierte Scope des Absenders
     * ist nur eine Untergrenze, kein Maximalwert.
     */
    public ModuleMessage withAdditionalScope(Set<PermissionScope> additional) {
        var merged = new HashSet<PermissionScope>(requiredScope);
        if (additional != null) {
            merged.addAll(additional);
        }
        return new ModuleMessage(messageId, sourceModuleId, targetModuleId, messageType,
                payloadJson, merged, createdAt, schemaVersion);
    }
}
