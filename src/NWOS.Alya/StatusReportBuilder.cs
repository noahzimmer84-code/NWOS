using System.Text.Json;
using NWOS.Contracts;

namespace NWOS.Alya;

public sealed record StatusReportEntry(string Category, string Text);
public sealed record StatusReport(DateTimeOffset GeneratedAt, IReadOnlyList<StatusReportEntry> Entries);

/// <summary>
/// Baut den Lagebericht (Architekturdokument Abschnitt 12: "Dieser enthält
/// relevante Änderungen, die während der Abwesenheit des Benutzers
/// stattgefunden haben."). Holt sich Daten NICHT direkt von Alice, sondern
/// ausschließlich über den bei Cardinal registrierten <see cref="IModuleMessenger"/>
/// - genau der Kommunikationsweg aus Abschnitt 6/31, den Alya laut Architektur
/// einhalten muss.
///
/// TODO(spätere Phasen): weitere Kategorien ergänzen, sobald die jeweiligen
/// Module existieren - Updates (Abschnitt 37), abgeschlossene
/// Wartungsarbeiten (Abschnitt 26), Geräteverbindungen (Abschnitt 8 der
/// Multi-Device-Sektion). Für den aktuellen Stand nur Sicherheitsereignisse
/// (Alice), weil das das einzige Modul ist, das dafür bereits etwas liefert -
/// zusätzliche Kategorien ohne echte Datenquelle wären ein Fake-Lagebericht
/// (Abschnitt 40).
/// </summary>
public sealed class StatusReportBuilder
{
    private readonly IModuleMessenger? _messenger;
    private readonly string _selfModuleId;

    public StatusReportBuilder(IModuleMessenger? messenger, string selfModuleId)
    {
        _messenger = messenger;
        _selfModuleId = selfModuleId;
    }

    public async Task<StatusReport> BuildAsync(CancellationToken cancellationToken)
    {
        var entries = new List<StatusReportEntry>();

        if (_messenger is null)
        {
            entries.Add(new StatusReportEntry("Hinweis", "Keine Verbindung zu Cardinal - Lagebericht ist unvollständig."));
            return new StatusReport(DateTimeOffset.UtcNow, entries);
        }

        entries.AddRange(await FetchAliceEventsAsync(cancellationToken));

        if (entries.Count == 0)
        {
            entries.Add(new StatusReportEntry("Sicherheit", "Keine sicherheitsrelevanten Ereignisse seit dem letzten Bericht."));
        }

        return new StatusReport(DateTimeOffset.UtcNow, entries);
    }

    private async Task<List<StatusReportEntry>> FetchAliceEventsAsync(CancellationToken cancellationToken)
    {
        var entries = new List<StatusReportEntry>();

        var request = ModuleMessage.Create(
            sourceModuleId: _selfModuleId,
            targetModuleId: "nwos.alice",
            messageType: "alice.getRecentEvents",
            payloadJson: JsonSerializer.Serialize(new { count = 5 }),
            // Bewusst PermissionScope.None: eine lesende Anfrage nach den
            // letzten (bereits protokollierten) Sicherheitsereignissen ist
            // kein Systemeingriff - Alya bekommt dadurch keine zusätzlichen
            // Rechte, sie liest nur, was Alice ohnehin schon aufgezeichnet hat.
            requiredScope: PermissionScope.None);

        var result = await _messenger!.SendAsync(request, cancellationToken);

        if (result.Outcome != ModuleMessageOutcome.Delivered || result.Response is null)
        {
            entries.Add(new StatusReportEntry("Sicherheit",
                $"Sicherheitsstatus konnte nicht abgerufen werden ({result.Outcome}: {result.Reason})."));
            return entries;
        }

        using var doc = JsonDocument.Parse(result.Response.PayloadJson);
        foreach (var eventElement in doc.RootElement.EnumerateArray())
        {
            var ruleId = eventElement.GetProperty("ruleId").GetString();
            var explanation = eventElement.GetProperty("explanation").GetString();
            entries.Add(new StatusReportEntry("Sicherheit", $"[{ruleId}] {explanation}"));
        }

        return entries;
    }
}
