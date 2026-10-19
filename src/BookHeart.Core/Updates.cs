using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookHeart.Core;

/// <summary>A version number like 0.2.0 or 0.2.0-beta.1, compared the way people expect.</summary>
public sealed partial record AppVersion(int Major, int Minor, int Patch, string PreRelease) : IComparable<AppVersion>
{
    public bool IsPreRelease => PreRelease.Length > 0;

    public static AppVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Pattern().Match(text.Trim());
        if (!m.Success) return null;
        return new AppVersion(
            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0,
            m.Groups[4].Success ? m.Groups[4].Value : "");
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null) return 1;
        int c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        // A release is newer than any of its pre-releases: 0.2.0 > 0.2.0-beta.2.
        if (!IsPreRelease || !other.IsPreRelease) return IsPreRelease == other.IsPreRelease ? 0 : IsPreRelease ? -1 : 1;
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (IsPreRelease ? "-" + PreRelease : "");

    private static int ComparePreRelease(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            bool na = int.TryParse(pa[i], out var ia), nb = int.TryParse(pb[i], out var ib);
            int c = na && nb ? ia.CompareTo(ib) : na ? -1 : nb ? 1 : string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
    }

    [GeneratedRegex(@"^v?(\d+)\.(\d+)(?:\.(\d+))?(?:-([0-9A-Za-z.\-]+))?(?:\+.*)?$")]
    private static partial Regex Pattern();
}

/// <summary>A downloadable file in a release, with the SHA-256 fingerprint GitHub publishes for it.</summary>
public sealed record ReleaseFile(string Name, string Url, long Size, string? Sha256);

/// <summary>One published version of BookHeart.</summary>
public sealed record ReleaseInfo(AppVersion Version, string Title, string Notes, string PageUrl, ReleaseFile? Installer, ReleaseFile? Portable)
{
    public bool IsPreRelease => Version.IsPreRelease;
}

/// <summary>Reads the list of BookHeart releases from GitHub and decides whether one is worth offering.</summary>
public static partial class UpdateFeed
{
    public const string ReleasesUrl = "https://api.github.com/repos/James-Trimble/BookHeart/releases?per_page=20";

    /// <summary>Reads GitHub's release list. Drafts, unreadable versions and releases without downloads are left out.</summary>
    public static List<ReleaseInfo> Parse(string json)
    {
        var list = new List<ReleaseInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (Bool(r, "draft")) continue;
            var version = AppVersion.TryParse(Str(r, "tag_name"));
            if (version is null) continue;
            // A release marked pre-release on GitHub counts as a test version even without a suffix.
            if (Bool(r, "prerelease") && !version.IsPreRelease) version = version with { PreRelease = "pre" };

            ReleaseFile? installer = null, portable = null;
            if (r.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = Str(a, "name") ?? "";
                    var file = new ReleaseFile(name, Str(a, "browser_download_url") ?? "",
                        a.TryGetProperty("size", out var s) && s.TryGetInt64(out var n) ? n : 0, Sha256Of(Str(a, "digest")));
                    if (file.Url.Length == 0) continue;
                    if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase)) installer = file;
                    else if (name.EndsWith("-Portable.zip", StringComparison.OrdinalIgnoreCase)) portable = file;
                }
            }
            if (installer is null && portable is null) continue;
            list.Add(new ReleaseInfo(version, Str(r, "name") ?? "BookHeart " + version, MarkdownToText(Str(r, "body") ?? ""),
                Str(r, "html_url") ?? "", installer, portable));
        }
        return list;
    }

    /// <summary>
    /// The newest release that is newer than the running version, or null. Test versions are only offered
    /// when asked for; a version you chose to skip is not offered unless <paramref name="ignoreSkipped"/>.
    /// </summary>
    public static ReleaseInfo? FindUpdate(IEnumerable<ReleaseInfo> releases, AppVersion current, bool includePreReleases,
        string? skippedVersion, bool ignoreSkipped = false)
    {
        var newest = releases
            .Where(r => includePreReleases || !r.IsPreRelease)
            .Where(r => r.Version > current)
            .MaxBy(r => r.Version);
        if (newest is null) return null;
        if (!ignoreSkipped && skippedVersion is not null && AppVersion.TryParse(skippedVersion) is { } skipped && newest.Version.CompareTo(skipped) <= 0)
            return null;
        return newest;
    }

    /// <summary>
    /// Turns release notes written in Markdown into plain text that reads well aloud: headings and list
    /// items each become their own paragraph, with no symbols for a screen reader to read out.
    /// </summary>
    public static string MarkdownToText(string markdown)
    {
        var paragraphs = new List<string>();
        var current = new List<string>();
        void Flush()
        {
            if (current.Count > 0) paragraphs.Add(string.Join(" ", current));
            current.Clear();
        }
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) { Flush(); continue; }
            bool startsBlock = Heading().IsMatch(line) || Bullet().IsMatch(line);
            line = Heading().Replace(line, "");
            line = Bullet().Replace(line, "");
            line = Link().Replace(line, "$1");
            line = Emphasis().Replace(line, "$2");
            line = line.Replace("`", "");
            if (startsBlock) Flush();
            current.Add(line);
            if (startsBlock && raw.TrimStart().StartsWith('#')) Flush();
        }
        Flush();
        return string.Join("\n\n", paragraphs);
    }

    private static string? Sha256Of(string? digest) =>
        digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    [GeneratedRegex(@"^#{1,6}\s*")] private static partial Regex Heading();
    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")] private static partial Regex Link();
    [GeneratedRegex(@"(\*\*|__|\*|_)(.+?)\1")] private static partial Regex Emphasis();
    [GeneratedRegex(@"^\s*[-*+]\s+")] private static partial Regex Bullet();
}
