using AudiobookPlayer.Core;

namespace AudiobookPlayer.Core.Tests;

public class LibraryTests
{
    [Fact]
    public void SavesAndLoadsBooksAndSettings()
    {
        var dir = Directory.CreateTempSubdirectory();
        var path = Path.Combine(dir.FullName, "library.json");
        var store = new LibraryStore(path);
        store.Settings.Volume = 150;
        store.Add(new BookEntry
        {
            Key = "book:a.m4b:10", Title = "A", Files = [@"C:\Books\a.m4b"], Position = 42.5, Duration = 100,
            Opened = DateTimeOffset.Now, Bookmarks = [new Bookmark { Time = 10, Note = "Good bit" }],
        });
        store.Save();

        var loaded = LibraryStore.Load(path);
        Assert.Equal(150, loaded.Settings.Volume);
        var book = Assert.Single(loaded.Books);
        Assert.Equal(42.5, book.Position);
        Assert.Equal("Good bit", Assert.Single(book.Bookmarks).Name);
        Assert.Equal(0.425, book.ProgressFraction!.Value, 3);
    }

    [Fact]
    public void DamagedFileStartsEmpty()
    {
        var dir = Directory.CreateTempSubdirectory();
        var path = Path.Combine(dir.FullName, "library.json");
        File.WriteAllText(path, "{ not json");
        Assert.Empty(LibraryStore.Load(path).Books);
        Assert.True(File.Exists(path + ".damaged"));
    }

    [Fact]
    public void OverallPositionNeedsAllLengths()
    {
        var e = new BookEntry { Files = ["a", "b", "c"], TrackDurations = [100, 200, 0], TrackIndex = 1, Position = 5 };
        Assert.Null(e.Overall);
        e.TrackDurations[2] = 50;
        Assert.Equal(105, e.Overall);
    }

    [Fact]
    public void SetKeyDoesNotDependOnOrder()
    {
        Assert.Equal(BookEntry.KeyFor(["x/2.mp3", "x/1.mp3"]), BookEntry.KeyFor(["x/1.mp3", "x/2.mp3"]));
    }

    [Fact]
    public void RecentIsNewestFirst()
    {
        var store = new LibraryStore("unused");
        store.Add(new BookEntry { Key = "old", Opened = DateTimeOffset.Now.AddDays(-2) });
        store.Add(new BookEntry { Key = "new", Opened = DateTimeOffset.Now });
        Assert.Equal(["new", "old"], store.Recent().Select(b => b.Key));
    }
}

public class SettingsTests
{
    [Fact]
    public void BookSettingsOverrideDefaults()
    {
        var s = new Settings { Speed = 1.25, Volume = 120, SkipSeconds = 10 };
        var book = new BookEntry();
        Assert.Equal(1.25, s.SpeedFor(book));
        book.Audio.Speed = 2;
        book.Audio.Volume = 400;
        Assert.Equal(2, s.SpeedFor(book));
        Assert.Equal(Settings.MaxVolume, s.VolumeFor(book));
        Assert.Equal(10, s.SkipFor(book));
        Assert.Equal(1.25, s.SpeedFor(null));
    }

    [Fact]
    public void NewBookIsNotPlayedYet()
    {
        var b = BookEntry.Create(["x/Dune 1.mp3", "x/Dune 2.mp3"]);
        Assert.Equal("Dune", b.Title);
        Assert.False(b.EverPlayed);
        Assert.Equal(2, b.TrackDurations.Count);
    }
}
