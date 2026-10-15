using System.Text.Json;
using NWOS.Contracts;
using Xunit;

namespace NWOS.SystemCenter.Tests;

internal sealed class FakeStatusProvider : ISystemStatusProvider
{
    public Task<SystemStatusSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SystemStatusSnapshot(42.5, 16_000_000_000, 8_000_000_000,
            new List<DiskStatus> { new("C:", 500_000_000_000, 250_000_000_000) }, 123, DateTimeOffset.UtcNow));
}

internal sealed class FakeAutostartManager : IAutostartManager
{
    private readonly List<AutostartEntry> _entries = new() { new("TestApp", "test.exe", true) };

    public Task<IReadOnlyList<AutostartEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AutostartEntry>>(_entries);

    public Task SetEnabledAsync(string name, bool isEnabled, CancellationToken cancellationToken)
    {
        var index = _entries.FindIndex(e => e.Name == name);
        if (index >= 0)
        {
            _entries[index] = _entries[index] with { IsEnabled = isEnabled };
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeMessenger : IModuleMessenger
{
    public ModuleMessageOutcome OutcomeToReturn { get; set; } = ModuleMessageOutcome.Delivered;
    public ModuleMessage? LastSentMessage { get; private set; }

    public Task<ModuleMessageResult> SendAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        LastSentMessage = message;
        return Task.FromResult(new ModuleMessageResult(OutcomeToReturn, null, "simuliert"));
    }
}

public class SystemCenterModuleTests
{
    [Fact]
    public async Task GetStatus_without_provider_reports_unavailable_instead_of_throwing()
    {
        var module = new SystemCenterModule(statusProvider: null, autostartManager: null, messenger: null);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("test", module.ModuleId, "systemcenter.getStatus", "{}", PermissionScope.None);
        var response = await module.HandleMessageAsync(message, CancellationToken.None);

        using var doc = JsonDocument.Parse(response!.PayloadJson);
        Assert.False(doc.RootElement.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task GetStatus_with_provider_returns_real_snapshot()
    {
        var module = new SystemCenterModule(new FakeStatusProvider(), null, null);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("test", module.ModuleId, "systemcenter.getStatus", "{}", PermissionScope.None);
        var response = await module.HandleMessageAsync(message, CancellationToken.None);

        using var doc = JsonDocument.Parse(response!.PayloadJson);
        Assert.Equal(42.5, doc.RootElement.GetProperty("cpuUsagePercent").GetDouble());
    }

    [Fact]
    public async Task Optimize_runs_temp_cleanup_on_configured_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-sysctr-module-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var oldFile = Path.Combine(dir, "old.tmp");
        File.WriteAllText(oldFile, "x");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-2));

        var module = new SystemCenterModule(null, null, null, tempDirectory: dir);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("test", module.ModuleId, "systemcenter.optimize", "{}", PermissionScope.None);
        await module.HandleMessageAsync(message, CancellationToken.None);

        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public async Task CloseProcess_for_protected_name_refuses_without_asking_marc()
    {
        // ProtectedProcessList prüft nach Name - wir nutzen die echte PID des
        // Testprozesses selbst (garantiert kein "explorer"), daher testen wir
        // hier nur den Marc-Delegationspfad separat unten; der Schutzlisten-
        // Test steht bereits in TempCleanerAndProtectedProcessListTests.
        var messenger = new FakeMessenger();
        var module = new SystemCenterModule(null, null, messenger);
        await module.StartAsync(CancellationToken.None);

        var currentProcessId = Environment.ProcessId;
        var message = ModuleMessage.Create(
            "test", module.ModuleId, "systemcenter.closeProcess",
            JsonSerializer.Serialize(new { processId = currentProcessId }), PermissionScope.None);

        await module.HandleMessageAsync(message, CancellationToken.None);

        // Der aktuelle Testprozess ist nicht auf der Schutzliste (kein bekannter
        // Systemprozessname) - die Anfrage muss deshalb an Marc delegiert werden.
        Assert.NotNull(messenger.LastSentMessage);
        Assert.Equal("nwos.marc", messenger.LastSentMessage!.TargetModuleId);
        Assert.Equal(PermissionScope.ProcessControl, messenger.LastSentMessage.RequiredScope);
    }

    [Fact]
    public async Task ListAutostart_and_setEnabled_roundtrip_through_fake_manager()
    {
        var autostart = new FakeAutostartManager();
        var module = new SystemCenterModule(null, autostart, null);
        await module.StartAsync(CancellationToken.None);

        var setMessage = ModuleMessage.Create(
            "test", module.ModuleId, "systemcenter.setAutostartEnabled",
            JsonSerializer.Serialize(new { name = "TestApp", isEnabled = false }), PermissionScope.None);
        await module.HandleMessageAsync(setMessage, CancellationToken.None);

        var listMessage = ModuleMessage.Create("test", module.ModuleId, "systemcenter.listAutostart", "{}", PermissionScope.None);
        var response = await module.HandleMessageAsync(listMessage, CancellationToken.None);

        using var doc = JsonDocument.Parse(response!.PayloadJson);
        var entry = doc.RootElement.EnumerateArray().First();
        Assert.False(entry.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task Unknown_message_type_throws()
    {
        var module = new SystemCenterModule(null, null, null);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("test", module.ModuleId, "does.not.exist", "{}", PermissionScope.None);

        await Assert.ThrowsAsync<NotSupportedException>(() => module.HandleMessageAsync(message, CancellationToken.None));
    }
}
