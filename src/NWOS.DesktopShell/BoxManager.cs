using System.Text.Json;

namespace NWOS.DesktopShell;

/// <summary>
/// Verwaltet die Menge aller Smart Boxes eines Nutzers (Abschnitt 17/23).
/// Persistiert lokal (Abschnitt 34). Enthält bewusst keine UI-Logik (Drag&amp;Drop-
/// Erkennung, Hover-Events) - das ist Aufgabe der späteren UI-Schicht, die
/// diese Klasse als Datenmodell verwendet.
/// </summary>
public sealed class BoxManager
{
    private readonly string _storageFilePath;
    private readonly Dictionary<string, SmartBox> _boxes = new();

    public BoxManager(string storageDirectory)
    {
        Directory.CreateDirectory(storageDirectory);
        _storageFilePath = Path.Combine(storageDirectory, "smartboxes.json");
    }

    public SmartBox CreateBox(string name)
    {
        var box = SmartBox.Create(name);
        _boxes[box.BoxId] = box;
        return box;
    }

    public bool TryGetBox(string boxId, out SmartBox box) =>
        _boxes.TryGetValue(boxId, out box!);

    public bool DeleteBox(string boxId) => _boxes.Remove(boxId);

    public IReadOnlyCollection<SmartBox> AllBoxes => _boxes.Values;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storageFilePath))
        {
            return;
        }

        await using var stream = File.OpenRead(_storageFilePath);
        var records = await JsonSerializer.DeserializeAsync<List<BoxRecord>>(stream, cancellationToken: cancellationToken)
            ?? new List<BoxRecord>();

        _boxes.Clear();
        foreach (var record in records)
        {
            var box = new SmartBox(record.BoxId, record.Name)
            {
                DisplayMode = record.DisplayMode,
                Width = record.Width,
                Height = record.Height,
                PositionX = record.PositionX,
                PositionY = record.PositionY,
            };

            foreach (var item in record.Items)
            {
                box.AddItem(item.SourcePath, item.Kind);
            }

            _boxes[box.BoxId] = box;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        var records = _boxes.Values.Select(b => new BoxRecord(
            b.BoxId, b.Name, b.DisplayMode, b.Width, b.Height, b.PositionX, b.PositionY, b.Items.ToList())).ToList();

        await using var stream = File.Create(_storageFilePath);
        await JsonSerializer.SerializeAsync(stream, records, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private sealed record BoxRecord(
        string BoxId,
        string Name,
        BoxDisplayMode DisplayMode,
        int Width,
        int Height,
        int PositionX,
        int PositionY,
        List<BoxItem> Items);
}
