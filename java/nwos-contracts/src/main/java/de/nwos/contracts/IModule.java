package de.nwos.contracts;

import java.util.concurrent.CompletableFuture;

/**
 * Jedes NWOS-Modul (Alya, Marc, Alice, Overlay, ...) implementiert diese
 * Schnittstelle, damit Cardinal es einheitlich starten, überwachen, pausieren
 * und beenden kann (Architekturdokument Abschnitt 2 und 3).
 *
 * <p>Wichtig: Diese Schnittstelle beschreibt ausschließlich den Lebenszyklus.
 * Fachliche Kommunikation läuft über {@link ModuleMessage} und den IPC-Bus in
 * Cardinal, niemals über direkte Objektreferenzen zwischen Modulen
 * (Abschnitt 7 / 31: kein Modul darf ein anderes direkt durchgreifen).</p>
 *
 * <p>Port von NWOS.Contracts.IModule.</p>
 */
public interface IModule {
    /** Eindeutiger, stabiler Modul-Bezeichner, z. B. "nwos.system". */
    String getModuleId();

    /** Menschlich lesbarer Name für UI/Logs. */
    String getDisplayName();

    /**
     * Wird von Cardinal beim Hochfahren aufgerufen. Muss schnell zurückkehren;
     * langlaufende Initialisierung gehört in einen Hintergrund-Task.
     */
    CompletableFuture<Void> start();

    /**
     * Sauberes Beenden. Muss innerhalb kurzer Zeit terminieren, damit Cardinal
     * Updates/Neustarts nicht blockiert (Abschnitt 37).
     */
    CompletableFuture<Void> stop();

    /**
     * Wird periodisch vom HealthMonitor aufgerufen (Heartbeat-Prinzip,
     * Abschnitt 5). Darf niemals blockieren oder den Aufrufer stören -
     * Implementierungen fangen eigene Fehler intern ab.
     */
    CompletableFuture<ModuleHealth> checkHealth();

    /**
     * Empfängt eine an dieses Modul gerichtete Nachricht. Cardinal garantiert,
     * dass die Nachricht bereits durch den PermissionManager geprüft wurde.
     * Kann {@code null} zurückgeben, wenn keine Antwort nötig ist.
     */
    CompletableFuture<ModuleMessage> handleMessage(ModuleMessage message);
}
