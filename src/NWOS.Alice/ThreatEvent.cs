namespace NWOS.Alice;

public enum ThreatSeverity
{
    Info,
    Suspicious,
    Critical,
}

/// <summary>
/// Ein von Alice erkanntes Ereignis (Architekturdokument Abschnitt 8).
/// Bewusst mit <see cref="RuleId"/> statt Freitext-Erklärung als primärem
/// Feld, damit jede Entscheidung auf eine konkrete, nachlesbare Regel
/// zurückführbar ist ("nachvollziehbar protokolliert", Abschnitt 8) statt auf
/// eine nicht reproduzierbare KI-Einschätzung.
/// </summary>
public sealed record ThreatEvent(
    string EventId,
    string FilePath,
    string RuleId,
    string Explanation,
    ThreatSeverity Severity,
    bool AutoActionTaken,
    DateTimeOffset DetectedAt)
{
    public static ThreatEvent Create(string filePath, string ruleId, string explanation, ThreatSeverity severity, bool autoActionTaken) =>
        new(Guid.NewGuid().ToString("N"), filePath, ruleId, explanation, severity, autoActionTaken, DateTimeOffset.UtcNow);
}
