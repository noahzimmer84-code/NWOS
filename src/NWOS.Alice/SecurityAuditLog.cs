using System.Text.Json;

namespace NWOS.Alice;

/// <summary>
/// Persistiert jedes <see cref="ThreatEvent"/> lokal (Abschnitt 8:
/// "Sicherheitsrelevante Aktionen müssen nachvollziehbar protokolliert
/// werden.", Abschnitt 34: lokale Datenhaltung). Append-only: bestehende
/// Einträge werden nie verändert, nur neue angehängt - ein Audit-Log, das
/// sich selbst nachträglich umschreiben lässt, wäre wertlos.
/// </summary>
public sealed class SecurityAuditLog
{
    private readonly string _logFilePath;
    private readonly List<ThreatEvent> _events = new();
    private readonly object _lock = new();

    public SecurityAuditLog(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, "alice-audit-log.jsonl");
    }

    /// <summary>Lädt bestehende Einträge (JSON Lines - ein Objekt pro Zeile, robust gegen Teilschreibfehler).</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_logFilePath))
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(_logFilePath, cancellationToken);
        lock (_lock)
        {
            _events.Clear();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var evt = JsonSerializer.Deserialize<ThreatEvent>(line);
                    if (evt is not null)
                    {
                        _events.Add(evt);
                    }
                }
                catch (JsonException)
                {
                    // Eine beschädigte Zeile darf den Rest des Logs nicht
                    // unlesbar machen (Abschnitt 36-Grundsatz auch für Daten).
                }
            }
        }
    }

    public async Task AppendAsync(ThreatEvent threatEvent, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _events.Add(threatEvent);
        }

        var line = JsonSerializer.Serialize(threatEvent) + Environment.NewLine;
        await File.AppendAllTextAsync(_logFilePath, line, cancellationToken);
    }

    public IReadOnlyList<ThreatEvent> GetRecent(int count)
    {
        lock (_lock)
        {
            return _events.OrderByDescending(e => e.DetectedAt).Take(count).ToList();
        }
    }
}
