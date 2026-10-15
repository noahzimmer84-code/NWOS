namespace NWOS.Contracts;

/// <summary>
/// Jedes NWOS-Modul (Alya, Marc, Alice, Desktop Shell, ...) implementiert diese
/// Schnittstelle, damit Cardinal es einheitlich starten, überwachen, pausieren
/// und beenden kann (siehe Architekturdokument Abschnitt 2 und 3).
///
/// Wichtig: Diese Schnittstelle beschreibt ausschließlich den Lebenszyklus.
/// Fachliche Kommunikation läuft über <see cref="ModuleMessage"/> und den
/// IPC-Bus in NWOS.Cardinal, niemals über direkte Objektreferenzen zwischen
/// Modulen (Abschnitt 7 / Abschnitt 31: kein Modul darf ein anderes direkt
/// durchgreifen).
/// </summary>
public interface IModule
{
    /// <summary>Eindeutiger, stabiler Modul-Bezeichner, z. B. "cardinal.alice".</summary>
    string ModuleId { get; }

    /// <summary>Menschlich lesbarer Name für UI/Logs.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Wird von Cardinal beim Hochfahren aufgerufen. Muss schnell zurückkehren;
    /// langlaufende Initialisierung gehört in einen Hintergrund-Task.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sauberes Beenden. Muss innerhalb kurzer Zeit terminieren, damit Cardinal
    /// Updates/Neustarts nicht blockiert (Abschnitt 37).
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Wird periodisch von <see cref="HealthMonitor"/> aufgerufen (Heartbeat-Prinzip,
    /// Abschnitt 5). Darf niemals blockieren oder Exceptions werfen lassen, die den
    /// Aufrufer stören - Implementierungen fangen eigene Fehler intern ab und
    /// melden sie über den Rückgabewert.
    /// </summary>
    Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Empfängt eine an dieses Modul gerichtete Nachricht. Cardinal garantiert,
    /// dass die Nachricht bereits durch den PermissionManager geprüft wurde.
    /// </summary>
    Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken);
}
