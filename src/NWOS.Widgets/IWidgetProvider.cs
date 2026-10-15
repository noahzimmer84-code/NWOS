namespace NWOS.Widgets;

/// <summary>
/// Momentaufnahme der Daten eines Widgets. Bewusst technologieneutral
/// (einfaches Key-Value statt eines UI-Frameworks) - eine spätere UI-Schicht
/// entscheidet, wie diese Werte dargestellt werden. Das hält Widget-Provider
/// vollständig UI-unabhängig testbar.
/// </summary>
public sealed record WidgetSnapshot(string WidgetTypeId, DateTimeOffset GeneratedAt, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Erweiterungspunkt für Widget-Typen (Architekturdokument Abschnitt 21:
/// "Das System muss später durch zusätzliche Widgets erweiterbar sein").
/// Jeder neue Widget-Typ implementiert diese Schnittstelle und wird bei
/// <see cref="WidgetRegistry"/> registriert - ohne dass Widgets.Module oder
/// Cardinal angepasst werden müssen (Modularitätsprinzip, Abschnitt 32).
/// </summary>
public interface IWidgetProvider
{
    /// <summary>Eindeutiger, stabiler Typ-Bezeichner, z. B. "nwos.widget.clock".</summary>
    string WidgetTypeId { get; }

    string DisplayName { get; }

    int DefaultWidth { get; }
    int DefaultHeight { get; }

    /// <summary>
    /// Liefert die aktuellen Anzeigedaten. Muss schnell zurückkehren und darf
    /// keine dauerhaften Hintergrund-Timer starten (Performance-Grundsatz,
    /// Abschnitt 35) - Aktualisierung wird vom aufrufenden Modul getaktet,
    /// nicht vom Provider selbst.
    /// </summary>
    Task<WidgetSnapshot> RenderAsync(CancellationToken cancellationToken);
}
