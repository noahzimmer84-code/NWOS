using Xunit;

namespace NWOS.SystemCenter.Tests;

public class TempCleanerTests
{
    [Fact]
    public void Deletes_old_files_and_reports_bytes_freed()
    {
        var dir = CreateTempDir();
        var oldFile = Path.Combine(dir, "old.tmp");
        File.WriteAllText(oldFile, "0123456789"); // 10 Bytes
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-2));

        var result = new TempCleaner().Clean(dir, TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal(1, result.FilesDeleted);
        Assert.Equal(10, result.BytesFreed);
        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public void Leaves_recently_modified_files_untouched()
    {
        var dir = CreateTempDir();
        var recentFile = Path.Combine(dir, "recent.tmp");
        File.WriteAllText(recentFile, "x");
        File.SetLastWriteTimeUtc(recentFile, DateTime.UtcNow);

        var result = new TempCleaner().Clean(dir, TimeSpan.FromHours(24), CancellationToken.None);

        Assert.Equal(0, result.FilesDeleted);
        Assert.True(File.Exists(recentFile));
    }

    [Fact]
    public void Returns_empty_result_for_missing_directory_instead_of_throwing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid());

        var result = new TempCleaner().Clean(missing, TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.BytesFreed);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-sysctr-tempcleaner-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public class ProtectedProcessListTests
{
    [Theory]
    [InlineData("explorer")]
    [InlineData("svchost")]
    [InlineData("lsass")]
    public void Well_known_critical_processes_are_protected(string name)
    {
        Assert.True(ProtectedProcessList.IsProtected(name));
    }

    [Fact]
    public void Protection_check_is_case_insensitive()
    {
        Assert.True(ProtectedProcessList.IsProtected("EXPLORER"));
    }

    [Fact]
    public void Arbitrary_user_application_is_not_protected()
    {
        Assert.False(ProtectedProcessList.IsProtected("notepad-plus-plus-totally-not-real"));
    }
}
