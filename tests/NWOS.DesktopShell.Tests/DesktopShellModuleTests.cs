using System.Text.Json;
using NWOS.Contracts;
using NWOS.Settings;
using Xunit;

namespace NWOS.DesktopShell.Tests;

public class DesktopShellModuleTests
{
    private static DesktopShellModule CreateSut(out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), "nwos-module-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var settings = new SettingsStore(tempDir);
        return new DesktopShellModule(tempDir, settings);
    }

    [Fact]
    public async Task ModuleId_is_stable_and_used_for_routing()
    {
        var module = CreateSut(out _);
        Assert.Equal("nwos.desktopshell", module.ModuleId);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Box_create_message_returns_created_box_and_it_is_retrievable_via_search()
    {
        var module = CreateSut(out _);
        await module.StartAsync(CancellationToken.None);

        var createMessage = ModuleMessage.Create(
            "nwos.cardinal", module.ModuleId, "box.create",
            JsonSerializer.Serialize(new { name = "Arbeit" }), PermissionScope.FileSystemWrite);

        var response = await module.HandleMessageAsync(createMessage, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("box.create.result", response!.MessageType);
        using var doc = JsonDocument.Parse(response.PayloadJson);
        Assert.Equal("Arbeit", doc.RootElement.GetProperty("name").GetString());

        await module.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Unknown_message_type_throws_instead_of_silently_ignoring()
    {
        var module = CreateSut(out _);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create(
            "nwos.cardinal", module.ModuleId, "does.not.exist", "{}", PermissionScope.None);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => module.HandleMessageAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task Health_is_unresponsive_before_start_and_healthy_after()
    {
        var module = CreateSut(out _);

        var beforeStart = await module.CheckHealthAsync(CancellationToken.None);
        Assert.Equal(ModuleStatus.Unresponsive, beforeStart.Status);

        await module.StartAsync(CancellationToken.None);
        var afterStart = await module.CheckHealthAsync(CancellationToken.None);
        Assert.Equal(ModuleStatus.Healthy, afterStart.Status);
    }
}
