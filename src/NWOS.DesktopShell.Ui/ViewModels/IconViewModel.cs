namespace NWOS.DesktopShell.Ui.ViewModels;

public sealed class IconViewModel : ObservableObject
{
    public string IconId { get; }
    public string DisplayName { get; }
    public string TargetPath { get; }

    private bool _isVisible;

    /// <summary>Spiegelt nur den in Cardinal gespeicherten Sichtbarkeitszustand (Abschnitt 16).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    public IconViewModel(string iconId, string displayName, string targetPath, bool isVisible)
    {
        IconId = iconId;
        DisplayName = displayName;
        TargetPath = targetPath;
        _isVisible = isVisible;
    }
}
