using Xunit;

namespace NWOS.Marc.Tests;

public class FileOrganizerTests
{
    [Fact]
    public void Organize_moves_known_extensions_into_matching_subfolders()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "urlaub.jpg"), "x");
        File.WriteAllText(Path.Combine(dir, "vertrag.pdf"), "x");

        var result = new FileOrganizer().Organize(dir);

        Assert.Equal(2, result.FilesMoved);
        Assert.True(File.Exists(Path.Combine(dir, "Bilder", "urlaub.jpg")));
        Assert.True(File.Exists(Path.Combine(dir, "Dokumente", "vertrag.pdf")));
    }

    [Fact]
    public void Organize_leaves_files_with_unknown_extension_untouched()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "custom.xyz");
        File.WriteAllText(path, "x");

        var result = new FileOrganizer().Organize(dir);

        Assert.Equal(0, result.FilesMoved);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Organize_does_not_overwrite_existing_file_with_same_name_in_target()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "Bilder"));
        File.WriteAllText(Path.Combine(dir, "Bilder", "foto.jpg"), "original");
        File.WriteAllText(Path.Combine(dir, "foto.jpg"), "neu");

        new FileOrganizer().Organize(dir);

        Assert.Equal("original", File.ReadAllText(Path.Combine(dir, "Bilder", "foto.jpg")));
        // Die neue Datei muss trotzdem irgendwo (unter neuem Namen) gelandet sein, nicht verloren gegangen sein.
        Assert.True(Directory.EnumerateFiles(Path.Combine(dir, "Bilder")).Count() >= 2);
    }

    [Fact]
    public void Organize_throws_for_missing_directory()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => new FileOrganizer().Organize(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid())));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-marc-organize-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public class FileTaskExecutorTests
{
    [Fact]
    public void CreateFolder_creates_nested_directories()
    {
        var root = CreateTempDir();
        var target = Path.Combine(root, "a", "b", "c");

        new FileTaskExecutor().CreateFolder(target);

        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public void RenameFile_renames_within_same_directory()
    {
        var dir = CreateTempDir();
        var original = Path.Combine(dir, "old.txt");
        File.WriteAllText(original, "content");

        var newPath = new FileTaskExecutor().RenameFile(original, "new.txt");

        Assert.False(File.Exists(original));
        Assert.True(File.Exists(newPath));
        Assert.Equal("content", File.ReadAllText(newPath));
    }

    [Fact]
    public void RenameFile_throws_when_source_missing()
    {
        var dir = CreateTempDir();
        Assert.Throws<FileNotFoundException>(
            () => new FileTaskExecutor().RenameFile(Path.Combine(dir, "missing.txt"), "new.txt"));
    }

    [Fact]
    public void RenameFile_throws_when_target_already_exists()
    {
        var dir = CreateTempDir();
        var source = Path.Combine(dir, "a.txt");
        var existingTarget = Path.Combine(dir, "b.txt");
        File.WriteAllText(source, "a");
        File.WriteAllText(existingTarget, "b");

        Assert.Throws<IOException>(() => new FileTaskExecutor().RenameFile(source, "b.txt"));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-marc-filetasks-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
