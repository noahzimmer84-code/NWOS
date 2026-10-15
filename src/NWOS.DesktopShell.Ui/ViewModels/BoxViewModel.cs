using System.IO;
using System.Collections.ObjectModel;

namespace NWOS.DesktopShell.Ui.ViewModels;

public sealed class BoxItemViewModel
{
    public string SourcePath { get; }
    public string DisplayFileName => Path.GetFileName(SourcePath);

    public BoxItemViewModel(string sourcePath) => SourcePath = sourcePath;
}

/// <summary>UI-Zustand einer Smart Box: Hover- vs. statischer Modus (Abschnitt 18). Die Daten selbst liegen in Cardinal.</summary>
public sealed class BoxViewModel : ObservableObject
{
    public string BoxId { get; }
    public string Name { get; }

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetField(ref _isPinned, value) && value)
            {
                IsExpanded = true;
            }
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public ObservableCollection<BoxItemViewModel> Items { get; } = new();

    public BoxViewModel(string boxId, string name)
    {
        BoxId = boxId;
        Name = name;
    }
}
