namespace StoryHeart.Core;

/// <summary>
/// Brings a library over from a folder the app used under an earlier name, so renaming the app
/// never loses anyone's books, places, bookmarks or settings.
/// </summary>
public static class OldData
{
    public const string LibraryFile = "library.json";

    /// <summary>
    /// Moves everything in <paramref name="oldDir"/> to <paramref name="newDir"/>, unless the new folder
    /// already has a library. If the folder can't be moved in one go, its files are copied instead and the
    /// old folder is left as it was. Returns true if a library was brought over.
    /// </summary>
    public static bool MoveFolder(string oldDir, string newDir)
    {
        if (!File.Exists(Path.Combine(oldDir, LibraryFile)) || File.Exists(Path.Combine(newDir, LibraryFile))) return false;
        try
        {
            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(newDir))!);
                Directory.Move(oldDir, newDir);
                return true;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return CopyFolder(oldDir, newDir);
    }

    /// <summary>
    /// Copies the files in <paramref name="oldDir"/> into <paramref name="newDir"/> without replacing any that
    /// are already there, unless the new folder already has a library. Returns true if a library was brought over.
    /// </summary>
    public static bool CopyFolder(string oldDir, string newDir)
    {
        if (!File.Exists(Path.Combine(oldDir, LibraryFile)) || File.Exists(Path.Combine(newDir, LibraryFile))) return false;
        try
        {
            foreach (var file in Directory.EnumerateFiles(oldDir, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(newDir, Path.GetRelativePath(oldDir, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!File.Exists(target)) File.Copy(file, target);
            }
            return File.Exists(Path.Combine(newDir, LibraryFile));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
