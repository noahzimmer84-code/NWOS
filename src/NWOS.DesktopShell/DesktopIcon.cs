namespace NWOS.DesktopShell;

/// <summary>
/// Repräsentiert ein Desktop-Icon (Architekturdokument Abschnitt 16).
/// Position, Verknüpfung/Datei/Ordner und Einstellungen gehören zur
/// eigentlichen Windows-Verknüpfung bzw. Desktop-Positionsdatenbank und werden
/// von NWOS nicht dupliziert - hier wird nur das gespeichert, was NWOS
/// zusätzlich braucht: ob das Icon aktuell ausgeblendet ist.
/// </summary>
public sealed record DesktopIcon(
    string IconId,
    string TargetPath,
    string DisplayName)
{
    /// <summary>
    /// Bewusst NICHT Teil dieses Records: Position/Link/Datei. Die
    /// Sichtbarkeit wird getrennt in <see cref="IconVisibilityStore"/>
    /// verwaltet, damit ein Sichtbarkeits-Toggle niemals versehentlich
    /// Position, Verknüpfung, Datei, Ordner oder Einstellungen verändert
    /// (explizite Anforderung aus Abschnitt 16).
    /// </summary>
    public static DesktopIcon Create(string targetPath, string displayName) =>
        new(Guid.NewGuid().ToString("N"), targetPath, displayName);
}
