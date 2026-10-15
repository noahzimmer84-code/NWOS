using Xunit;

namespace NWOS.DesktopShell.Tests;

public class SearchIndexTests
{
    [Fact]
    public async Task RebuildAsync_indexes_files_and_folders_recursively()
    {
        var root = CreateTempDirWithFiles();
        var index = new SearchIndex();

        await index.RebuildAsync(root, CancellationToken.None);

        Assert.True(index.EntryCount >= 3); // 2 Dateien + 1 Unterordner, siehe CreateTempDirWithFiles
    }

    [Fact]
    public async Task Search_finds_file_by_partial_case_insensitive_name()
    {
        var root = CreateTempDirWithFiles();
        var index = new SearchIndex();
        await index.RebuildAsync(root, CancellationToken.None);

        var results = index.Search("REPORT");

        Assert.Contains(results, r => r.DisplayName == "report.docx");
    }

    [Fact]
    public async Task Search_does_not_return_non_matching_entries()
    {
        var root = CreateTempDirWithFiles();
        var index = new SearchIndex();
        await index.RebuildAsync(root, CancellationToken.None);

        var results = index.Search("xyz-nicht-vorhanden");

        Assert.Empty(results);
    }

    [Fact]
    public void Search_with_empty_query_returns_no_results_instead_of_everything()
    {
        // Bewusstes Verhalten: eine leere globale Suche soll nicht versehentlich
        // tausende Ergebnisse zurückgeben (Performance/UX), anders als die
        // Box-interne Suche, die bei leerer Query bewusst alles zeigt.
        var index = new SearchIndex();
        Assert.Empty(index.Search(""));
    }

    private static string CreateTempDirWithFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "nwos-search-tests-" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(root, "Dokumente");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(root, "report.docx"), "dummy");
        File.WriteAllText(Path.Combine(subDir, "budget.xlsx"), "dummy");

        return root;
    }
}
