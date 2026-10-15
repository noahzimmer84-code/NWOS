using System.Text.Json;

namespace NWOS.Alice;

/// <summary>
/// Isoliert eine als kritisch eingestufte Datei, ohne sie zu löschen -
/// Architekturdokument Abschnitt 8 verlangt Erkennung/Isolierung, Abschnitt 41
/// "Sicherheit vor Komfort" schließt aber ein, dass eine falsch-positive
/// Erkennung reversibel sein muss. Verschieben statt Löschen ist deshalb
/// bewusst die einzige unterstützte Aktion in diesem Entwicklungsstand.
/// </summary>
public sealed class QuarantineService
{
    private readonly string _quarantineDirectory;
    private readonly string _manifestPath;
    private readonly Dictionary<string, string> _originalPathByQuarantineId = new();

    public QuarantineService(string stateDirectory)
    {
        _quarantineDirectory = Path.Combine(stateDirectory, "quarantine");
        Directory.CreateDirectory(_quarantineDirectory);
        _manifestPath = Path.Combine(_quarantineDirectory, "manifest.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_manifestPath))
        {
            return;
        }

        await using var stream = File.OpenRead(_manifestPath);
        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken);
        if (loaded is null)
        {
            return;
        }

        _originalPathByQuarantineId.Clear();
        foreach (var (key, value) in loaded)
        {
            _originalPathByQuarantineId[key] = value;
        }
    }

    /// <summary>Verschiebt die Datei in den Quarantäne-Ordner. Gibt die Quarantäne-Id zurück (für spätere Wiederherstellung).</summary>
    public async Task<string> QuarantineAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Zu isolierende Datei wurde nicht gefunden - möglicherweise bereits verschoben/gelöscht.", filePath);
        }

        var quarantineId = Guid.NewGuid().ToString("N");
        var quarantinedFileName = quarantineId + Path.GetExtension(filePath) + ".quarantined";
        var destination = Path.Combine(_quarantineDirectory, quarantinedFileName);

        File.Move(filePath, destination);
        _originalPathByQuarantineId[quarantineId] = filePath;
        await SaveManifestAsync(cancellationToken);

        return quarantineId;
    }

    /// <summary>Stellt eine zuvor isolierte Datei an ihrem ursprünglichen Ort wieder her.</summary>
    public async Task RestoreAsync(string quarantineId, CancellationToken cancellationToken)
    {
        if (!_originalPathByQuarantineId.TryGetValue(quarantineId, out var originalPath))
        {
            throw new InvalidOperationException($"Keine Quarantäne-Datei mit Id '{quarantineId}' bekannt.");
        }

        var quarantinedFile = Directory
            .EnumerateFiles(_quarantineDirectory)
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith(quarantineId, StringComparison.Ordinal));

        if (quarantinedFile is null)
        {
            throw new FileNotFoundException("Quarantäne-Datei fehlt physisch, obwohl im Manifest bekannt.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(originalPath) ?? ".");
        File.Move(quarantinedFile, originalPath, overwrite: false);

        _originalPathByQuarantineId.Remove(quarantineId);
        await SaveManifestAsync(cancellationToken);
    }

    public IReadOnlyDictionary<string, string> AllQuarantined => _originalPathByQuarantineId;

    private async Task SaveManifestAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_manifestPath);
        await JsonSerializer.SerializeAsync(stream, _originalPathByQuarantineId, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}
