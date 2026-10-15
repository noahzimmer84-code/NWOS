using Xunit;

namespace NWOS.DesktopShell.Tests;

public class IconVisibilityStoreTests
{
    [Fact]
    public void Icon_is_visible_by_default_before_any_toggle()
    {
        var store = new IconVisibilityStore(CreateTempDir());
        Assert.True(store.IsVisible("icon-1"));
    }

    [Fact]
    public void Double_click_toggle_hides_then_shows_again()
    {
        var store = new IconVisibilityStore(CreateTempDir());

        var afterFirstToggle = store.ToggleVisibility("icon-1");
        Assert.False(afterFirstToggle);
        Assert.False(store.IsVisible("icon-1"));

        var afterSecondToggle = store.ToggleVisibility("icon-1");
        Assert.True(afterSecondToggle);
        Assert.True(store.IsVisible("icon-1"));
    }

    [Fact]
    public void Toggling_one_icon_does_not_affect_another()
    {
        var store = new IconVisibilityStore(CreateTempDir());

        store.ToggleVisibility("icon-1");

        Assert.False(store.IsVisible("icon-1"));
        Assert.True(store.IsVisible("icon-2")); // unberührt, wie in Abschnitt 16 gefordert
    }

    [Fact]
    public async Task Visibility_state_survives_save_and_reload()
    {
        var dir = CreateTempDir();
        var store = new IconVisibilityStore(dir);
        store.ToggleVisibility("icon-1");
        await store.SaveAsync(CancellationToken.None);

        var reloaded = new IconVisibilityStore(dir);
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.False(reloaded.IsVisible("icon-1"));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
