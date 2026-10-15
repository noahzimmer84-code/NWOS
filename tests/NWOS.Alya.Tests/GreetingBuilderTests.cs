using Xunit;

namespace NWOS.Alya.Tests;

public class GreetingBuilderTests
{
    private readonly GreetingBuilder _builder = new();

    [Theory]
    [InlineData(8, "Guten Morgen")]
    [InlineData(14, "Guten Tag")]
    [InlineData(20, "Guten Abend")]
    [InlineData(2, "Hallo")]
    public void Greeting_depends_on_time_of_day(int hour, string expectedPrefix)
    {
        var now = new DateTimeOffset(2026, 1, 1, hour, 0, 0, TimeSpan.Zero);

        var greeting = _builder.BuildGreeting(AlyaTone.Sachlich, now);

        Assert.StartsWith(expectedPrefix, greeting);
    }

    [Fact]
    public void Friendly_tone_produces_longer_more_personal_text_than_sachlich()
    {
        var now = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        var sachlich = _builder.BuildGreeting(AlyaTone.Sachlich, now);
        var freundlich = _builder.BuildGreeting(AlyaTone.Freundlich, now);

        Assert.NotEqual(sachlich, freundlich);
        Assert.True(freundlich.Length > sachlich.Length);
    }
}
