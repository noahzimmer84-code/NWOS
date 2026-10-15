using System.Text.Json;
using NWOS.Contracts;
using NWOS.Settings;

namespace NWOS.DesktopShell;

/// <summary>
/// Registriert sich bei Cardinal als "nwos.desktopshell" (Architekturdokument
/// Abschnitt 15: Desktop-Grundgerüst). Bündelt Icons, Boxen und Suche zu
/// einem Modul, weil sie fachlich zusammengehören und gemeinsam laden/speichern -
/// das entspricht der Empfehlung aus den Persistenz-Hinweisen, zusammengehörige
/// Daten nicht unnötig in viele Einzelaufrufe zu splitten.
///
/// Nimmt Nachrichten über <see cref="HandleMessageAsync"/> entgegen (Abschnitt 7:
/// alle Modul-Kommunikation läuft über Cardinal, nie direkt). Unterstützte
/// MessageType-Werte:
///   - "icon.toggleVisibility"  { "iconId": string }
///   - "box.create"             { "name": string }
///   - "box.addItem"            { "boxId": string, "sourcePath": string, "kind": string }
///   - "box.search"             { "boxId": string, "query": string }
///   - "search.global"          { "query": string }
///   - "search.rebuildIndex"    { "rootDirectory": string }
///   - "icon.listStates"        (kein Payload nötig)
///   - "box.list"               (kein Payload nötig)
/// </summary>
public sealed class DesktopShellModule : IModule
{
    public string ModuleId => "nwos.desktopshell";
    public string DisplayName => "Desktop Shell";

    private readonly IconVisibilityStore _iconVisibility;
    private readonly BoxManager _boxManager;
    private readonly SearchIndex _searchIndex;
    private readonly SettingsStore _settings;

    private bool _started;

    public DesktopShellModule(string stateDirectory, SettingsStore settings)
    {
        _iconVisibility = new IconVisibilityStore(stateDirectory);
        _boxManager = new BoxManager(stateDirectory);
        _searchIndex = new SearchIndex();
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _iconVisibility.LoadAsync(cancellationToken);
        await _boxManager.LoadAsync(cancellationToken);
        // Bewusst KEIN automatischer Such-Index-Rebuild beim Start: ein
        // vollständiger Reindex ist teuer (Performance-Grundsatz, Abschnitt 35)
        // und der Wurzelordner ist noch nicht konfiguriert. Wird explizit über
        // "search.rebuildIndex" ausgelöst, z. B. beim ersten Einrichten der
        // Suche oder auf Nutzeranforderung.
        _started = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        await _iconVisibility.SaveAsync(cancellationToken);
        await _boxManager.SaveAsync(cancellationToken);
        _started = false;
    }

    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleHealth(
            ModuleId,
            _started ? ModuleStatus.Healthy : ModuleStatus.Unresponsive,
            $"Boxen: {_boxManager.AllBoxes.Count}, Such-Index-Einträge: {_searchIndex.EntryCount}"));

    public async Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        var root = payload.RootElement;

        object result = message.MessageType switch
        {
            "icon.toggleVisibility" => new
            {
                iconId = root.GetProperty("iconId").GetString(),
                isVisible = _iconVisibility.ToggleVisibility(root.GetProperty("iconId").GetString()!),
            },

            "box.create" => DescribeBox(_boxManager.CreateBox(root.GetProperty("name").GetString()!)),

            "icon.listStates" => _iconVisibility.GetAllStates(),

            "box.list" => _boxManager.AllBoxes.Select(b => new
            {
                b.BoxId,
                b.Name,
                b.DisplayMode,
                b.Width,
                b.Height,
                Items = b.Items.Select(i => new { i.ItemId, i.SourcePath, i.Kind }).ToList(),
            }).ToList(),

            "box.addItem" => HandleAddItem(root),

            "box.search" => HandleBoxSearch(root),

            "search.global" => _searchIndex.Search(root.GetProperty("query").GetString() ?? string.Empty),

            "search.rebuildIndex" => await HandleRebuildIndexAsync(root, cancellationToken),

            _ => throw new NotSupportedException($"Unbekannter MessageType '{message.MessageType}' für {ModuleId}."),
        };

        // Änderungen sofort sichern: der Zustand darf nicht davon abhängen, dass
        // Cardinal später sauber beendet wird (Abschnitt 36).
        if (message.MessageType is "icon.toggleVisibility" or "box.create" or "box.addItem")
        {
            await _iconVisibility.SaveAsync(cancellationToken);
            await _boxManager.SaveAsync(cancellationToken);
        }

        var responsePayload = JsonSerializer.Serialize(result, NwosJson.ResponseOptions);
        return message with
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SourceModuleId = ModuleId,
            TargetModuleId = message.SourceModuleId,
            MessageType = $"{message.MessageType}.result",
            PayloadJson = responsePayload,
        };
    }

    private object HandleAddItem(JsonElement root)
    {
        var boxId = root.GetProperty("boxId").GetString()!;
        if (!_boxManager.TryGetBox(boxId, out var box))
        {
            throw new InvalidOperationException($"Box '{boxId}' existiert nicht.");
        }

        var kind = Enum.Parse<BoxItemKind>(root.GetProperty("kind").GetString()!, ignoreCase: true);
        var item = box.AddItem(root.GetProperty("sourcePath").GetString()!, kind);
        return item;
    }

    private object HandleBoxSearch(JsonElement root)
    {
        var boxId = root.GetProperty("boxId").GetString()!;
        if (!_boxManager.TryGetBox(boxId, out var box))
        {
            throw new InvalidOperationException($"Box '{boxId}' existiert nicht.");
        }

        var query = root.TryGetProperty("query", out var q) ? q.GetString() ?? string.Empty : string.Empty;
        return box.SearchContent(query).ToList();
    }

    private async Task<object> HandleRebuildIndexAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var rootDirectory = root.GetProperty("rootDirectory").GetString()!;
        await _searchIndex.RebuildAsync(rootDirectory, cancellationToken);
        return new { entryCount = _searchIndex.EntryCount };
    }

    private static object DescribeBox(SmartBox box) => new
    {
        box.BoxId,
        box.Name,
        box.DisplayMode,
        box.Width,
        box.Height,
    };
}
