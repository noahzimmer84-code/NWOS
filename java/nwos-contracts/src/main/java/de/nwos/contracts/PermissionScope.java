package de.nwos.contracts;

/**
 * Grobe Berechtigungskategorien gemäß Architekturdokument Abschnitt 9
 * (Berechtigungssystem). Ein Modul deklariert beim Registrieren bei Cardinal,
 * welche Scopes es grundsätzlich benötigen darf; jede konkrete Aktion wird
 * zusätzlich vom PermissionManager einzeln geprüft.
 *
 * <p>Da Java-Enums keine Bit-Flags kennt (C# [Flags]), wird ein
 * {@link java.util.Set} von Scopes verwendet; die leere Menge entspricht
 * PermissionScope.None.</p>
 */
public enum PermissionScope {
    FILE_SYSTEM_READ,
    FILE_SYSTEM_WRITE,
    PROCESS_CONTROL,
    /** Eng gefasste, whitelistende Recherche (Marc, Abschnitt 9). */
    NETWORK_RESEARCH,
    /** Bewusst NICHT an Marc vergeben. */
    NETWORK_GENERAL,
    /** Erfordert i.d.R. zusätzliche Nutzerfreigabe pro Aktion. */
    SYSTEM_CHANGE,
    /** Multi-Device, Abschnitt 8. */
    DEVICE_COMMUNICATION
}
