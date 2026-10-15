using System.Text.Json;
using NWOS.Contracts;

namespace NWOS.Alice;

/// <summary>
/// Registriert sich bei Cardinal als "nwos.alice" (Architekturdokument
/// Abschnitt 8). Überwacht ein konfiguriertes Verzeichnis passiv per
/// <see cref="FileSystemWatcher"/> (kein Polling, Abschnitt 35), wertet neue/
/// geänderte Dateien über <see cref="HeuristicEngine"/> und
/// <see cref="BurstDetector"/> aus, protokolliert jede Entscheidung
/// (<see cref="SecurityAuditLog"/>) und isoliert nur bei der einen, eng
/// gefassten Regel automatisch, die als eindeutig sicher automatisierbar
/// eingestuft ist (Abschnitt 8) - alles andere bleibt informativ, bis der
/// Nutzer (über eine höhere Schicht) eine Aktion bestätigt.
///
/// Unterstützte MessageType-Werte:
///   - "alice.getRecentEvents"  { "count": int }
///   - "alice.quarantine"       { "filePath": string }
///   - "alice.restore"          { "quarantineId": string }
///   - "alice.listQuarantined"  (kein Payload nötig)
/// </summary>
public sealed class AliceModule : IModule, IDisposable
{
    public string ModuleId => "nwos.alice";
    public string DisplayName => "Alice";

    private readonly string _watchedDirectory;
    private readonly HeuristicEngine _heuristics = new();
    private readonly BurstDetector _burstDetector = new();
    private readonly SecurityAuditLog _auditLog;
    private readonly QuarantineService _quarantine;

    private FileSystemWatcher? _watcher;
    private bool _watcherActive;

    // Einzige Regel, die ohne Nutzerbestätigung automatisch isoliert wird -
    // bewusst als explizite, überschaubare Liste statt verstreuter Bedingungen,
    // damit sie bei Bedarf (Abschnitt 8) leicht überprüft/erweitert werden kann.
    private static readonly IReadOnlySet<string> AutoQuarantineRuleIds = new HashSet<string> { "double-extension-executable" };

    public AliceModule(string watchedDirectory, string stateDirectory)
    {
        _watchedDirectory = watchedDirectory;
        _auditLog = new SecurityAuditLog(stateDirectory);
        _quarantine = new QuarantineService(stateDirectory);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _auditLog.LoadAsync(cancellationToken);
        await _quarantine.LoadAsync(cancellationToken);

        if (!Directory.Exists(_watchedDirectory))
        {
            // Abschnitt 36: ein fehlendes Verzeichnis darf Alice nicht zum
            // Absturz bringen - Modul bleibt "degraded" statt unresponsive,
            // damit HealthMonitor keine Crash-Loop-Recovery auslöst.
            _watcherActive = false;
            return;
        }

        _watcher = new FileSystemWatcher(_watchedDirectory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
        _watcherActive = true;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;
            _watcher.Dispose();
            _watcher = null;
        }

        _watcherActive = false;
        return Task.CompletedTask;
    }

    public void Dispose() => _watcher?.Dispose();

    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleHealth(
            ModuleId,
            _watcherActive ? ModuleStatus.Healthy : ModuleStatus.Degraded,
            _watcherActive
                ? $"Überwacht: {_watchedDirectory}"
                : $"Überwachung inaktiv (Verzeichnis fehlt oder Modul gestoppt): {_watchedDirectory}"));

    private async void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // FileSystemWatcher-Callbacks laufen auf einem eigenen Thread-Pool-
        // Thread; Exceptions hier dürfen den Prozess nicht mitreißen
        // (Abschnitt 36) - deshalb vollständig try/catch-umschlossen statt
        // sie an den Aufrufer durchzureichen (es gibt hier keinen sinnvollen
        // Aufrufer, der sie behandeln könnte).
        try
        {
            var now = DateTimeOffset.UtcNow;

            if (_burstDetector.RecordEventAndCheckBurst(now))
            {
                var burstEvent = ThreatEvent.Create(
                    _watchedDirectory,
                    ruleId: "mass-file-write-burst",
                    explanation: "Ungewöhnlich viele Dateiänderungen in kurzer Zeit im überwachten Verzeichnis.",
                    severity: ThreatSeverity.Suspicious,
                    autoActionTaken: false);
                await _auditLog.AppendAsync(burstEvent, CancellationToken.None);
            }

            foreach (var threatEvent in _heuristics.EvaluateFileCreatedOrChanged(e.FullPath))
            {
                var autoAction = AutoQuarantineRuleIds.Contains(threatEvent.RuleId);
                var finalEvent = threatEvent with { AutoActionTaken = autoAction };
                await _auditLog.AppendAsync(finalEvent, CancellationToken.None);

                if (autoAction)
                {
                    try
                    {
                        await _quarantine.QuarantineAsync(e.FullPath, CancellationToken.None);
                    }
                    catch (IOException)
                    {
                        // Datei evtl. bereits verschoben/gelöscht/gesperrt -
                        // Ereignis bleibt trotzdem protokolliert.
                    }
                }
            }
        }
        catch
        {
            // Bewusst breit gefangen: ein Fehler in der Auswertung eines
            // einzelnen Dateiereignisses darf die Überwachung als Ganzes nicht
            // stoppen (Abschnitt 36). TODO(Phase 4): strukturiertes Logging
            // statt stillem Schlucken, sobald ein zentraler Log-Sink existiert.
        }
    }

    public async Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        var root = payload.RootElement;

        object result = message.MessageType switch
        {
            "alice.getRecentEvents" => _auditLog.GetRecent(
                root.TryGetProperty("count", out var c) ? c.GetInt32() : 20),

            "alice.quarantine" => new
            {
                quarantineId = await _quarantine.QuarantineAsync(root.GetProperty("filePath").GetString()!, cancellationToken),
            },

            "alice.restore" => await HandleRestoreAsync(root, cancellationToken),

            "alice.listQuarantined" => _quarantine.AllQuarantined,

            _ => throw new NotSupportedException($"Unbekannter MessageType '{message.MessageType}' für {ModuleId}."),
        };

        return message with
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SourceModuleId = ModuleId,
            TargetModuleId = message.SourceModuleId,
            MessageType = $"{message.MessageType}.result",
            PayloadJson = JsonSerializer.Serialize(result, NwosJson.ResponseOptions),
        };
    }

    private async Task<object> HandleRestoreAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var quarantineId = root.GetProperty("quarantineId").GetString()!;
        await _quarantine.RestoreAsync(quarantineId, cancellationToken);
        return new { restored = true, quarantineId };
    }
}
