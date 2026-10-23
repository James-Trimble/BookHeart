using System.Text.RegularExpressions;

namespace StoryHeart.Core;

/// <summary>What a file's tags say, before deciding what's useful. Empty values are null.</summary>
public sealed record FileTags(string? Title, string? Album, string? Artist, string? AlbumArtist, string? Composer, string? Grouping);

/// <summary>A book's title, author, narrator and series, worked out from the tags in its files.</summary>
public sealed partial record BookDetails(string? Title, string? Author, string? Narrator, string? Series)
{
    /// <summary>
    /// Picks the book's details from a file's tags. For a book made of several files, each file's own
    /// title is usually a chapter name, so the album is the book's title. Values that say nothing,
    /// like "Unknown artist" or "Track 01", are ignored, and the caller falls back to the file names.
    /// </summary>
    public static BookDetails From(FileTags tags, bool isSet)
    {
        var title = isSet ? Useful(tags.Album) : Useful(tags.Title) ?? Useful(tags.Album);
        var author = Useful(tags.AlbumArtist) ?? Useful(tags.Artist);
        // Audiobooks often put the narrator in the composer tag, or in the artist tag next to the author.
        var narrator = Useful(tags.Composer);
        if (narrator is null && Useful(tags.Artist) is { } artist && author is not null && !Same(artist, author)) narrator = artist;
        if (narrator is not null && author is not null && Same(narrator, author)) narrator = null;
        return new BookDetails(title, author, narrator, Useful(tags.Grouping));
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>The text, tidied, or null if it's empty or says nothing about the book.</summary>
    public static string? Useful(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = Spaces().Replace(text.Trim(), " ");
        return Junk().IsMatch(text) ? null : text;
    }

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();

    [GeneratedRegex(@"^(unknown( (artist|album|title|author))?|various( artists)?|untitled|audiobook|audio ?book|no title|none|n/?a|(track|chapter|part|disc|cd)[\s_\-.]*\d+|\d{1,3})$",
        RegexOptions.IgnoreCase)]
    private static partial Regex Junk();
}
