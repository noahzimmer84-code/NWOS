namespace NWOS.Widgets;

/// <summary>
/// Eingebauter Referenz-Widget-Typ. Zeigt echte, aktuelle Zeit - bewusst kein
/// Platzhalter (Abschnitt 40: keine Fake-Funktionen), damit der
/// Erweiterungsmechanismus von Anfang an an einem funktionierenden Beispiel
/// nachvollziehbar ist, nicht nur an einer leeren Schnittstelle.
/// </summary>
public sealed class ClockWidgetProvider : IWidgetProvider
{
    public string WidgetTypeId => "nwos.widget.clock";
    public string DisplayName => "Uhr";
    public int DefaultWidth => 160;
    public int DefaultHeight => 80;

    public Task<WidgetSnapshot> RenderAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var values = new Dictionary<string, string>
        {
            ["time"] = now.ToString("HH:mm:ss"),
            ["date"] = now.ToString("dddd, dd. MMMM yyyy"),
        };

        return Task.FromResult(new WidgetSnapshot(WidgetTypeId, now, values));
    }
}
