namespace NWOS.DesktopShell.Ui.ViewModels;

public sealed class WidgetViewModel : ObservableObject
{
    public string InstanceId { get; }
    public string WidgetTypeId { get; }

    private string _primaryText = "…";
    public string PrimaryText
    {
        get => _primaryText;
        set => SetField(ref _primaryText, value);
    }

    private string _secondaryText = string.Empty;
    public string SecondaryText
    {
        get => _secondaryText;
        set => SetField(ref _secondaryText, value);
    }

    public WidgetViewModel(string instanceId, string widgetTypeId)
    {
        InstanceId = instanceId;
        WidgetTypeId = widgetTypeId;
    }

    /// <summary><c>null</c> = Widget deaktiviert (Abschnitt 21) - wird so angezeigt statt veralteter Werte.</summary>
    public void ApplyValues(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
        {
            PrimaryText = "(deaktiviert)";
            SecondaryText = string.Empty;
            return;
        }

        PrimaryText = values.GetValueOrDefault("time", values.Values.FirstOrDefault() ?? "");
        SecondaryText = values.GetValueOrDefault("date", "");
    }
}
