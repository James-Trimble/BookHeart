using StoryHeart.Core;

namespace StoryHeart.Core.Tests;

public class BookDetailsTests
{
    [Fact]
    public void OneFileBookUsesItsTitle()
    {
        var d = BookDetails.From(new FileTags("Project Hail Mary", "Project Hail Mary (Unabridged)", "Andy Weir", null, "Ray Porter", "Standalone"), isSet: false);
        Assert.Equal(new BookDetails("Project Hail Mary", "Andy Weir", "Ray Porter", "Standalone"), d);
    }

    [Fact]
    public void BookOfSeveralFilesUsesTheAlbumBecauseEachTitleIsAChapter()
    {
        var d = BookDetails.From(new FileTags("Chapter 1: The Boy", "The Way of Kings", "Brandon Sanderson", null, null, null), isSet: true);
        Assert.Equal("The Way of Kings", d.Title);
    }

    [Fact]
    public void UselessValuesAreIgnored()
    {
        var d = BookDetails.From(new FileTags("Track 01", "Unknown Album", "Unknown artist", "Various Artists", "  ", null), isSet: false);
        Assert.Equal(new BookDetails(null, null, null, null), d);
        Assert.Null(BookDetails.Useful("03"));
        Assert.Equal("1984", BookDetails.Useful("1984"));
        Assert.Equal("Dune Messiah", BookDetails.Useful("  Dune   Messiah "));
    }

    [Fact]
    public void NarratorCanBeTheArtistWhenTheAuthorIsTheAlbumArtist()
    {
        var d = BookDetails.From(new FileTags("Dune", null, "Scott Brick", "Frank Herbert", null, null), isSet: false);
        Assert.Equal("Frank Herbert", d.Author);
        Assert.Equal("Scott Brick", d.Narrator);
        Assert.Null(BookDetails.From(new FileTags("Dune", null, "Frank Herbert", null, "Frank Herbert", null), false).Narrator);
    }

    [Fact]
    public void RenamedBooksKeepTheirTitle()
    {
        var book = BookEntry.Create(["/books/hail_mary.m4b"]);
        book.TitleEdited = true;
        book.Title = "My title";
        book.ApplyDetails(new BookDetails("Project Hail Mary", "Andy Weir", null, null));
        Assert.Equal("My title", book.Title);
        Assert.Equal("by Andy Weir", book.Byline);
        Assert.True(book.DetailsRead);

        var other = BookEntry.Create(["/books/hail_mary.m4b"]);
        other.ApplyDetails(new BookDetails("Project Hail Mary", "Andy Weir", "Ray Porter", null));
        Assert.Equal("Project Hail Mary", other.Title);
        Assert.Equal("by Andy Weir, read by Ray Porter", other.Byline);
    }
}

public sealed class TagReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("storyheart-tags-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>A few silent MP3 frames, enough for TagLib to recognise the file.</summary>
    private string MakeMp3(string name)
    {
        var path = Path.Combine(_dir, name);
        var frame = new byte[417];
        frame[0] = 0xFF; frame[1] = 0xFB; frame[2] = 0x90; frame[3] = 0x64;
        using (var f = File.Create(path))
            for (int i = 0; i < 20; i++) f.Write(frame);
        return path;
    }

    [Fact]
    public void ReadsTagsAndCoverFromAFile()
    {
        var path = MakeMp3("book.mp3");
        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Title = "Project Hail Mary";
            file.Tag.Performers = ["Andy Weir"];
            file.Tag.Composers = ["Ray Porter"];
            file.Tag.Pictures = [new TagLib.Picture(new TagLib.ByteVector([1, 2, 3])) { Type = TagLib.PictureType.FrontCover, MimeType = "image/jpeg" }];
            file.Save();
        }
        Assert.Equal(new BookDetails("Project Hail Mary", "Andy Weir", "Ray Porter", null), TagReader.ReadBook([path]));
        Assert.Equal(new byte[] { 1, 2, 3 }, TagReader.ReadCover(path));
    }

    [Fact]
    public void FilesThatArentAudioGiveNothing()
    {
        var path = Path.Combine(_dir, "notes.mp3");
        File.WriteAllText(path, "not audio");
        Assert.Null(TagReader.ReadCover(path));
        Assert.Null(TagReader.ReadCover(Path.Combine(_dir, "missing.mp3")));
    }
}
