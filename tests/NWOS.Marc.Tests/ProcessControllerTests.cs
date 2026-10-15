using Xunit;

namespace NWOS.Marc.Tests;

public class ProcessControllerTests
{
    [Fact]
    public void LaunchApplication_throws_for_missing_executable()
    {
        var controller = new ProcessController();
        var missingPath = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".exe");

        Assert.Throws<FileNotFoundException>(() => controller.LaunchApplication(missingPath));
    }

    [Fact]
    public void CloseApplication_returns_false_for_nonexistent_process_id()
    {
        var controller = new ProcessController();

        // Eine sehr unwahrscheinliche PID - Ziel ist ArgumentException aus
        // Process.GetProcessById abzufangen, nicht einen echten Prozess zu testen
        // (das wäre plattform-/timingabhängig und gehört in einen manuellen Test
        // auf echtem Windows, siehe README).
        var result = controller.CloseApplication(int.MaxValue - 1);

        Assert.False(result);
    }
}
