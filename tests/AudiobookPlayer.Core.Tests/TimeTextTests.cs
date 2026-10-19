using AudiobookPlayer.Core;

namespace AudiobookPlayer.Core.Tests;

public class TimeTextTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(59.9, "0:59")]
    [InlineData(61, "1:01")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    [InlineData(double.NaN, "0:00")]
    public void Clock(double seconds, string expected) => Assert.Equal(expected, TimeText.Clock(seconds));

    [Theory]
    [InlineData(0, "0 seconds")]
    [InlineData(1, "1 second")]
    [InlineData(60, "1 minute")]
    [InlineData(3600, "1 hour")]
    [InlineData(7322, "2 hours 2 minutes 2 seconds")]
    public void Spoken(double seconds, string expected) => Assert.Equal(expected, TimeText.Spoken(seconds));

    [Fact]
    public void SpokenRoughlyRoundsToMinutes() => Assert.Equal("3 minutes", TimeText.SpokenRoughly(170));

    [Fact]
    public void Ago()
    {
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("today", TimeText.Ago(now.AddMinutes(-5), now));
        Assert.Equal("3 days ago", TimeText.Ago(now.AddDays(-3), now));
    }
}
