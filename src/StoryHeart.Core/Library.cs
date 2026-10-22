using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryHeart.Core;

public sealed class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Seconds into the file named by <see cref="Track"/> (or into the book, for a single file).</summary>
    public double Time { get; set; }
    public string Note { get; set; } = "";
    public string Chapter { get; set; } = "";
    /// <summary>For a book made of several files: the file name this bookmark is in.</summary>
    public string? Track { get; set; }

    [JsonIgnore]
    public string Name => Note.Length > 0 ? Note : Chapter.Length > 0 ? Chapter : "Bookmark";
}

/// <summary>
/// A book's own audio settings. Anything left empty follows the defaults in <see cref="Settings"/>.
/// </summary>
public sealed class BookAudio
{
    public double? Speed { get; set; }
    public int? Volume { get; set; }
    public int? SkipSeconds { get; set; }

    [JsonIgnore] public bool IsEmpty => Speed is null && Volume is null && SkipSeconds is null;
}

/// <summary>One book in the library: its files, where you stopped, and its bookmarks.</summary>
public sealed class BookEntry
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>True when you have renamed the book, so the title isn't replaced by one made from the file names.</summary>
    public bool TitleEdited { get; set; }
    /// <summary>Full paths of the book's files, in the order they play.</summary>
    public List<string> Files { get; set; } = [];
    public long Size { get; set; }
    /// <summary>Length of the whole book in seconds, or 0 while not known.</summary>
    public double Duration { get; set; }
    /// <summary>Lengths of each file in seconds (0 for not read yet). Only used for multi-file books.</summary>
    public List<double> TrackDurations { get; set; } = [];
    public int TrackIndex { get; set; }
    /// <summary>Seconds into the current file.</summary>
    public double Position { get; set; }
    /// <summary>When you last listened, or <see cref="DateTimeOffset.MinValue"/> if never.</summary>
    public DateTimeOffset Opened { get; set; }
    public DateTimeOffset Added { get; set; }
    public bool Finished { get; set; }
    public List<Bookmark> Bookmarks { get; set; } = [];
    public BookAudio Audio { get; set; } = new();

    [JsonIgnore] public bool EverPlayed => Opened > DateTimeOffset.MinValue;

    [JsonIgnore] public bool IsSet => Files.Count > 1;
    [JsonIgnore] public bool IsStarted => IsSet ? TrackIndex > 0 || Position > 5 : Position > 5;

    /// <summary>Seconds from the start of the book, when every file's length is known.</summary>
    [JsonIgnore]
    public double? Overall
    {
        get
        {
            if (!IsSet) return Position;
            if (TrackDurations.Count != Files.Count || TrackDurations.Any(d => d <= 0)) return null;
            return TrackDurations.Take(TrackIndex).Sum() + Position;
        }
    }

    [JsonIgnore]
    public double? ProgressFraction => Finished ? 1 : Duration > 0 && Overall is { } o ? Math.Clamp(o / Duration, 0, 1) : null;

    /// <summary>Seconds left to listen at normal speed, when known.</summary>
    [JsonIgnore]
    public double? Remaining => Duration > 0 && Overall is { } o ? Math.Max(0, Duration - o) : null;

    /// <summary>Makes a library entry for files that haven't been played yet.</summary>
    public static BookEntry Create(IReadOnlyList<string> files)
    {
        var list = files.ToList();
        return new BookEntry
        {
            Key = KeyFor(list),
            Title = list.Count > 1 ? BookNaming.SetTitle(list.Select(Path.GetFileName).ToList()!) : Path.GetFileNameWithoutExtension(list[0]),
            Files = list,
            Size = list.Sum(SizeOf),
            TrackDurations = list.Count > 1 ? list.Select(_ => 0.0).ToList() : [],
            Added = DateTimeOffset.Now,
            Opened = DateTimeOffset.MinValue,
        };
    }

    public List<string> MissingFiles() => Files.Where(f => !File.Exists(f)).ToList();

    /// <summary>Identifies a book by its file names and sizes, so it is recognised again even if moved.</summary>
    public static string KeyFor(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1) return "book:" + Path.GetFileName(paths[0]) + ":" + SizeOf(paths[0]);
        var names = string.Join('|', NaturalComparer.Sort(paths).Select(Path.GetFileName));
        uint h = 5381;
        foreach (var ch in names) h = unchecked((h << 5) + h + ch);
        return $"set:{paths.Count}:{paths.Sum(SizeOf)}:{h:x}";
    }

    public static long SizeOf(string path)
    {
        try { return new FileInfo(path).Length; } catch (IOException) { return 0; } catch (UnauthorizedAccessException) { return 0; }
    }
}

public enum ThemeChoice { System, Light, Dark }

/// <summary>A window's size and position on screen, and whether it was maximised.</summary>
public sealed record WindowPlace(double Left, double Top, double Width, double Height, bool Maximized);

/// <summary>App-wide settings. Speed, volume and skip amount are the defaults for books without their own.</summary>
public sealed class Settings
{
    public const double MinSpeed = 0.5, MaxSpeed = 3;
    public const int MaxVolume = 300;

    public int Volume { get; set; } = 100;
    public double Speed { get; set; } = 1;
    public int SkipSeconds { get; set; } = 30;
    [JsonConverter(typeof(JsonStringEnumConverter<ThemeChoice>))]
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    /// <summary>Open the book you were last listening to when the app starts.</summary>
    public bool OpenLastBookOnStart { get; set; }
    /// <summary>Check for a new version at startup and once a day.</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>Also offer test versions (pre-releases). For testers.</summary>
    public bool IncludeTestVersions { get; set; }
    public DateTimeOffset? LastUpdateCheck { get; set; }
    /// <summary>A version you chose to skip; it isn't offered again, but anything newer is.</summary>
    public string? SkippedVersion { get; set; }
    /// <summary>The version that last ran, so StoryHeart can say when it has been updated.</summary>
    public string? LastVersionRun { get; set; }
    /// <summary>Set just before installing an update: "library" or "player", the screen to come back to.</summary>
    public string? ResumeView { get; set; }
    /// <summary>Set just before installing an update: the book that was open, if any.</summary>
    public string? ResumeBookKey { get; set; }
    /// <summary>Where the main window was and how big, so it opens the same way next time.</summary>
    public WindowPlace? Window { get; set; }

    public double SpeedFor(BookEntry? b) => Math.Clamp(b?.Audio.Speed ?? Speed, MinSpeed, MaxSpeed);
    public int VolumeFor(BookEntry? b) => Math.Clamp(b?.Audio.Volume ?? Volume, 0, MaxVolume);
    public int SkipFor(BookEntry? b) => Math.Max(1, b?.Audio.SkipSeconds ?? SkipSeconds);
}

/// <summary>Keeps the library and settings in a JSON file under the user's local app data.</summary>
public sealed class LibraryStore
{
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;

    public Settings Settings { get; private set; } = new();
    public List<BookEntry> Books { get; private set; } = [];

    public LibraryStore(string path) => _path = path;

    public static LibraryStore Load(string path)
    {
        var store = new LibraryStore(path);
        try
        {
            if (File.Exists(path))
            {
                var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), Json);
                if (data is not null)
                {
                    store.Settings = data.Settings ?? new();
                    store.Books = data.Books ?? [];
                }
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged file shouldn't stop the app opening; keep it aside so nothing is lost.
            try { File.Copy(path, path + ".damaged", true); } catch (IOException) { }
        }
        return store;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new Data { Settings = Settings, Books = Books }, Json));
            File.Move(tmp, _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public BookEntry? Find(string key) => Books.FirstOrDefault(b => b.Key == key);

    public IReadOnlyList<BookEntry> Recent() => Books.OrderByDescending(b => b.Opened).ThenByDescending(b => b.Added).ToList();

    /// <summary>The book you listened to most recently, if any.</summary>
    public BookEntry? LastPlayed => Books.Where(b => b.EverPlayed).MaxBy(b => b.Opened);

    /// <summary>Adds a book, replacing any entry with the same key.</summary>
    public BookEntry Add(BookEntry entry)
    {
        Books.RemoveAll(b => b.Key == entry.Key && b != entry);
        if (!Books.Contains(entry)) Books.Add(entry);
        return entry;
    }

    public void Remove(string key) => Books.RemoveAll(b => b.Key == key);

    private sealed class Data
    {
        public int Version { get; set; } = 1;
        public Settings? Settings { get; set; }
        public List<BookEntry>? Books { get; set; }
    }
}
