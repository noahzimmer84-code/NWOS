namespace NWOS.DesktopShell;

public enum BoxDisplayMode
{
    /// <summary>Box öffnet bei Mouseover, schließt beim Verlassen (Abschnitt 18).</summary>
    Hover,

    /// <summary>Box bleibt dauerhaft geöffnet, bis der Nutzer sie manuell schließt (Abschnitt 18).</summary>
    Static,
}

public sealed record BoxItem(
    string ItemId,
    string SourcePath,
    BoxItemKind Kind,
    DateTimeOffset AddedAt);

public enum BoxItemKind
{
    File,
    Folder,
    Shortcut,
    Widget,
}

/// <summary>
/// Eine Smart Box (Architekturdokument Abschnitt 17). Für Phase 2 bewusst
/// OHNE Passwortschutz (laut Entwicklungsplan Abschnitt 13: "einfache Boxen
/// (ohne Passwortschutz zunächst)") - das Feld ist vorbereitet, aber die
/// Durchsetzung kommt erst, wenn sie auch wirklich sicher implementiert und
/// getestet werden kann (kein Fake-Passwortfeld, Abschnitt 40).
/// </summary>
public sealed class SmartBox
{
    public string BoxId { get; }
    public string Name { get; set; }
    public BoxDisplayMode DisplayMode { get; set; } = BoxDisplayMode.Hover;
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 240;
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // TODO(Phase 2, nach Grundgerüst): Passwortschutz mit sicherem Hashing
    // (z. B. PBKDF2) implementieren, bevor dieses Feld über die UI gesetzt
    // werden kann. Bis dahin bleibt es ungenutzt statt eine Sicherheitsfunktion
    // vorzutäuschen, die nicht wirklich schützt.
    public string? PasswordHashPlaceholder { get; set; }

    private readonly List<BoxItem> _items = new();
    public IReadOnlyList<BoxItem> Items => _items;

    public SmartBox(string boxId, string name)
    {
        BoxId = boxId;
        Name = name;
    }

    public static SmartBox Create(string name) => new(Guid.NewGuid().ToString("N"), name);

    public BoxItem AddItem(string sourcePath, BoxItemKind kind)
    {
        var item = new BoxItem(Guid.NewGuid().ToString("N"), sourcePath, kind, DateTimeOffset.UtcNow);
        _items.Add(item);
        return item;
    }

    public bool RemoveItem(string itemId) =>
        _items.RemoveAll(i => i.ItemId == itemId) > 0;

    /// <summary>
    /// Durchsucht ausschließlich den Inhalt dieser Box (Abschnitt 19: "Die
    /// integrierte Suchfunktion durchsucht ausschließlich den Inhalt der
    /// jeweiligen Box.") - bewusst getrennt von der globalen Suche in
    /// <see cref="SearchIndex"/>.
    /// </summary>
    public IEnumerable<BoxItem> SearchContent(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Items;
        }

        return Items.Where(i =>
            Path.GetFileName(i.SourcePath).Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
