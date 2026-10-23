namespace StoryHeart.Core;

/// <summary>Reads the tags and cover picture inside audio files, using TagLib#.</summary>
public static class TagReader
{
    /// <summary>The file's tags, or null if it has none or can't be read.</summary>
    public static FileTags? Read(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path, TagLib.ReadStyle.None);
            var t = file.Tag;
            return new FileTags(t.Title, t.Album, First(t.Performers), First(t.AlbumArtists), First(t.Composers), t.Grouping);
        }
        catch (Exception e) when (e is TagLib.UnsupportedFormatException or TagLib.CorruptFileException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The book's details from its first file, or null if it has no useful tags.</summary>
    public static BookDetails? ReadBook(IReadOnlyList<string> files)
    {
        if (files.Count == 0 || Read(files[0]) is not { } tags) return null;
        return BookDetails.From(tags, files.Count > 1);
    }

    /// <summary>The cover picture in the file, as image bytes, or null if there isn't one.</summary>
    public static byte[]? ReadCover(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path);
            var pictures = file.Tag.Pictures;
            var cover = pictures.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover) ?? pictures.FirstOrDefault();
            return cover?.Data?.Data is { Length: > 0 } data ? data : null;
        }
        catch (Exception e) when (e is TagLib.UnsupportedFormatException or TagLib.CorruptFileException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? First(string[]? values) => values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
