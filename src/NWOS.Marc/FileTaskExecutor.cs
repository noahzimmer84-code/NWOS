namespace NWOS.Marc;

/// <summary>
/// Einfache, direkte Dateisystem-Operationen (Architekturdokument Abschnitt 9:
/// "Ordner erstellen", "Dateien umbenennen"). Bewusst ohne Rückgängig-Funktion
/// in diesem Entwicklungsstand - TODO(Phase 4+): Aktionsprotokoll mit
/// Undo-Fähigkeit, analog zum Audit-Log von Alice, sobald das für Marc
/// gebraucht wird.
/// </summary>
public sealed class FileTaskExecutor
{
    public void CreateFolder(string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <summary>Benennt eine Datei innerhalb desselben Ordners um (kein Verschieben zwischen Laufwerken/Ordnern).</summary>
    public string RenameFile(string filePath, string newFileName)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Datei nicht gefunden.", filePath);
        }

        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"Konnte Verzeichnis von '{filePath}' nicht bestimmen.");

        var destination = Path.Combine(directory, newFileName);
        if (File.Exists(destination))
        {
            throw new IOException($"Zieldatei '{destination}' existiert bereits.");
        }

        File.Move(filePath, destination);
        return destination;
    }
}
