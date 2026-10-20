using StoryHeart.Core;

namespace StoryHeart.Core.Tests;

public class LibraryScannerTests
{
    private static string Make(DirectoryInfo root, string relative)
    {
        var path = Path.Combine(root.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void EachFolderIsABook()
    {
        var root = Directory.CreateTempSubdirectory();
        Make(root, "Dune/Dune 02.mp3");
        Make(root, "Dune/Dune 01.mp3");
        Make(root, "Emma/Emma.mp3");
        Make(root, "Emma/cover.jpg");

        var books = LibraryScanner.FindBooks(root.FullName);
        Assert.Equal(2, books.Count);
        Assert.Equal(["Dune 01.mp3", "Dune 02.mp3"], books[0].Select(Path.GetFileName));
        Assert.Equal(["Emma.mp3"], books[1].Select(Path.GetFileName));
    }

    [Fact]
    public void DiscFoldersJoinTheirBook()
    {
        var root = Directory.CreateTempSubdirectory();
        Make(root, "Hobbit/CD 2/01.mp3");
        Make(root, "Hobbit/CD 1/01.mp3");
        Make(root, "Hobbit/CD 1/02.mp3");

        var book = Assert.Single(LibraryScanner.FindBooks(root.FullName));
        Assert.Equal(3, book.Count);
        Assert.Contains("CD 1", book[0]);
        Assert.Contains("CD 2", book[2]);
    }

    [Fact]
    public void SeveralM4bFilesAreSeparateBooks()
    {
        var root = Directory.CreateTempSubdirectory();
        Make(root, "Audiobooks/Book A.m4b");
        Make(root, "Audiobooks/Book B.m4b");

        var books = LibraryScanner.FindBooks(root.FullName);
        Assert.Equal(2, books.Count);
        Assert.All(books, b => Assert.Single(b));
    }

    [Fact]
    public void EmptyFolderHasNoBooks() => Assert.Empty(LibraryScanner.FindBooks(Directory.CreateTempSubdirectory().FullName));
}
