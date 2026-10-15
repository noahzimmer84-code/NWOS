using Xunit;

namespace NWOS.Alice.Tests;

public class HeuristicEngineTests
{
    private readonly HeuristicEngine _engine = new();

    [Theory]
    [InlineData(@"C:\Users\Test\Downloads\rechnung.pdf.exe")]
    [InlineData(@"C:\Users\Test\Downloads\foto.jpg.scr")]
    public void Detects_double_extension_disguised_executable(string path)
    {
        var events = _engine.EvaluateFileCreatedOrChanged(path);

        Assert.Contains(events, e => e.RuleId == "double-extension-executable" && e.Severity == ThreatSeverity.Critical);
    }

    [Theory]
    [InlineData(@"C:\Users\Test\Downloads\setup.exe")]
    [InlineData(@"C:\Users\Test\Documents\report.docx")]
    public void Does_not_flag_normal_files_as_double_extension(string path)
    {
        var events = _engine.EvaluateFileCreatedOrChanged(path);

        Assert.DoesNotContain(events, e => e.RuleId == "double-extension-executable");
    }

    [Fact]
    public void Flags_new_executable_in_downloads_as_info_only()
    {
        var events = _engine.EvaluateFileCreatedOrChanged(@"C:\Users\Test\Downloads\installer.exe");

        var evt = Assert.Single(events, e => e.RuleId == "new-executable-in-downloads");
        Assert.Equal(ThreatSeverity.Info, evt.Severity);
        Assert.False(evt.AutoActionTaken);
    }

    [Fact]
    public void Does_not_flag_executable_outside_downloads_as_download_rule()
    {
        var events = _engine.EvaluateFileCreatedOrChanged(@"C:\Program Files\App\tool.exe");

        Assert.DoesNotContain(events, e => e.RuleId == "new-executable-in-downloads");
    }

    [Fact]
    public void Regular_document_triggers_no_rules_at_all()
    {
        var events = _engine.EvaluateFileCreatedOrChanged(@"C:\Users\Test\Documents\budget.xlsx");

        Assert.Empty(events);
    }
}
