namespace NWOS.Widgets;

/// <summary>
/// Eine vom Nutzer auf dem Desktop platzierte Widget-Instanz
/// (Architekturdokument Abschnitt 21: "verschiebbar, skalierbar, aktivierbar,
/// deaktivierbar, personalisierbar"). Enthält absichtlich keine Anzeigedaten -
/// die werden bei Bedarf live über <see cref="IWidgetProvider.RenderAsync"/>
/// abgerufen, damit ein Widget nie veraltete Werte persistiert zeigt.
/// </summary>
public sealed class WidgetInstance
{
    public string InstanceId { get; }
    public string WidgetTypeId { get; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Personalisierung pro Instanz (Abschnitt 21), z. B. Farbschema, Anzeigeformat.</summary>
    public Dictionary<string, string> Settings { get; } = new();

    public WidgetInstance(string instanceId, string widgetTypeId, int width, int height)
    {
        InstanceId = instanceId;
        WidgetTypeId = widgetTypeId;
        Width = width;
        Height = height;
    }
}
