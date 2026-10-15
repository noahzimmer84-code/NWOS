namespace NWOS.Widgets;

/// <summary>
/// Verwaltet die Menge der verfügbaren Widget-<b>Typen</b> (nicht die
/// platzierten Instanzen - dafür ist <see cref="WidgetManager"/> zuständig).
/// Getrennt, weil "welche Widget-Typen existieren" und "welche Widgets hat
/// dieser Nutzer platziert" unabhängig voneinander veränderlich sind.
/// </summary>
public sealed class WidgetRegistry
{
    private readonly Dictionary<string, IWidgetProvider> _providers = new();

    public void RegisterProvider(IWidgetProvider provider)
    {
        if (!_providers.TryAdd(provider.WidgetTypeId, provider))
        {
            throw new InvalidOperationException($"Widget-Typ '{provider.WidgetTypeId}' ist bereits registriert.");
        }
    }

    public bool TryGetProvider(string widgetTypeId, out IWidgetProvider provider) =>
        _providers.TryGetValue(widgetTypeId, out provider!);

    public IReadOnlyCollection<IWidgetProvider> AllProviders => _providers.Values;
}
