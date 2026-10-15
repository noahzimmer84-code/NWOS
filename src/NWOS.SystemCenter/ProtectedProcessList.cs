namespace NWOS.SystemCenter;

/// <summary>
/// Verhindert, dass wichtige Systemprozesse versehentlich beendet werden
/// (Architekturdokument Abschnitt 25: "Vor kritischen Aktionen muss ein
/// sicherer Mechanismus verhindern, dass wichtige Systemprozesse versehentlich
/// beendet werden."). Bewusst als explizite, kleine Denyliste statt einer
/// Heuristik ("läuft mit Systemrechten" o.ä.) - eine feste Liste ist
/// nachvollziehbar und lässt sich bei Bedarf gezielt erweitern/reviewen.
///
/// TODO(Phase 7, Sicherheitshärtung): Liste mit echten Windows-Experten
/// reviewen und ggf. um eine Prüfung des Installationspfads ergänzen
/// (z. B. nur Prozesse aus %SystemRoot%\System32 als geschützt behandeln),
/// bevor dies auf einem echten System produktiv eingesetzt wird.
/// </summary>
public static class ProtectedProcessList
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "svchost", "explorer", "dwm", "fontdrvhost",
        "conhost", "ntoskrnl",
    };

    public static bool IsProtected(string processName) => ProtectedNames.Contains(processName);
}
