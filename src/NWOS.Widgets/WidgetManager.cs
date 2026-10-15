using System.Text.Json;

namespace NWOS.Widgets;

/// <summary>
/// Verwaltet platzierte Widget-Instanzen. Validiert bei der Erstellung gegen
/// die <see cref="WidgetRegistry"/> - eine Instanz eines nicht registrierten
/// Widget-Typs kann NICHT erzeugt werden. Das verhindert "Geister-Widgets",
/// die später keinen Provider zum Rendern finden (Abschnitt 40: keine
/// Fake-Funktionen).
/// </summary>
public sealed class WidgetManager
{
    private readonly WidgetRegistry _registry;
    private readonly string _storageFilePath;
    private readonly Dictionary<string, WidgetInstance> _instances = new();

    public WidgetManager(WidgetRegistry registry, string storageDirectory)
    {
        _registry = registry;
        Directory.CreateDirectory(storageDirectory);
        _storageFilePath = Path.Combine(storageDirectory, "widgets.json");
    }

    public WidgetInstance CreateInstance(string widgetTypeId)
    {
        if (!_registry.TryGetProvider(widgetTypeId, out var provider))
        {
            throw new InvalidOperationException(
                $"Widget-Typ '{widgetTypeId}' ist nicht registriert - Instanz kann nicht erzeugt werden.");
        }

        var instance = new WidgetInstance(Guid.NewGuid().ToString("N"), widgetTypeId, provider.DefaultWidth, provider.DefaultHeight);
        _instances[instance.InstanceId] = instance;
        return instance;
    }

    public bool TryGetInstance(string instanceId, out WidgetInstance instance) =>
        _instances.TryGetValue(instanceId, out instance!);

    public bool RemoveInstance(string instanceId) => _instances.Remove(instanceId);

    public IReadOnlyCollection<WidgetInstance> AllInstances => _instances.Values;

    public void Move(string instanceId, int x, int y)
    {
        var instance = GetOrThrow(instanceId);
        instance.PositionX = x;
        instance.PositionY = y;
    }

    public void Resize(string instanceId, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Breite und Höhe müssen positiv sein.");
        }

        var instance = GetOrThrow(instanceId);
        instance.Width = width;
        instance.Height = height;
    }

    public void SetEnabled(string instanceId, bool isEnabled) => GetOrThrow(instanceId).IsEnabled = isEnabled;

    /// <summary>
    /// Ruft live die aktuellen Anzeigedaten über den zuständigen Provider ab.
    /// Ein deaktiviertes Widget wird nicht gerendert (spart Ressourcen,
    /// Abschnitt 35), sondern liefert <c>null</c> zurück.
    /// </summary>
    public async Task<WidgetSnapshot?> RenderAsync(string instanceId, CancellationToken cancellationToken)
    {
        var instance = GetOrThrow(instanceId);
        if (!instance.IsEnabled)
        {
            return null;
        }

        if (!_registry.TryGetProvider(instance.WidgetTypeId, out var provider))
        {
            throw new InvalidOperationException(
                $"Provider für Widget-Typ '{instance.WidgetTypeId}' wurde nach Instanzerstellung entfernt.");
        }

        return await provider.RenderAsync(cancellationToken);
    }

    private WidgetInstance GetOrThrow(string instanceId) =>
        _instances.TryGetValue(instanceId, out var instance)
            ? instance
            : throw new InvalidOperationException($"Widget-Instanz '{instanceId}' existiert nicht.");

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storageFilePath))
        {
            return;
        }

        await using var stream = File.OpenRead(_storageFilePath);
        var records = await JsonSerializer.DeserializeAsync<List<InstanceRecord>>(stream, cancellationToken: cancellationToken)
            ?? new List<InstanceRecord>();

        _instances.Clear();
        foreach (var record in records)
        {
            var instance = new WidgetInstance(record.InstanceId, record.WidgetTypeId, record.Width, record.Height)
            {
                PositionX = record.PositionX,
                PositionY = record.PositionY,
                IsEnabled = record.IsEnabled,
            };

            foreach (var (key, value) in record.Settings)
            {
                instance.Settings[key] = value;
            }

            _instances[instance.InstanceId] = instance;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        var records = _instances.Values.Select(i => new InstanceRecord(
            i.InstanceId, i.WidgetTypeId, i.PositionX, i.PositionY, i.Width, i.Height, i.IsEnabled,
            new Dictionary<string, string>(i.Settings))).ToList();

        await using var stream = File.Create(_storageFilePath);
        await JsonSerializer.SerializeAsync(stream, records, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private sealed record InstanceRecord(
        string InstanceId,
        string WidgetTypeId,
        int PositionX,
        int PositionY,
        int Width,
        int Height,
        bool IsEnabled,
        Dictionary<string, string> Settings);
}
