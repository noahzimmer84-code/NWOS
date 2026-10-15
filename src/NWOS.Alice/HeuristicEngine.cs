namespace NWOS.Alice;

/// <summary>
/// Einfache, transparente Regeln für einzelne Dateiereignisse
/// (Architekturdokument Abschnitt 8). Bewusst keine "KI-Einschätzung" ohne
/// nachvollziehbare Begründung, sondern feste, benannte Regeln - jede
/// Erkennung lässt sich auf eine konkrete <see cref="ThreatEvent.RuleId"/>
/// zurückführen.
///
/// WICHTIG: Dies ist ein bewusst kleines, illustratives Regelwerk für den
/// Entwicklungsstand "kleinster stabiler Kern + Phase 3", keine vollständige
/// Malware-Erkennung. Es ersetzt kein echtes Antivirus-Produkt. TODO(Phase 7,
/// Sicherheitshärtung): Regelwerk mit echten Sicherheitsexperten reviewen,
/// bevor Alice produktiv auf einem echten System läuft.
/// </summary>
public sealed class HeuristicEngine
{
    // Sehr verbreitetes, öffentlich dokumentiertes Erkennungsmuster für als
    // Dokument getarnte ausführbare Dateien (z. B. "rechnung.pdf.exe").
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".jpg", ".jpeg", ".png", ".txt" };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".scr", ".bat", ".cmd", ".vbs", ".js", ".ps1", ".msi" };

    public IReadOnlyList<ThreatEvent> EvaluateFileCreatedOrChanged(string filePath)
    {
        var events = new List<ThreatEvent>();

        if (TryDetectDoubleExtension(filePath, out var doubleExtEvent))
        {
            events.Add(doubleExtEvent!);
        }

        if (TryDetectNewExecutableInDownloads(filePath, out var downloadEvent))
        {
            events.Add(downloadEvent!);
        }

        return events;
    }

    private static bool TryDetectDoubleExtension(string filePath, out ThreatEvent? threatEvent)
    {
        threatEvent = null;
        var fileName = Path.GetFileName(filePath);
        var outerExtension = Path.GetExtension(fileName);

        if (!ExecutableExtensions.Contains(outerExtension))
        {
            return false;
        }

        var nameWithoutOuterExtension = fileName[..^outerExtension.Length];
        var innerExtension = Path.GetExtension(nameWithoutOuterExtension);

        if (!DocumentExtensions.Contains(innerExtension))
        {
            return false;
        }

        threatEvent = ThreatEvent.Create(
            filePath,
            ruleId: "double-extension-executable",
            explanation: $"Datei '{fileName}' hat eine Dokument-Endung ('{innerExtension}') gefolgt von einer " +
                         $"ausführbaren Endung ('{outerExtension}') - ein verbreitetes Muster für als Dokument " +
                         "getarnte Schadsoftware.",
            severity: ThreatSeverity.Critical,
            autoActionTaken: false); // Ob automatisch gehandelt wird, entscheidet AliceModule anhand der Policy.

        return true;
    }

    private static bool TryDetectNewExecutableInDownloads(string filePath, out ThreatEvent? threatEvent)
    {
        threatEvent = null;
        var extension = Path.GetExtension(filePath);

        if (!ExecutableExtensions.Contains(extension))
        {
            return false;
        }

        var parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? string.Empty);
        if (!parentDirectoryName.Equals("Downloads", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        threatEvent = ThreatEvent.Create(
            filePath,
            ruleId: "new-executable-in-downloads",
            explanation: $"Neue ausführbare Datei im Downloads-Ordner: '{Path.GetFileName(filePath)}'. " +
                         "Rein informativ, keine automatische Aktion.",
            severity: ThreatSeverity.Info,
            autoActionTaken: false);

        return true;
    }
}
