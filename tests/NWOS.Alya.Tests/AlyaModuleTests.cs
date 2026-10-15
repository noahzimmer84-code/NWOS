using System.Text.Json;
using NWOS.Contracts;
using NWOS.Settings;
using Xunit;

namespace NWOS.Alya.Tests;

public class AlyaModuleTests
{
    private static AlyaModule CreateSut(out SettingsStore settings)
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-alya-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        settings = new SettingsStore(dir);
        return new AlyaModule(settings, messenger: null);
    }

    [Fact]
    public async Task Greet_returns_nonempty_text()
    {
        var module = CreateSut(out _);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("nwos.cardinal", module.ModuleId, "alya.greet", "{}", PermissionScope.None);
        var response = await module.HandleMessageAsync(message, CancellationToken.None);

        using var doc = JsonDocument.Parse(response!.PayloadJson);
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("text").GetString()));
    }

    [Fact]
    public async Task SetTone_then_getTone_roundtrips_and_persists_via_settings()
    {
        var module = CreateSut(out var settings);
        await module.StartAsync(CancellationToken.None);

        var setMessage = ModuleMessage.Create(
            "nwos.cardinal", module.ModuleId, "alya.setTone",
            JsonSerializer.Serialize(new { tone = "Freundlich" }), PermissionScope.None);
        await module.HandleMessageAsync(setMessage, CancellationToken.None);

        var getMessage = ModuleMessage.Create("nwos.cardinal", module.ModuleId, "alya.getTone", "{}", PermissionScope.None);
        var response = await module.HandleMessageAsync(getMessage, CancellationToken.None);

        using var doc = JsonDocument.Parse(response!.PayloadJson);
        Assert.Equal("Freundlich", doc.RootElement.GetProperty("tone").GetString());
        Assert.Equal("Freundlich", settings.GetValue(module.ModuleId, "tone", "?"));
    }

    [Fact]
    public async Task SetTone_with_invalid_value_throws_instead_of_silently_defaulting()
    {
        var module = CreateSut(out _);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create(
            "nwos.cardinal", module.ModuleId, "alya.setTone",
            JsonSerializer.Serialize(new { tone = "Wütend" }), PermissionScope.None);

        await Assert.ThrowsAsync<ArgumentException>(() => module.HandleMessageAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task StatusReport_message_returns_a_report_even_without_messenger()
    {
        var module = CreateSut(out _);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("nwos.cardinal", module.ModuleId, "alya.statusReport", "{}", PermissionScope.None);
        var response = await module.HandleMessageAsync(message, CancellationToken.None);

        using var doc = JsonDocument.Parse(response!.PayloadJson);
        Assert.True(doc.RootElement.GetProperty("entries").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Unknown_message_type_throws()
    {
        var module = CreateSut(out _);
        await module.StartAsync(CancellationToken.None);

        var message = ModuleMessage.Create("nwos.cardinal", module.ModuleId, "does.not.exist", "{}", PermissionScope.None);

        await Assert.ThrowsAsync<NotSupportedException>(() => module.HandleMessageAsync(message, CancellationToken.None));
    }
}
