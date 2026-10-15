namespace NWOS.Marc;

public sealed record OrganizeResult(int FilesMoved, IReadOnlyList<string> Errors);

/// <summary>
/// Ordnet Dateien in einem Ordner nach Dateityp in Unterordner ein
/// (Architekturdokument Abschnitt 9: "Dateien organisieren"). Bewusst nur
/// EIN Regelwerk (Gruppierung nach Dateiendung) statt konfigurierbarer
/// Regeln - komplexere Regeln (nach Datum, nach Inhalt, benutzerdefiniert)
/// sind ein TODO für eine spätere Ausbaustufe, sobald dafür eine echte
/// Konfigurationsoberfläche existiert (kein Fake-Konfigurationsfeld ohne
/// Wirkung, Abschnitt 40).
/// </summary>
public sealed class FileOrganizer
{
    private static readonly Dictionary<string, string> ExtensionToFolder = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "Bilder", [".jpeg"] = "Bilder", [".png"] = "Bilder", [".gif"] = "Bilder", [".webp"] = "Bilder",
        [".pdf"] = "Dokumente", [".docx"] = "Dokumente", [".doc"] = "Dokumente", [".txt"] = "Dokumente",
        [".xlsx"] = "Tabellen", [".xls"] = "Tabellen", [".csv"] = "Tabellen",
        [".zip"] = "Archive", [".rar"] = "Archive", [".7z"] = "Archive",
        [".mp3"] = "Audio", [".wav"] = "Audio", [".flac"] = "Audio",
        [".mp4"] = "Video", [".mkv"] = "Video", [".mov"] = "Video",
    };

    /// <summary>
    /// Verschiebt jede Datei direkt in <paramref name="folderPath"/> (nicht
    /// rekursiv - bereits einsortierte Unterordner werden nicht erneut
    /// durchsucht) in einen passenden Unterordner. Dateien mit unbekannter
    /// Endung bleiben unverändert liegen, statt in einen Sammel-Ordner
    /// "Sonstiges" verschoben zu werden - das wäre eine Annahme, die der
    /// Auftrag nicht trifft.
    /// </summary>
    public OrganizeResult Organize(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Ordner '{folderPath}' existiert nicht.");
        }

        var errors = new List<string>();
        var movedCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(folderPath))
        {
            var extension = Path.GetExtension(filePath);
            if (!ExtensionToFolder.TryGetValue(extension, out var targetFolderName))
            {
                continue;
            }

            try
            {
                var targetFolder = Path.Combine(folderPath, targetFolderName);
                Directory.CreateDirectory(targetFolder);

                var destination = Path.Combine(targetFolder, Path.GetFileName(filePath));
                if (File.Exists(destination))
                {
                    // Keine stille Überschreibung fremder Dateien.
                    destination = Path.Combine(targetFolder,
                        $"{Path.GetFileNameWithoutExtension(filePath)}_{Guid.NewGuid():N}{extension}");
                }

                File.Move(filePath, destination);
                movedCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Abschnitt 36-Grundsatz: eine blockierte/gesperrte Datei darf
                // den restlichen Organisationsvorgang nicht abbrechen.
                errors.Add($"{filePath}: {ex.Message}");
            }
        }

        return new OrganizeResult(movedCount, errors);
    }
}
