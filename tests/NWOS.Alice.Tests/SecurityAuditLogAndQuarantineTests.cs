using Xunit;

namespace NWOS.Alice.Tests;

public class SecurityAuditLogTests
{
    [Fact]
    public async Task AppendAsync_then_GetRecent_returns_event()
    {
        var log = new SecurityAuditLog(CreateTempDir());
        var evt = ThreatEvent.Create("C:\\a.exe", "test-rule", "test", ThreatSeverity.Info, false);

        await log.AppendAsync(evt, CancellationToken.None);

        Assert.Contains(log.GetRecent(10), e => e.EventId == evt.EventId);
    }

    [Fact]
    public async Task GetRecent_respects_count_and_orders_newest_first()
    {
        var log = new SecurityAuditLog(CreateTempDir());
        var first = ThreatEvent.Create("a", "r", "e", ThreatSeverity.Info, false) with { DetectedAt = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var second = ThreatEvent.Create("b", "r", "e", ThreatSeverity.Info, false) with { DetectedAt = DateTimeOffset.UtcNow };

        await log.AppendAsync(first, CancellationToken.None);
        await log.AppendAsync(second, CancellationToken.None);

        var recent = log.GetRecent(1);

        Assert.Single(recent);
        Assert.Equal(second.EventId, recent[0].EventId);
    }

    [Fact]
    public async Task Events_survive_reload_via_new_instance()
    {
        var dir = CreateTempDir();
        var log = new SecurityAuditLog(dir);
        var evt = ThreatEvent.Create("C:\\a.exe", "test-rule", "test", ThreatSeverity.Critical, true);
        await log.AppendAsync(evt, CancellationToken.None);

        var reloaded = new SecurityAuditLog(dir);
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.Contains(reloaded.GetRecent(10), e => e.EventId == evt.EventId);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-alice-log-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public class QuarantineServiceTests
{
    [Fact]
    public async Task QuarantineAsync_moves_file_out_of_original_location()
    {
        var stateDir = CreateTempDir();
        var sourceDir = CreateTempDir();
        var filePath = Path.Combine(sourceDir, "suspicious.exe");
        File.WriteAllText(filePath, "dummy");

        var quarantine = new QuarantineService(stateDir);
        var id = await quarantine.QuarantineAsync(filePath, CancellationToken.None);

        Assert.False(File.Exists(filePath));
        Assert.True(quarantine.AllQuarantined.ContainsKey(id));
    }

    [Fact]
    public async Task RestoreAsync_moves_file_back_to_original_location()
    {
        var stateDir = CreateTempDir();
        var sourceDir = CreateTempDir();
        var filePath = Path.Combine(sourceDir, "suspicious.exe");
        File.WriteAllText(filePath, "dummy");

        var quarantine = new QuarantineService(stateDir);
        var id = await quarantine.QuarantineAsync(filePath, CancellationToken.None);

        await quarantine.RestoreAsync(id, CancellationToken.None);

        Assert.True(File.Exists(filePath));
        Assert.False(quarantine.AllQuarantined.ContainsKey(id));
    }

    [Fact]
    public async Task QuarantineAsync_throws_for_missing_file()
    {
        var quarantine = new QuarantineService(CreateTempDir());

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => quarantine.QuarantineAsync(Path.Combine(CreateTempDir(), "does-not-exist.exe"), CancellationToken.None));
    }

    [Fact]
    public async Task RestoreAsync_throws_for_unknown_id()
    {
        var quarantine = new QuarantineService(CreateTempDir());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => quarantine.RestoreAsync("unknown-id", CancellationToken.None));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-alice-quarantine-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
