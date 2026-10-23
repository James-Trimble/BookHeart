using System.Text.RegularExpressions;

namespace StoryHeart.Core;

/// <summary>Reads the licence agreement's "Last updated" line, so StoryHeart can tell when the agreement changes.</summary>
public static partial class Agreement
{
    /// <summary>The date after "Last updated:", or null if the agreement doesn't have one.</summary>
    public static string? LastUpdated(string text) =>
        LastUpdatedLine().Match(text) is { Success: true } m ? m.Groups[1].Value.Trim() : null;

    [GeneratedRegex(@"^\s*Last updated:\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex LastUpdatedLine();
}
