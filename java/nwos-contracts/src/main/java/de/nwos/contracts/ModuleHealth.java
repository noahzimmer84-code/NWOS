package de.nwos.contracts;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;

/**
 * Ergebnis eines Health-Checks (Heartbeat-Prinzip, Architekturdokument
 * Abschnitt 5). Port von NWOS.Contracts.ModuleHealth.
 */
public record ModuleHealth(String moduleId, ModuleStatus status, String statusMessage, OffsetDateTime timestamp) {

    public ModuleHealth {
        if (statusMessage == null) statusMessage = "";
        if (timestamp == null) timestamp = OffsetDateTime.now(ZoneOffset.UTC);
    }

    public static ModuleHealth of(String moduleId, ModuleStatus status, String statusMessage) {
        return new ModuleHealth(moduleId, status, statusMessage, OffsetDateTime.now(ZoneOffset.UTC));
    }

    public boolean isHealthy() {
        return status == ModuleStatus.HEALTHY;
    }
}
