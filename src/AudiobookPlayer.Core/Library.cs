using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudiobookPlayer.Core;

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

/// <summary>One book in the library: its files, where you stopped, and its bookmarks.</summary>
public sealed class BookEntry
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
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
    public DateTimeOffset Opened { get; set; }
    public List<Bookmark> Bookmarks { get; set; } = [];

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
    public double? ProgressFraction => Duration > 0 && Overall is { } o ? Math.Clamp(o / Duration, 0, 1) : null;

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

public sealed class Settings
{
    public int Volume { get; set; } = 100;
    public double Speed { get; set; } = 1;
    public int SkipSeconds { get; set; } = 30;
}

/// <summary>Keeps the library and settings in a JSON file under the user's local app data.</summary>
public sealed class LibraryStore
{
    public const int MaxBooks = 200;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;

    public Settings Settings { get; private set; } = new();
    public List<BookEntry> Books { get; private set; } = [];

    public LibraryStore(string path) => _path = path;

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudiobookPlayer", "library.json");

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

    public IReadOnlyList<BookEntry> Recent() => Books.OrderByDescending(b => b.Opened).ToList();

    public BookEntry Add(BookEntry entry)
    {
        Books.RemoveAll(b => b.Key == entry.Key);
        Books.Add(entry);
        if (Books.Count > MaxBooks)
            Books = Books.OrderByDescending(b => b.Opened).Take(MaxBooks).ToList();
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
