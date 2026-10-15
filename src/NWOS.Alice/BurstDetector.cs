namespace NWOS.Alice;

/// <summary>
/// Erkennt ungewöhnlich viele Dateiänderungen in kurzer Zeit (grobes,
/// bewusst konservatives Indiz für z. B. Ransomware-artiges Verhalten -
/// erzeugt bewusst NICHT automatisch eine Aktion, siehe Klassendokumentation
/// in <see cref="HeuristicEngine"/>: false positives durch legitime
/// Massenoperationen wie Entpacken eines Archivs sind zu erwarten).
/// </summary>
public sealed class BurstDetector
{
    private readonly TimeSpan _window;
    private readonly int _threshold;
    private readonly Queue<DateTimeOffset> _recentEvents = new();
    private readonly object _lock = new();

    public BurstDetector(TimeSpan? window = null, int threshold = 30)
    {
        _window = window ?? TimeSpan.FromSeconds(10);
        _threshold = threshold;
    }

    /// <summary>
    /// Meldet ein Dateiereignis und gibt <c>true</c> zurück, wenn dadurch der
    /// Schwellwert innerhalb des Zeitfensters überschritten wurde.
    /// </summary>
    public bool RecordEventAndCheckBurst(DateTimeOffset timestamp)
    {
        lock (_lock)
        {
            _recentEvents.Enqueue(timestamp);

            while (_recentEvents.Count > 0 && timestamp - _recentEvents.Peek() > _window)
            {
                _recentEvents.Dequeue();
            }

            return _recentEvents.Count >= _threshold;
        }
    }
}
