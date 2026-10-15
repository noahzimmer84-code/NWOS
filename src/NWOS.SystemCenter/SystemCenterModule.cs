using System.Text.Json;
using NWOS.Contracts;

namespace NWOS.SystemCenter;

/// <summary>
/// Registriert sich bei Cardinal als "nwos.systemcenter" (Architekturdokument
/// Abschnitt 24/25). Bündelt Status, Optimierung und Autostart-Verwaltung.
/// Schließt NIEMALS selbst einen Prozess - dafür wird über
/// <see cref="IModuleMessenger"/> an Marc delegiert (Single Source of Truth
/// für Prozessbeendigung, siehe <c>NWOS.Marc.ProcessController</c>), nach
/// einer zusätzlichen lokalen Prüfung gegen <see cref="ProtectedProcessList"/>.
///
/// Unterstützte MessageType-Werte:
///   - "systemcenter.getStatus"            (kein Payload nötig)
///   - "systemcenter.listProcesses"        (kein Payload nötig)
///   - "systemcenter.cleanTemp"            { "minAgeHours"?: number }
///   - "systemcenter.optimize"             (kein Payload nötig - der "Raketen-Button")
///   - "systemcenter.listAutostart"        (kein Payload nötig)
///   - "systemcenter.setAutostartEnabled"  { "name": string, "isEnabled": bool }
///   - "systemcenter.closeProcess"         { "processId": int }
/// </summary>
public sealed class SystemCenterModule : IModule
{
    public string ModuleId => "nwos.systemcenter";
    public string DisplayName => "System Center";

    private readonly ISystemStatusProvider? _statusProvider;
    private readonly IAutostartManager? _autostartManager;
    private readonly IModuleMessenger? _messenger;
    private readonly string _tempDirectory;
    private readonly TempCleaner _tempCleaner = new();
    private readonly ProcessLister _processLister = new();

    private bool _started;

    public SystemCenterModule(
        ISystemStatusProvider? statusProvider,
        IAutostartManager? autostartManager,
        IModuleMessenger? messenger,
        string? tempDirectory = null)
    {
        _statusProvider = statusProvider;
        _autostartManager = autostartManager;
        _messenger = messenger;
        _tempDirectory = tempDirectory ?? Path.GetTempPath();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _started = false;
        return Task.CompletedTask;
    }

    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleHealth(ModuleId, _started ? ModuleStatus.Healthy : ModuleStatus.Unresponsive));

    public async Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(string.IsNullOrEmpty(message.PayloadJson) ? "{}" : message.PayloadJson);
        var root = payload.RootElement;

        object result = message.MessageType switch
        {
            "systemcenter.getStatus" => await GetStatusAsync(cancellationToken),
            "systemcenter.listProcesses" => _processLister.ListProcesses(),
            "systemcenter.cleanTemp" => HandleCleanTemp(root, cancellationToken),
            "systemcenter.optimize" => HandleOptimize(cancellationToken),
            "systemcenter.listAutostart" => await GetAutostartAsync(cancellationToken),
            "systemcenter.setAutostartEnabled" => await HandleSetAutostartAsync(root, cancellationToken),
            "systemcenter.closeProcess" => await HandleCloseProcessAsync(root, cancellationToken),
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

    private async Task<object> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (_statusProvider is null)
        {
            return new { available = false, reason = "Kein ISystemStatusProvider konfiguriert (nur unter Windows verfügbar)." };
        }

        return await _statusProvider.CaptureAsync(cancellationToken);
    }

    private object HandleCleanTemp(JsonElement root, CancellationToken cancellationToken)
    {
        var minAgeHours = root.TryGetProperty("minAgeHours", out var h) ? h.GetDouble() : 24.0;
        return _tempCleaner.Clean(_tempDirectory, TimeSpan.FromHours(minAgeHours), cancellationToken);
    }

    private object HandleOptimize(CancellationToken cancellationToken)
    {
        // Der "Raketen-Symbol"-Button aus Abschnitt 25: bewusst NUR die
        // sichere Temp-Bereinigung, kein automatisches Prozess-Beenden - das
        // bleibt eine bewusste, einzelne Nutzeraktion über
        // "systemcenter.closeProcess" (Abschnitt 41: Sicherheit vor Komfort).
        var cleanupResult = _tempCleaner.Clean(_tempDirectory, TimeSpan.FromHours(24), cancellationToken);
        return new { action = "optimize", tempCleanup = cleanupResult };
    }

    private async Task<object> GetAutostartAsync(CancellationToken cancellationToken)
    {
        if (_autostartManager is null)
        {
            return Array.Empty<AutostartEntry>();
        }

        return await _autostartManager.ListAsync(cancellationToken);
    }

    private async Task<object> HandleSetAutostartAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_autostartManager is null)
        {
            throw new InvalidOperationException("Kein IAutostartManager konfiguriert (nur unter Windows verfügbar).");
        }

        var name = root.GetProperty("name").GetString()!;
        var isEnabled = root.GetProperty("isEnabled").GetBoolean();
        await _autostartManager.SetEnabledAsync(name, isEnabled, cancellationToken);
        return new { name, isEnabled };
    }

    private async Task<object> HandleCloseProcessAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var processId = root.GetProperty("processId").GetInt32();

        var process = _processLister.ListProcesses().FirstOrDefault(p => p.ProcessId == processId);
        if (process is not null && process.IsProtected)
        {
            return new { closed = false, reason = $"Prozess '{process.Name}' steht auf der Schutzliste und wird nicht beendet." };
        }

        if (_messenger is null)
        {
            throw new InvalidOperationException("Kein IModuleMessenger konfiguriert - kann Marc nicht um Prozessbeendigung bitten.");
        }

        var request = ModuleMessage.Create(
            ModuleId, "nwos.marc", "marc.closeApp",
            JsonSerializer.Serialize(new { processId }),
            PermissionScope.ProcessControl);

        var response = await _messenger.SendAsync(request, cancellationToken);

        return new
        {
            closed = response.Outcome == ModuleMessageOutcome.Delivered,
            outcome = response.Outcome.ToString(),
            reason = response.Reason,
        };
    }
}
