using System.Text.Json;
using NWOS.Contracts;

namespace NWOS.Widgets;

/// <summary>
/// Registriert sich bei Cardinal als "nwos.widgets". Bringt den eingebauten
/// Uhr-Widget-Typ mit; weitere Typen werden über
/// <see cref="WidgetRegistry.RegisterProvider"/> ergänzt, bevor das Modul
/// gestartet wird - typischerweise von NWOS.Host anhand einer künftigen
/// Erweiterungs-/Plugin-Konfiguration (noch nicht implementiert, TODO Phase 2+).
///
/// Unterstützte MessageType-Werte:
///   - "widget.listTypes"                                     (kein Payload nötig)
///   - "widget.list"                                          (kein Payload nötig)
///   - "widget.create"        { "widgetTypeId": string }
///   - "widget.move"          { "instanceId": string, "x": int, "y": int }
///   - "widget.resize"        { "instanceId": string, "width": int, "height": int }
///   - "widget.setEnabled"    { "instanceId": string, "isEnabled": bool }
///   - "widget.remove"        { "instanceId": string }
///   - "widget.render"        { "instanceId": string }
/// </summary>
public sealed class WidgetsModule : IModule
{
    public string ModuleId => "nwos.widgets";
    public string DisplayName => "Widgets";

    private readonly WidgetRegistry _registry;
    private readonly WidgetManager _manager;
    private bool _started;

    public WidgetsModule(string stateDirectory)
    {
        _registry = new WidgetRegistry();
        _registry.RegisterProvider(new ClockWidgetProvider());
        _manager = new WidgetManager(_registry, stateDirectory);
    }

    /// <summary>Erlaubt zusätzlichen Widget-Typen sich vor dem Start zu registrieren.</summary>
    public void RegisterAdditionalProvider(IWidgetProvider provider) => _registry.RegisterProvider(provider);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _manager.LoadAsync(cancellationToken);
        _started = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        await _manager.SaveAsync(cancellationToken);
        _started = false;
    }

    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleHealth(
            ModuleId,
            _started ? ModuleStatus.Healthy : ModuleStatus.Unresponsive,
            $"Widget-Typen: {_registry.AllProviders.Count}, Instanzen: {_manager.AllInstances.Count}"));

    public async Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        var root = payload.RootElement;

        object? result = message.MessageType switch
        {
            "widget.listTypes" => _registry.AllProviders
                .Select(p => new { p.WidgetTypeId, p.DisplayName, p.DefaultWidth, p.DefaultHeight })
                .ToList(),

            "widget.list" => _manager.AllInstances.Select(Describe).ToList(),

            "widget.create" => Describe(_manager.CreateInstance(root.GetProperty("widgetTypeId").GetString()!)),

            "widget.move" => HandleMove(root),

            "widget.resize" => HandleResize(root),

            "widget.setEnabled" => HandleSetEnabled(root),

            "widget.remove" => new { removed = _manager.RemoveInstance(root.GetProperty("instanceId").GetString()!) },

            "widget.render" => await _manager.RenderAsync(root.GetProperty("instanceId").GetString()!, cancellationToken),

            _ => throw new NotSupportedException($"Unbekannter MessageType '{message.MessageType}' für {ModuleId}."),
        };

        if (message.MessageType is "widget.create" or "widget.move" or "widget.resize" or "widget.setEnabled" or "widget.remove")
        {
            await _manager.SaveAsync(cancellationToken);
        }

        return message with
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SourceModuleId = ModuleId,
            TargetModuleId = message.SourceModuleId,
            MessageType = $"{message.MessageType}.result",
            PayloadJson = JsonSerializer.Serialize(result, NwosJson.ResponseOptions),
        };
    }

    private object HandleMove(JsonElement root)
    {
        var instanceId = root.GetProperty("instanceId").GetString()!;
        _manager.Move(instanceId, root.GetProperty("x").GetInt32(), root.GetProperty("y").GetInt32());
        return Describe(GetInstanceOrThrow(instanceId));
    }

    private object HandleResize(JsonElement root)
    {
        var instanceId = root.GetProperty("instanceId").GetString()!;
        _manager.Resize(instanceId, root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32());
        return Describe(GetInstanceOrThrow(instanceId));
    }

    private object HandleSetEnabled(JsonElement root)
    {
        var instanceId = root.GetProperty("instanceId").GetString()!;
        _manager.SetEnabled(instanceId, root.GetProperty("isEnabled").GetBoolean());
        return Describe(GetInstanceOrThrow(instanceId));
    }

    private WidgetInstance GetInstanceOrThrow(string instanceId) =>
        _manager.TryGetInstance(instanceId, out var instance)
            ? instance
            : throw new InvalidOperationException($"Widget-Instanz '{instanceId}' existiert nicht.");

    private static object Describe(WidgetInstance instance) => new
    {
        instance.InstanceId,
        instance.WidgetTypeId,
        instance.PositionX,
        instance.PositionY,
        instance.Width,
        instance.Height,
        instance.IsEnabled,
    };
}
