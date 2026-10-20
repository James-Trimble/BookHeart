namespace StoryHeart.Core;

/// <summary>Formats times for the screen ("1:02:03") and for speech ("1 hour 2 minutes 3 seconds").</summary>
public static class TimeText
{
    public static string Clock(double seconds)
    {
        var total = WholeSeconds(seconds);
        long h = total / 3600, m = total % 3600 / 60, s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }

    public static string Clock(TimeSpan time) => Clock(time.TotalSeconds);

    public static string Spoken(double seconds)
    {
        var total = WholeSeconds(seconds);
        long h = total / 3600, m = total % 3600 / 60, s = total % 60;
        var parts = new List<string>(3);
        if (h > 0) parts.Add(h + (h == 1 ? " hour" : " hours"));
        if (m > 0) parts.Add(m + (m == 1 ? " minute" : " minutes"));
        if (s > 0 || parts.Count == 0) parts.Add(s + (s == 1 ? " second" : " seconds"));
        return string.Join(' ', parts);
    }

    public static string Spoken(TimeSpan time) => Spoken(time.TotalSeconds);

    /// <summary>Rounds to the nearest minute before speaking, for "about N left" messages.</summary>
    public static string SpokenRoughly(double seconds) => Spoken(Math.Round(seconds / 60) * 60);

    public static string Ago(DateTimeOffset when, DateTimeOffset now)
    {
        var days = (now.LocalDateTime.Date - when.LocalDateTime.Date).Days;
        if (days <= 0) return "today";
        if (days == 1) return "yesterday";
        if (days < 7) return days + " days ago";
        var local = when.LocalDateTime;
        return local.Year == now.LocalDateTime.Year ? local.ToString("MMM d") : local.ToString("MMM d, yyyy");
    }

    public static string Size(long bytes) =>
        bytes >= 1_000_000_000 ? (bytes / 1e9).ToString("0.0") + " GB" : Math.Max(1, (long)Math.Round(bytes / 1e6)) + " MB";

    private static long WholeSeconds(double seconds) =>
        double.IsFinite(seconds) && seconds > 0 ? (long)Math.Floor(seconds) : 0;
}
