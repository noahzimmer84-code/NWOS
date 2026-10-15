namespace NWOS.DesktopShell;

public sealed record SearchResult(string Path, string DisplayName, SearchResultKind Kind);

public enum SearchResultKind
{
    File,
    Folder,
}

/// <summary>
/// Zentrale, lokale Suche (Architekturdokument Abschnitt 20). Für den
/// Grundgerüst-Stand ein einfacher In-Memory-Index über einen konfigurierten
/// Wurzelordner - bewusst simpel gehalten, damit er nachvollziehbar korrekt
/// ist. Skalierung auf sehr große Dateisysteme (z. B. inkrementelle Updates
/// über einen Dateisystem-Watcher statt vollständigem Rebuild, Volltextsuche
/// in Dokumenteninhalten) ist als TODO markiert statt hier vorgetäuscht zu
/// werden - Abschnitt 20 verlangt nur, dass die Suche "einen lokalen Index
/// verwendet, wenn dies die Performance verbessert", nicht eine bestimmte
/// Indexierungstechnologie.
///
/// TODO(Phase 2, Ausbau): Dateisystem-Watcher (FileSystemWatcher) für
/// inkrementelle Aktualisierung statt Reindex bei jedem RebuildAsync-Aufruf -
/// wichtig für den Performance-Grundsatz aus Abschnitt 35 ("keine unnötigen
/// Polling-Schleifen"), aktuell wird bewusst noch nicht automatisch/periodisch
/// reindiziert.
/// </summary>
public sealed class SearchIndex
{
    private readonly List<SearchResult> _entries = new();
    private readonly object _lock = new();

    public int EntryCount
    {
        get { lock (_lock) { return _entries.Count; } }
    }

    /// <summary>
    /// Baut den Index für einen Wurzelordner neu auf. Nicht lesbare
    /// Unterordner (Berechtigungsfehler) werden übersprungen statt den
    /// gesamten Vorgang abzubrechen - eine einzelne unzugängliche Fläche
    /// darf die Suche nicht komplett lahmlegen.
    /// </summary>
    public Task RebuildAsync(string rootDirectory, CancellationToken cancellationToken)
    {
        var collected = new List<SearchResult>();
        CollectRecursive(rootDirectory, collected, cancellationToken);

        lock (_lock)
        {
            _entries.Clear();
            _entries.AddRange(collected);
        }

        return Task.CompletedTask;
    }

    private static void CollectRecursive(string directory, List<SearchResult> collected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> subDirectories;
        IEnumerable<string> files;
        try
        {
            subDirectories = Directory.EnumerateDirectories(directory);
            files = Directory.EnumerateFiles(directory);
        }
        catch (UnauthorizedAccessException)
        {
            return; // Abschnitt-35-Grundsatz: robust statt abbrechend.
        }
        catch (IOException)
        {
            return;
        }

        foreach (var file in files)
        {
            collected.Add(new SearchResult(file, Path.GetFileName(file), SearchResultKind.File));
        }

        foreach (var subDirectory in subDirectories)
        {
            collected.Add(new SearchResult(subDirectory, Path.GetFileName(subDirectory), SearchResultKind.Folder));
            CollectRecursive(subDirectory, collected, cancellationToken);
        }
    }

    /// <summary>Einfache, fallunabhängige Teilstring-Suche über Dateinamen.</summary>
    public IReadOnlyList<SearchResult> Search(string query, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResult>();
        }

        lock (_lock)
        {
            return _entries
                .Where(e => e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults)
                .ToList();
        }
    }
}
