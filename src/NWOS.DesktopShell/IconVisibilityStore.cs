using System.Text.Json;

namespace NWOS.DesktopShell;

/// <summary>
/// Verwaltet ausschließlich die Sichtbarkeit von Desktop-Icons
/// (Architekturdokument Abschnitt 16: "Durch Doppelklick können sie
/// ausgeblendet werden. Durch erneuten Doppelklick können sie wieder
/// eingeblendet werden. Dabei dürfen Position, Verknüpfung, Datei, Ordner,
/// Einstellungen nicht verändert werden.").
///
/// Diese Klasse fasst deshalb absichtlich NUR eine Sichtbarkeits-Flag pro
/// Icon-Id an - nie den Zielpfad, nie eine Windows-Verknüpfungsdatei, nie
/// Positionsdaten. Persistiert lokal als JSON (Abschnitt 34).
/// </summary>
public sealed class IconVisibilityStore
{
    private readonly string _stateFilePath;
    private readonly Dictionary<string, bool> _visibilityByIconId = new();
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public IconVisibilityStore(string stateDirectory)
    {
        Directory.CreateDirectory(stateDirectory);
        _stateFilePath = Path.Combine(stateDirectory, "icon-visibility.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_stateFilePath))
        {
            return;
        }

        await using var stream = File.OpenRead(_stateFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(stream, cancellationToken: cancellationToken);
        if (loaded is null)
        {
            return;
        }

        _visibilityByIconId.Clear();
        foreach (var (key, value) in loaded)
        {
            _visibilityByIconId[key] = value;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_stateFilePath);
        await JsonSerializer.SerializeAsync(stream, _visibilityByIconId, SerializerOptions, cancellationToken);
    }

    /// <summary>Alle gespeicherten Zustände (IconId -> sichtbar). Nicht gelistete Icons gelten als sichtbar.</summary>
    public IReadOnlyDictionary<string, bool> GetAllStates() => _visibilityByIconId;

    /// <summary>Icons sind standardmäßig sichtbar, solange kein Toggle gespeichert wurde.</summary>
    public bool IsVisible(string iconId) => _visibilityByIconId.GetValueOrDefault(iconId, true);

    /// <summary>
    /// Kehrt die Sichtbarkeit um (Doppelklick-Verhalten). Gibt den neuen
    /// Zustand zurück, damit die UI-Schicht sofort reagieren kann, ohne
    /// zusätzlich nachzufragen.
    /// </summary>
    public bool ToggleVisibility(string iconId)
    {
        var newValue = !IsVisible(iconId);
        _visibilityByIconId[iconId] = newValue;
        return newValue;
    }
}
