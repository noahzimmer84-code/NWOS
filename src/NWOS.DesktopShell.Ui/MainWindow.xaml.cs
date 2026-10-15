using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NWOS.Contracts;
using NWOS.DesktopShell.Ui.ViewModels;

namespace NWOS.DesktopShell.Ui;

public sealed record WidgetTypeItem(string WidgetTypeId, string DisplayName);
public sealed record SearchResultItem(string Path, string DisplayName);

/// <summary>
/// Reine Darstellung: sämtliche Daten (Icon-Sichtbarkeit, Boxen, Widgets,
/// Suchindex) liegen im laufenden Cardinal-Prozess und werden per IPC
/// abgefragt/geändert (Abschnitt 7). Ist Cardinal nicht erreichbar, zeigt das
/// Fenster das im Titel an und bleibt bedienbar, statt abzustürzen (Abschnitt 36).
/// </summary>
public partial class MainWindow : Window
{
    private const string DesktopShellId = "nwos.desktopshell";
    private const string WidgetsId = "nwos.widgets";

    private readonly CardinalClient _cardinal;
    private readonly ObservableCollection<IconViewModel> _icons = new();
    private readonly ObservableCollection<BoxViewModel> _boxes = new();
    private readonly ObservableCollection<WidgetViewModel> _widgets = new();

    // Widget-Aktualisierung nur bei sichtbarem, nicht minimiertem Fenster
    // (Performance-Grundsatz, Abschnitt 35).
    private readonly DispatcherTimer _widgetRefreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _refreshingWidgets;
    private int _lastIndexCount;

    public MainWindow(CardinalClient cardinal)
    {
        InitializeComponent();
        _cardinal = cardinal;

        IconsItemsControl.ItemsSource = _icons;
        BoxesItemsControl.ItemsSource = _boxes;
        WidgetsItemsControl.ItemsSource = _widgets;

        _widgetRefreshTimer.Tick += async (_, _) => await RefreshWidgetsAsync();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized) _widgetRefreshTimer.Stop();
            else if (_widgets.Count > 0) _widgetRefreshTimer.Start();
        };

        Loaded += async (_, _) => await InitializeAsync();
    }

    private void UpdateConnectionState()
    {
        Title = _cardinal.LastError is null
            ? "NWOS Desktop Shell — mit Cardinal verbunden"
            : $"NWOS Desktop Shell — NICHT verbunden ({_cardinal.LastError})";
    }

    private async Task InitializeAsync()
    {
        await LoadIconsAsync();
        await LoadBoxesAsync();
        await LoadWidgetsAsync();
        UpdateConnectionState();

        if (_widgets.Count > 0) _widgetRefreshTimer.Start();

        // Suchindex im Hintergrund aufbauen, damit das Fenster nicht wartet (Abschnitt 35).
        _ = RebuildSearchIndexAsync();
    }

    // --- Laden ---------------------------------------------------------------

    private async Task LoadIconsAsync()
    {
        var states = new Dictionary<string, bool>();
        using (var doc = await _cardinal.CallAsync(DesktopShellId, "icon.listStates", new { }, PermissionScope.FileSystemRead))
        {
            if (doc is not null)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                    states[prop.Name] = prop.Value.GetBoolean();
            }
        }

        // Die Einträge des echten Desktop-Ordners sind reine Darstellungsdaten
        // und werden lokal gelesen; nur der Sichtbarkeitszustand kommt aus Cardinal.
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        List<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(desktop).OrderBy(p => p).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }

        _icons.Clear();
        foreach (var path in entries)
        {
            // Stabile ID (Pfad), damit der gespeicherte Zustand nach Neustarts passt.
            var iconId = path.ToLowerInvariant();
            _icons.Add(new IconViewModel(iconId, Path.GetFileNameWithoutExtension(path), path,
                states.GetValueOrDefault(iconId, true)));
        }
    }

    private async Task LoadBoxesAsync()
    {
        using var doc = await _cardinal.CallAsync(DesktopShellId, "box.list", new { }, PermissionScope.FileSystemRead);
        if (doc is null) return;

        _boxes.Clear();
        foreach (var box in doc.RootElement.EnumerateArray())
        {
            var vm = new BoxViewModel(box.GetProperty("boxId").GetString()!, box.GetProperty("name").GetString()!);
            foreach (var item in box.GetProperty("items").EnumerateArray())
                vm.Items.Add(new BoxItemViewModel(item.GetProperty("sourcePath").GetString()!));
            _boxes.Add(vm);
        }
    }

    private async Task LoadWidgetsAsync()
    {
        using (var types = await _cardinal.CallAsync(WidgetsId, "widget.listTypes", new { }, PermissionScope.FileSystemRead))
        {
            if (types is not null)
            {
                var items = types.RootElement.EnumerateArray()
                    .Select(t => new WidgetTypeItem(t.GetProperty("widgetTypeId").GetString()!, t.GetProperty("displayName").GetString()!))
                    .ToList();
                WidgetTypeComboBox.ItemsSource = items;
                WidgetTypeComboBox.SelectedIndex = items.Count > 0 ? 0 : -1;
            }
        }

        using var doc = await _cardinal.CallAsync(WidgetsId, "widget.list", new { }, PermissionScope.FileSystemRead);
        if (doc is null) return;

        _widgets.Clear();
        foreach (var w in doc.RootElement.EnumerateArray())
            _widgets.Add(new WidgetViewModel(w.GetProperty("instanceId").GetString()!, w.GetProperty("widgetTypeId").GetString()!));
    }

    private async Task RefreshWidgetsAsync()
    {
        if (_refreshingWidgets) return; // keine überlappenden Abfragen aufstauen
        _refreshingWidgets = true;
        try
        {
            foreach (var vm in _widgets.ToList())
            {
                using var doc = await _cardinal.CallAsync(WidgetsId, "widget.render",
                    new { instanceId = vm.InstanceId }, PermissionScope.FileSystemRead);
                if (doc is null) continue;

                if (doc.RootElement.ValueKind == JsonValueKind.Null)
                {
                    vm.ApplyValues(null);
                }
                else
                {
                    var values = doc.RootElement.GetProperty("values").EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
                    vm.ApplyValues(values);
                }
            }
        }
        finally
        {
            _refreshingWidgets = false;
            UpdateConnectionState();
        }
    }

    private async Task RebuildSearchIndexAsync()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        using var doc = await _cardinal.CallAsync(DesktopShellId, "search.rebuildIndex",
            new { rootDirectory = desktop }, PermissionScope.FileSystemWrite);
        if (doc is not null) _lastIndexCount = doc.RootElement.GetProperty("entryCount").GetInt32();
    }

    // --- Icons (Abschnitt 16) -------------------------------------------------

    private async void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement { DataContext: IconViewModel iconVm }) return;

        // Bewusst Doppelklick = Ein-/Ausblenden (Abschnitt 16), nicht "Öffnen".
        using var doc = await _cardinal.CallAsync(DesktopShellId, "icon.toggleVisibility",
            new { iconId = iconVm.IconId }, PermissionScope.FileSystemWrite);
        if (doc is not null) iconVm.IsVisible = doc.RootElement.GetProperty("isVisible").GetBoolean();
        UpdateConnectionState();
    }

    // --- Boxen (Abschnitt 17/18) ----------------------------------------------

    private async void CreateBox_Click(object sender, RoutedEventArgs e)
    {
        using var doc = await _cardinal.CallAsync(DesktopShellId, "box.create",
            new { name = $"Box {_boxes.Count + 1}" }, PermissionScope.FileSystemWrite);
        if (doc is not null)
            _boxes.Add(new BoxViewModel(doc.RootElement.GetProperty("boxId").GetString()!, doc.RootElement.GetProperty("name").GetString()!));
        UpdateConnectionState();
    }

    private void BoxBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BoxViewModel vm } && !vm.IsPinned) vm.IsExpanded = true;
    }

    private void BoxBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BoxViewModel vm } && !vm.IsPinned) vm.IsExpanded = false;
    }

    private async void BoxBorder_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BoxViewModel boxVm }) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        foreach (var path in paths)
        {
            var kind = Directory.Exists(path) ? "Folder" : "File";
            using var doc = await _cardinal.CallAsync(DesktopShellId, "box.addItem",
                new { boxId = boxVm.BoxId, sourcePath = path, kind }, PermissionScope.FileSystemWrite);
            if (doc is not null) boxVm.Items.Add(new BoxItemViewModel(path));
        }

        UpdateConnectionState();
    }

    // --- Widgets (Abschnitt 21) -----------------------------------------------

    private async void CreateWidget_Click(object sender, RoutedEventArgs e)
    {
        if (WidgetTypeComboBox.SelectedItem is not WidgetTypeItem type)
        {
            MessageBox.Show("Bitte zuerst einen Widget-Typ auswählen (ist Cardinal gestartet?).", "NWOS",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var doc = await _cardinal.CallAsync(WidgetsId, "widget.create",
            new { widgetTypeId = type.WidgetTypeId }, PermissionScope.FileSystemWrite);
        if (doc is not null)
        {
            _widgets.Add(new WidgetViewModel(doc.RootElement.GetProperty("instanceId").GetString()!, type.WidgetTypeId));
            if (!_widgetRefreshTimer.IsEnabled) _widgetRefreshTimer.Start();
        }

        UpdateConnectionState();
    }

    // --- Suche (Abschnitt 20) ---------------------------------------------------

    private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResultsList.Visibility = Visibility.Collapsed;
            SearchResultsList.ItemsSource = null;
            return;
        }

        using var doc = await _cardinal.CallAsync(DesktopShellId, "search.global", new { query }, PermissionScope.FileSystemRead);
        if (doc is null || SearchBox.Text != query) return; // veraltete Antwort verwerfen

        var results = doc.RootElement.EnumerateArray()
            .Select(r => new SearchResultItem(r.GetProperty("path").GetString()!, r.GetProperty("displayName").GetString()!))
            .ToList();
        SearchResultsList.ItemsSource = results;
        SearchResultsList.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RebuildIndex_Click(object sender, RoutedEventArgs e)
    {
        await RebuildSearchIndexAsync();
        MessageBox.Show($"Suchindex neu aufgebaut ({_lastIndexCount} Einträge, Desktop-Ordner).",
            "NWOS", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
