using System.Text.RegularExpressions;

namespace BookHeart.Core;

/// <summary>Works out which audio files belong together as books when you add a folder to the library.</summary>
public static partial class LibraryScanner
{
    /// <summary>
    /// Finds the books in a folder and its subfolders. Each folder of audio files is one book, except:
    /// folders named like "Disc 2" or "CD 1" join the book in the folder above them, and a folder of
    /// several .m4b files is several books (an .m4b file is normally a whole book).
    /// </summary>
    public static List<List<string>> FindBooks(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(BookNaming.IsAudioFile);
        return GroupIntoBooks(files);
    }

    public static List<List<string>> GroupIntoBooks(IEnumerable<string> files)
    {
        var books = new List<List<string>>();
        var byRoot = files
            .GroupBy(BookRoot, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, NaturalComparer.Instance);
        foreach (var group in byRoot)
        {
            var sorted = NaturalComparer.Sort(group);
            if (sorted.Count > 1 && sorted.All(f => Path.GetExtension(f).Equals(".m4b", StringComparison.OrdinalIgnoreCase)))
                books.AddRange(sorted.Select(f => new List<string> { f }));
            else
                books.Add(sorted);
        }
        return books;
    }

    private static string BookRoot(string file)
    {
        var dir = Path.GetDirectoryName(file) ?? "";
        var parent = Path.GetDirectoryName(dir);
        return parent is not null && PartFolder().IsMatch(Path.GetFileName(dir)) ? parent : dir;
    }

    [GeneratedRegex(@"^(disc|disk|cd|part|pt)[\s_\-.]*\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex PartFolder();
}
