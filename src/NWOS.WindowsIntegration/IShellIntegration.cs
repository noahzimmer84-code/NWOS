namespace NWOS.WindowsIntegration;

/// <summary>
/// Schnittstelle für alles, was NWOS an Windows selbst anfassen muss
/// (Architekturdokument Abschnitt 4: klare Trennung Windows-System /
/// NWOS-Systemebene). Jede Implementierung muss reversibel sein -
/// <see cref="RestoreDefaultShellAsync"/> ist deshalb Pflichtbestandteil der
/// Schnittstelle, nicht optional.
/// </summary>
public interface IShellIntegration
{
    /// <summary>
    /// Prüft, ob NWOS aktuell als alternative Shell registriert ist.
    /// </summary>
    Task<bool> IsNwosShellActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registriert NWOS als alternative Shell für den aktuellen Benutzer
    /// (nicht systemweit, siehe Begründung in <see cref="ShellIntegration"/>).
    /// </summary>
    Task RegisterNwosShellAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Macht die Registrierung rückgängig und stellt explorer.exe als
    /// Standard-Shell wieder her. Muss auch dann zuverlässig funktionieren,
    /// wenn NWOS abgestürzt ist (Aufruf z. B. aus dem abgesicherten Modus /
    /// einem separaten Recovery-Tool) - siehe Auftrag Abschnitt 4/42:
    /// "möglichst wenig invasive Änderungen", volle Reversibilität.
    /// </summary>
    Task RestoreDefaultShellAsync(CancellationToken cancellationToken);
}
