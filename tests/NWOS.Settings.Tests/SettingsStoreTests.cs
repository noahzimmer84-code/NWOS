using Xunit;

namespace NWOS.Settings.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void GetValue_returns_default_when_not_set()
    {
        var store = new SettingsStore(CreateTempDir());
        Assert.Equal("fallback", store.GetValue("nwos.desktopshell", "theme", "fallback"));
    }

    [Fact]
    public void SetValue_then_GetValue_roundtrips()
    {
        var store = new SettingsStore(CreateTempDir());
        store.SetValue("nwos.desktopshell", "theme", "dark");

        Assert.Equal("dark", store.GetValue("nwos.desktopshell", "theme", "fallback"));
    }

    [Fact]
    public void Settings_are_isolated_per_module_section()
    {
        var store = new SettingsStore(CreateTempDir());
        store.SetValue("nwos.desktopshell", "theme", "dark");
        store.SetValue("nwos.alice", "theme", "light");

        Assert.Equal("dark", store.GetValue("nwos.desktopshell", "theme", "?"));
        Assert.Equal("light", store.GetValue("nwos.alice", "theme", "?"));
    }

    [Fact]
    public async Task Settings_survive_save_and_reload()
    {
        var dir = CreateTempDir();
        var store = new SettingsStore(dir);
        store.SetValue("nwos.desktopshell", "theme", "dark");
        await store.SaveAsync(CancellationToken.None);

        var reloaded = new SettingsStore(dir);
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.Equal("dark", reloaded.GetValue("nwos.desktopshell", "theme", "?"));
    }

    [Fact]
    public void GetSection_returns_all_values_for_one_module()
    {
        var store = new SettingsStore(CreateTempDir());
        store.SetValue("nwos.desktopshell", "theme", "dark");
        store.SetValue("nwos.desktopshell", "iconSize", "48");

        var section = store.GetSection("nwos.desktopshell");

        Assert.Equal(2, section.Count);
        Assert.Equal("48", section["iconSize"]);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-settings-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
