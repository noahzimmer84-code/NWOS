using Xunit;

namespace NWOS.Widgets.Tests;

public class ClockWidgetProviderTests
{
    [Fact]
    public async Task RenderAsync_returns_current_time_values()
    {
        var provider = new ClockWidgetProvider();
        var before = DateTimeOffset.Now;

        var snapshot = await provider.RenderAsync(CancellationToken.None);

        Assert.Equal("nwos.widget.clock", snapshot.WidgetTypeId);
        Assert.True(snapshot.GeneratedAt >= before);
        Assert.True(snapshot.Values.ContainsKey("time"));
        Assert.True(snapshot.Values.ContainsKey("date"));
    }
}

public class WidgetManagerTests
{
    private static (WidgetRegistry registry, WidgetManager manager) CreateSut()
    {
        var registry = new WidgetRegistry();
        registry.RegisterProvider(new ClockWidgetProvider());
        var manager = new WidgetManager(registry, CreateTempDir());
        return (registry, manager);
    }

    [Fact]
    public void CreateInstance_fails_for_unregistered_widget_type()
    {
        var (_, manager) = CreateSut();

        Assert.Throws<InvalidOperationException>(() => manager.CreateInstance("nwos.widget.does-not-exist"));
    }

    [Fact]
    public void CreateInstance_uses_provider_default_size()
    {
        var (_, manager) = CreateSut();

        var instance = manager.CreateInstance("nwos.widget.clock");

        Assert.Equal(160, instance.Width);
        Assert.Equal(80, instance.Height);
        Assert.True(instance.IsEnabled);
    }

    [Fact]
    public void Resize_rejects_non_positive_dimensions()
    {
        var (_, manager) = CreateSut();
        var instance = manager.CreateInstance("nwos.widget.clock");

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Resize(instance.InstanceId, 0, 50));
    }

    [Fact]
    public void Move_updates_position()
    {
        var (_, manager) = CreateSut();
        var instance = manager.CreateInstance("nwos.widget.clock");

        manager.Move(instance.InstanceId, 100, 200);

        Assert.True(manager.TryGetInstance(instance.InstanceId, out var found));
        Assert.Equal(100, found.PositionX);
        Assert.Equal(200, found.PositionY);
    }

    [Fact]
    public async Task RenderAsync_returns_null_for_disabled_widget()
    {
        var (_, manager) = CreateSut();
        var instance = manager.CreateInstance("nwos.widget.clock");
        manager.SetEnabled(instance.InstanceId, false);

        var snapshot = await manager.RenderAsync(instance.InstanceId, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task RenderAsync_returns_snapshot_for_enabled_widget()
    {
        var (_, manager) = CreateSut();
        var instance = manager.CreateInstance("nwos.widget.clock");

        var snapshot = await manager.RenderAsync(instance.InstanceId, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal("nwos.widget.clock", snapshot!.WidgetTypeId);
    }

    [Fact]
    public async Task Instances_survive_save_and_reload()
    {
        var registry = new WidgetRegistry();
        registry.RegisterProvider(new ClockWidgetProvider());
        var dir = CreateTempDir();

        var manager = new WidgetManager(registry, dir);
        var instance = manager.CreateInstance("nwos.widget.clock");
        manager.Move(instance.InstanceId, 42, 84);
        manager.SetEnabled(instance.InstanceId, false);
        await manager.SaveAsync(CancellationToken.None);

        var reloaded = new WidgetManager(registry, dir);
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.True(reloaded.TryGetInstance(instance.InstanceId, out var found));
        Assert.Equal(42, found.PositionX);
        Assert.False(found.IsEnabled);
    }

    [Fact]
    public void RemoveInstance_returns_false_for_unknown_id()
    {
        var (_, manager) = CreateSut();
        Assert.False(manager.RemoveInstance("does-not-exist"));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-widget-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
