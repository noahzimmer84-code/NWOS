using System.Text.Json;

namespace NWOS.Settings;

/// <summary>
/// Zentrales Einstellungszentrum (Architekturdokument Abschnitt 33: "Dort
/// müssen sämtliche NWOS-Funktionen konfigurierbar sein"). Bewusst als
/// eigenständiges, kleines Modul ohne Abhängigkeit auf NWOS.Cardinal oder ein
/// bestimmtes Fach-Modul - jedes Modul (Desktop, Alice, Marc, ...) legt seine
/// Einstellungen unter einem eigenen, mit seiner ModuleId präfixierten
/// Abschnitt ab. So kann Settings unabhängig von der Anzahl/Art der
/// installierten Module funktionieren (Abschnitt 4: Modularität).
///
/// Nur lokale Speicherung (Abschnitt 34) - kein Modul, das über diesen Store
/// Einstellungen liest oder schreibt, sendet dabei Daten nach außen.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _settingsFilePath;
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new();
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public SettingsStore(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return;
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, string>>>(
            stream, cancellationToken: cancellationToken);

        if (loaded is null)
        {
            return;
        }

        _sections.Clear();
        foreach (var (section, values) in loaded)
        {
            _sections[section] = values;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, _sections, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Liest einen Einstellungswert. Gibt <paramref name="defaultValue"/>
    /// zurück, wenn er nicht existiert - kein Modul muss also seinen eigenen
    /// Erststart-Zustand kennen, um Settings sicher zu benutzen.
    /// </summary>
    public string GetValue(string moduleId, string key, string defaultValue)
    {
        if (_sections.TryGetValue(moduleId, out var section) && section.TryGetValue(key, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public void SetValue(string moduleId, string key, string value)
    {
        if (!_sections.TryGetValue(moduleId, out var section))
        {
            section = new Dictionary<string, string>();
            _sections[moduleId] = section;
        }

        section[key] = value;
    }

    /// <summary>Alle Einstellungen eines Moduls, z. B. für eine Settings-UI-Ansicht.</summary>
    public IReadOnlyDictionary<string, string> GetSection(string moduleId) =>
        _sections.TryGetValue(moduleId, out var section)
            ? section
            : new Dictionary<string, string>();
}
