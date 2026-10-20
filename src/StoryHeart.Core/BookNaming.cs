using System.Text.RegularExpressions;

namespace StoryHeart.Core;

/// <summary>Works out a book title from its file names, and short chapter names for each file.</summary>
public static partial class BookNaming
{
    public static readonly string[] AudioExtensions =
        [".m4b", ".m4a", ".mp4", ".mp3", ".aac", ".ogg", ".oga", ".opus", ".wav", ".flac", ".wma"];

    public static bool IsAudioFile(string path) =>
        AudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>The longest shared start of the file names, with any trailing "Chapter 1" style text removed.</summary>
    public static string SetTitle(IReadOnlyList<string> fileNames)
    {
        var names = fileNames.Select(Path.GetFileNameWithoutExtension).Select(n => n ?? "").ToList();
        if (names.Count == 0) return "";
        var prefix = names[0];
        foreach (var n in names)
            while (prefix.Length > 0 && !n.StartsWith(prefix, StringComparison.Ordinal))
                prefix = prefix[..^1];

        // Don't cut a word in half: "Book One" and "Book Two" share "Book ", not "Book T".
        if (prefix.Length > 0 && char.IsLetter(prefix[^1]) &&
            names.Any(n => n.Length > prefix.Length && char.IsLetter(n[prefix.Length])))
        {
            var m = LastWord().Match(prefix);
            prefix = m.Success && m.Index > 0 ? prefix[..m.Index] : "";
        }
        prefix = TrailingPartLabel().Replace(prefix, "").Trim();
        return prefix.Length >= 3 ? prefix : names.Count + " audio files";
    }

    /// <summary>A file's name with the book title removed from the front, when that leaves something.</summary>
    public static string TrackTitle(string fileName, string bookTitle)
    {
        var n = Path.GetFileNameWithoutExtension(fileName);
        if (bookTitle.Length > 0 && n.StartsWith(bookTitle, StringComparison.OrdinalIgnoreCase))
        {
            var rest = LeadingSeparators().Replace(n[bookTitle.Length..], "");
            if (rest.Length > 0) return rest;
        }
        return n;
    }

    [GeneratedRegex(@"[\s\-–_:,.]+[^\s\-–_:,.]*$")]
    private static partial Regex LastWord();

    [GeneratedRegex(@"[\s\-–_:,.#(\[]*(chapter|ch|part|pt|episode|ep|track|disc|cd|book)?[\s\-–_.#]*\d*[\s\-–_:,.#(\[]*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingPartLabel();

    [GeneratedRegex(@"^[\s\-–_:,.]+")]
    private static partial Regex LeadingSeparators();
}
