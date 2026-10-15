using Xunit;

namespace NWOS.DesktopShell.Tests;

public class SmartBoxTests
{
    [Fact]
    public void AddItem_adds_to_box_and_RemoveItem_removes_it()
    {
        var box = SmartBox.Create("Projekte");
        var item = box.AddItem(@"C:\Users\Test\report.docx", BoxItemKind.File);

        Assert.Single(box.Items);

        var removed = box.RemoveItem(item.ItemId);

        Assert.True(removed);
        Assert.Empty(box.Items);
    }

    [Fact]
    public void SearchContent_only_matches_items_in_this_box()
    {
        var box = SmartBox.Create("Projekte");
        box.AddItem(@"C:\Users\Test\report.docx", BoxItemKind.File);
        box.AddItem(@"C:\Users\Test\budget.xlsx", BoxItemKind.File);

        var results = box.SearchContent("report").ToList();

        Assert.Single(results);
        Assert.EndsWith("report.docx", results[0].SourcePath);
    }

    [Fact]
    public void SearchContent_with_empty_query_returns_all_items()
    {
        var box = SmartBox.Create("Projekte");
        box.AddItem(@"C:\a.txt", BoxItemKind.File);
        box.AddItem(@"C:\b.txt", BoxItemKind.File);

        Assert.Equal(2, box.SearchContent("").Count());
    }
}

public class BoxManagerTests
{
    [Fact]
    public void CreateBox_makes_box_retrievable_by_id()
    {
        var manager = new BoxManager(CreateTempDir());
        var box = manager.CreateBox("Downloads");

        Assert.True(manager.TryGetBox(box.BoxId, out var found));
        Assert.Equal("Downloads", found.Name);
    }

    [Fact]
    public async Task Boxes_and_their_items_survive_save_and_reload()
    {
        var dir = CreateTempDir();
        var manager = new BoxManager(dir);
        var box = manager.CreateBox("Downloads");
        box.AddItem(@"C:\Downloads\installer.exe", BoxItemKind.File);
        box.DisplayMode = BoxDisplayMode.Static;
        await manager.SaveAsync(CancellationToken.None);

        var reloaded = new BoxManager(dir);
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.True(reloaded.TryGetBox(box.BoxId, out var found));
        Assert.Equal(BoxDisplayMode.Static, found.DisplayMode);
        Assert.Single(found.Items);
        Assert.EndsWith("installer.exe", found.Items[0].SourcePath);
    }

    [Fact]
    public void DeleteBox_removes_it_from_AllBoxes()
    {
        var manager = new BoxManager(CreateTempDir());
        var box = manager.CreateBox("Temp");

        var deleted = manager.DeleteBox(box.BoxId);

        Assert.True(deleted);
        Assert.DoesNotContain(manager.AllBoxes, b => b.BoxId == box.BoxId);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nwos-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
