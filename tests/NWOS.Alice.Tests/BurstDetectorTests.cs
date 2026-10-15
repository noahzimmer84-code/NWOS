using Xunit;

namespace NWOS.Alice.Tests;

public class BurstDetectorTests
{
    [Fact]
    public void Does_not_trigger_below_threshold()
    {
        var detector = new BurstDetector(TimeSpan.FromSeconds(10), threshold: 5);
        var now = DateTimeOffset.UtcNow;

        bool triggered = false;
        for (int i = 0; i < 4; i++)
        {
            triggered = detector.RecordEventAndCheckBurst(now.AddMilliseconds(i));
        }

        Assert.False(triggered);
    }

    [Fact]
    public void Triggers_once_threshold_is_reached_within_window()
    {
        var detector = new BurstDetector(TimeSpan.FromSeconds(10), threshold: 5);
        var now = DateTimeOffset.UtcNow;

        bool triggered = false;
        for (int i = 0; i < 5; i++)
        {
            triggered = detector.RecordEventAndCheckBurst(now.AddMilliseconds(i));
        }

        Assert.True(triggered);
    }

    [Fact]
    public void Old_events_outside_window_do_not_count_toward_threshold()
    {
        var detector = new BurstDetector(TimeSpan.FromSeconds(1), threshold: 3);
        var start = DateTimeOffset.UtcNow;

        detector.RecordEventAndCheckBurst(start);
        detector.RecordEventAndCheckBurst(start.AddMilliseconds(100));

        // Weit außerhalb des 1-Sekunden-Fensters - die ersten beiden Events
        // müssen "verfallen" sein.
        var triggered = detector.RecordEventAndCheckBurst(start.AddSeconds(5));

        Assert.False(triggered);
    }
}
