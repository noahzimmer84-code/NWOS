namespace NWOS.SystemCenter;

public sealed record CleanupResult(int FilesDeleted, long BytesFreed, IReadOnlyList<string> SkippedFiles);

/// <summary>
/// Bereinigt temporäre Dateien (Architekturdokument Abschnitt 25:
/// "temporäre Daten bereinigen", "Cache bereinigen"). Löscht bewusst nur
/// Dateien, die älter als <paramref name="minAge"/> sind, und überspringt
/// gesperrte/in Benutzung befindliche Dateien statt einen Fehler zu werfen -
/// eine gerade von einem laufenden Programm genutzte Temp-Datei zu löschen
/// wäre ein Stabilitätsrisiko (Abschnitt 43: Stabilität vor Funktionsumfang).
/// </summary>
public sealed class TempCleaner
{
    public CleanupResult Clean(string directory, TimeSpan minAge, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return new CleanupResult(0, 0, Array.Empty<string>());
        }

        var cutoff = DateTime.UtcNow - minAge;
        var deletedCount = 0;
        long bytesFreed = 0;
        var skipped = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(filePath);
                if (info.LastWriteTimeUtc > cutoff)
                {
                    continue; // Zu neu - könnte noch in Benutzung sein.
                }

                var size = info.Length;
                info.Delete();
                deletedCount++;
                bytesFreed += size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped.Add(filePath);
            }
        }

        return new CleanupResult(deletedCount, bytesFreed, skipped);
    }
}
