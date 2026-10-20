using StoryHeart.Core;

namespace StoryHeart.Core.Tests;

public sealed class OldDataTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("storyheart-tests-").FullName;
    private string OldDir => Path.Combine(_root, "BookHeart");
    private string NewDir => Path.Combine(_root, "StoryHeart");

    public void Dispose() => Directory.Delete(_root, true);

    private void MakeOldLibrary()
    {
        Directory.CreateDirectory(Path.Combine(OldDir, "covers"));
        File.WriteAllText(Path.Combine(OldDir, "library.json"), "old library");
        File.WriteAllText(Path.Combine(OldDir, "error.log"), "old log");
        File.WriteAllText(Path.Combine(OldDir, "covers", "a.jpg"), "picture");
    }

    [Fact]
    public void MovesTheWholeFolder()
    {
        MakeOldLibrary();
        Assert.True(OldData.MoveFolder(OldDir, NewDir));
        Assert.Equal("old library", File.ReadAllText(Path.Combine(NewDir, "library.json")));
        Assert.True(File.Exists(Path.Combine(NewDir, "covers", "a.jpg")));
        Assert.False(Directory.Exists(OldDir));
    }

    [Fact]
    public void CopiesIntoAFolderThatAlreadyExistsWithoutALibrary()
    {
        MakeOldLibrary();
        Directory.CreateDirectory(NewDir);
        File.WriteAllText(Path.Combine(NewDir, "error.log"), "new log");
        Assert.True(OldData.MoveFolder(OldDir, NewDir));
        Assert.Equal("old library", File.ReadAllText(Path.Combine(NewDir, "library.json")));
        Assert.Equal("new log", File.ReadAllText(Path.Combine(NewDir, "error.log")));
        Assert.True(File.Exists(Path.Combine(NewDir, "covers", "a.jpg")));
        // The old folder stays as it was, in case anything went wrong.
        Assert.True(File.Exists(Path.Combine(OldDir, "library.json")));
    }

    [Fact]
    public void NeverReplacesALibraryThatIsAlreadyThere()
    {
        MakeOldLibrary();
        Directory.CreateDirectory(NewDir);
        File.WriteAllText(Path.Combine(NewDir, "library.json"), "new library");
        Assert.False(OldData.MoveFolder(OldDir, NewDir));
        Assert.False(OldData.CopyFolder(OldDir, NewDir));
        Assert.Equal("new library", File.ReadAllText(Path.Combine(NewDir, "library.json")));
        Assert.True(File.Exists(Path.Combine(OldDir, "library.json")));
    }

    [Fact]
    public void DoesNothingWithoutAnOldLibrary()
    {
        Assert.False(OldData.MoveFolder(OldDir, NewDir));
        Directory.CreateDirectory(OldDir);
        Assert.False(OldData.MoveFolder(OldDir, NewDir));
        Assert.False(Directory.Exists(NewDir));
    }
}
