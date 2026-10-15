namespace NWOS.SystemCenter;

public sealed record AutostartEntry(string Name, string CommandLine, bool IsEnabled);

/// <summary>
/// Erweiterungspunkt für Autostart-Verwaltung (Architekturdokument
/// Abschnitt 25: "Autostart verwalten"). Reales Verhalten (Windows Run-Key)
/// in NWOS.SystemCenter.Windows - siehe Begründung bei
/// <see cref="ISystemStatusProvider"/>.
/// </summary>
public interface IAutostartManager
{
    Task<IReadOnlyList<AutostartEntry>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deaktivieren verschiebt den Eintrag reversibel in einen "disabled"-
    /// Bereich statt ihn zu löschen (Abschnitt 41: Sicherheit/Reversibilität
    /// vor Komfort) - erneutes Aktivieren muss ohne Datenverlust möglich sein.
    /// </summary>
    Task SetEnabledAsync(string name, bool isEnabled, CancellationToken cancellationToken);
}
