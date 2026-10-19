using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AudiobookPlayer.Audio;
using AudiobookPlayer.Core;
using AudiobookPlayer.Platform;
using Microsoft.Win32;

namespace AudiobookPlayer;

public partial class MainWindow : Window
{
    private static readonly (double Value, string Label)[] Speeds =
        [(0.75, "0.75 times"), (1, "Normal"), (1.1, "1.1 times"), (1.25, "1.25 times"), (1.5, "1.5 times"),
         (1.75, "1.75 times"), (2, "2 times"), (2.5, "2.5 times"), (3, "3 times")];
    private static readonly (int Seconds, string Label)[] Skips =
        [(10, "10 seconds"), (30, "30 seconds"), (60, "1 minute"), (300, "5 minutes")];

    private readonly LibraryStore _store = LibraryStore.Load(LibraryStore.DefaultPath);
    private readonly AudioEngine _engine = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Announcer _say;
    private MediaControls? _media;
    private readonly string[] _startupFiles;

    private BookEntry? _book;
    private IReadOnlyList<Chapter> _chapters = [];
    private int _shownChapter = -2;
    private DateTime _lastSave;
    private bool _updatingUi;
    private CancellationTokenSource? _probe;

    private readonly ObservableCollection<Row> _orderRows = [];
    private List<string> _orderFiles = [];
    private readonly ObservableCollection<Row> _recentRows = [];
    private readonly ObservableCollection<Row> _bookmarkRows = [];
    private readonly ObservableCollection<Row> _chapterRows = [];

    public MainWindow(string[] startupFiles)
    {
        InitializeComponent();
        _startupFiles = startupFiles;
        _say = new Announcer(this);
        _say.Announced += m => StatusText.Text = m;

        OrderList.ItemsSource = _orderRows;
        RecentList.ItemsSource = _recentRows;
        BmList.ItemsSource = _bookmarkRows;
        ChList.ItemsSource = _chapterRows;
        foreach (var (_, label) in Speeds) SpeedBox.Items.Add(label);
        foreach (var (_, label) in Skips) SkipBox.Items.Add(label);
        BuildShortcutList();
        WireEvents();

        _engine.Ended += (_, _) => OnTrackEnded();
        _engine.Failed += (_, ex) => { UpdatePlayState(); _say.Say("The sound device stopped: " + ex.Message); };
        _tick.Tick += (_, _) => OnTick();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    // ---------------------------------------------------------------- startup and shutdown

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _media = new MediaControls(this);
        _media.PlayPressed += () => { if (!_engine.IsPlaying) TogglePlay(); };
        _media.PausePressed += () => { if (_engine.IsPlaying) TogglePlay(); };
        _media.NextPressed += () => { if (HasChapters) NextChapter(); else Skip(1); };
        _media.PreviousPressed += () => { if (HasChapters) PreviousChapter(); else Skip(-1); };
        _media.ForwardPressed += () => Skip(1);
        _media.BackPressed += () => Skip(-1);

        var s = _store.Settings;
        SetVolume(s.Volume, speak: false);
        SpeedBox.SelectedIndex = Math.Max(0, Array.FindIndex(Speeds, x => Math.Abs(x.Value - s.Speed) < 0.001));
        SkipBox.SelectedIndex = Math.Max(0, Array.FindIndex(Skips, x => x.Seconds == s.SkipSeconds));

        RenderLibrary();
        if (_startupFiles.Length > 0) OpenPaths(_startupFiles, autoplay: false);
        else if (ContinueSection.Visibility == Visibility.Visible) ContBtn.Focus();
        else OpenBtn.Focus();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SavePosition();
        _store.Save();
        _probe?.Cancel();
        _engine.Dispose();
    }

    private void WireEvents()
    {
        OpenBtn.Click += (_, _) => PickFiles();
        FolderBtn.Click += (_, _) => PickFolder();
        ContBtn.Click += (_, _) => { if (ContBtn.Tag is BookEntry b) Resume(b); };

        OrderUpBtn.Click += (_, _) => MoveSelected(-1);
        OrderDownBtn.Click += (_, _) => MoveSelected(1);
        OrderToBtn.Click += (_, _) => ShowMoveTo();
        MoveToGo.Click += (_, _) => ApplyMoveTo();
        MoveToCancel.Click += (_, _) => HideMoveTo(announce: true);
        MoveToBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; ApplyMoveTo(); }
            else if (e.Key == Key.Escape) { e.Handled = true; HideMoveTo(announce: true); }
        };
        OrderList.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.Up) { e.Handled = true; MoveSelected(-1); }
            else if (e.Key == Key.Down) { e.Handled = true; MoveSelected(1); }
        };
        OrderGo.Click += (_, _) => { var files = _orderFiles.ToList(); HideOrder(); OpenBook(files, autoplay: false); };
        OrderReset.Click += (_, _) => { SetOrder(NaturalComparer.Sort(_orderFiles), 0); _say.Say("Order reset to sorting by file name."); };
        OrderCancel.Click += (_, _) => { HideOrder(); _say.Say("Cancelled."); OpenBtn.Focus(); };
        ChangeOrderBtn.Click += (_, _) => { if (_book is { IsSet: true }) ShowOrder(_book.Files.ToList()); };

        ActivateOnEnter(RecentList, () => OpenSelectedRecent());
        RecentList.KeyDown += (_, e) => { if (e.Key == Key.Delete) { e.Handled = true; RemoveSelectedRecent(); } };
        RecentOpenBtn.Click += (_, _) => OpenSelectedRecent();
        RecentRemoveBtn.Click += (_, _) => RemoveSelectedRecent();

        PlayBtn.Click += (_, _) => TogglePlay();
        BackBtn.Click += (_, _) => Skip(-1);
        FwdBtn.Click += (_, _) => Skip(1);
        WhereBtn.Click += (_, _) => WhereAmI();
        PrevChBtn.Click += (_, _) => PreviousChapter();
        NextChBtn.Click += (_, _) => NextChapter();

        Seek.ValueChanged += (_, e) =>
        {
            if (_updatingUi || !_engine.IsOpen) return;
            _engine.Position = TimeSpan.FromSeconds(e.NewValue);
            ShowPosition(e.NewValue);
        };
        Volume.ValueChanged += (_, e) => { if (!_updatingUi) SetVolume((int)Math.Round(e.NewValue), speak: false); };
        VolDownBtn.Click += (_, _) => SetVolume(_engine.VolumePercent - 10, speak: true);
        VolUpBtn.Click += (_, _) => SetVolume(_engine.VolumePercent + 10, speak: true);

        SpeedBox.SelectionChanged += (_, _) =>
        {
            if (SpeedBox.SelectedIndex < 0) return;
            _engine.Speed = Speeds[SpeedBox.SelectedIndex].Value;
            _store.Settings.Speed = _engine.Speed;
            _store.Save();
        };
        SkipBox.SelectionChanged += (_, _) =>
        {
            if (SkipBox.SelectedIndex < 0) return;
            var (seconds, label) = Skips[SkipBox.SelectedIndex];
            BackBtn.Content = "Back " + label;
            FwdBtn.Content = "Forward " + label;
            _store.Settings.SkipSeconds = seconds;
            _store.Save();
        };

        AddBmBtn.Click += (_, _) => AddBookmark();
        BmNote.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; AddBookmark(); } };
        ActivateOnEnter(BmList, GoToSelectedBookmark);
        BmList.KeyDown += (_, e) => { if (e.Key == Key.Delete) { e.Handled = true; DeleteSelectedBookmark(); } };
        BmGoBtn.Click += (_, _) => GoToSelectedBookmark();
        BmDeleteBtn.Click += (_, _) => DeleteSelectedBookmark();

        ActivateOnEnter(ChList, PlaySelectedChapter);
        ChGoBtn.Click += (_, _) => PlaySelectedChapter();

        PreviewKeyDown += OnShortcutKey;
        Drop += OnDrop;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private static void ActivateOnEnter(ListBox list, Action action)
    {
        list.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; action(); } };
        list.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(list, d) is ListBoxItem) action();
        };
    }

    // ---------------------------------------------------------------- keyboard shortcuts

    private static readonly (string Keys, string Action)[] ShortcutHelp =
    [
        ("Alt + P", "Play or pause"),
        ("Alt + Comma", "Skip back"),
        ("Alt + Period", "Skip forward"),
        ("Alt + Left arrow", "Previous chapter"),
        ("Alt + Right arrow", "Next chapter"),
        ("Alt + Up arrow", "Volume up"),
        ("Alt + Down arrow", "Volume down"),
        ("Alt + B", "Add a bookmark at the current spot"),
        ("Alt + I", "Hear where you are in the book"),
        ("Alt + R", "Resume the book you listened to most recently"),
        ("Alt + O", "Open a new book"),
        ("Enter", "In a list: open, play or go to the selected item"),
        ("Delete", "In a list: remove the selected bookmark or book"),
    ];

    private void BuildShortcutList()
    {
        for (int i = 0; i < ShortcutHelp.Length; i++)
        {
            KeysGrid.RowDefinitions.Add(new RowDefinition());
            var key = new TextBlock { Text = ShortcutHelp[i].Keys, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 24, 8) };
            var what = new TextBlock { Text = ShortcutHelp[i].Action, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(key, i);
            Grid.SetRow(what, i);
            Grid.SetColumn(what, 1);
            KeysGrid.Children.Add(key);
            KeysGrid.Children.Add(what);
        }
    }

    private void OnShortcutKey(object sender, KeyEventArgs e)
    {
        if (_media is { Available: false })
        {
            Action? media = e.Key switch
            {
                Key.MediaPlayPause => TogglePlay,
                Key.MediaNextTrack => () => { if (HasChapters) NextChapter(); else Skip(1); },
                Key.MediaPreviousTrack => () => { if (HasChapters) PreviousChapter(); else Skip(-1); },
                _ => null,
            };
            if (media is not null) { e.Handled = true; media(); return; }
        }

        if (Keyboard.Modifiers != ModifierKeys.Alt) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        Action? action = key switch
        {
            Key.P => TogglePlay,
            Key.OemComma => () => Skip(-1),
            Key.OemPeriod => () => Skip(1),
            Key.Left => PreviousChapter,
            Key.Right => NextChapter,
            Key.Up => () => SetVolume(_engine.VolumePercent + 10, speak: true),
            Key.Down => () => SetVolume(_engine.VolumePercent - 10, speak: true),
            Key.B => AddBookmark,
            Key.I => WhereAmI,
            Key.R => ResumeLatest,
            Key.O => PickFiles,
            _ => null,
        };
        if (action is null) return;
        e.Handled = true;
        action();
    }

    // ---------------------------------------------------------------- opening books

    private void PickFiles()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open audiobook files",
            Multiselect = true,
            Filter = "Audio files|" + string.Join(';', BookNaming.AudioExtensions.Select(x => "*" + x)) + "|All files|*.*",
        };
        if (dlg.ShowDialog(this) == true) OpenPaths(dlg.FileNames, autoplay: false);
    }

    private void PickFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Choose the folder that holds the book" };
        if (dlg.ShowDialog(this) == true) OpenPaths([dlg.FolderName], autoplay: false);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            Activate();
            OpenPaths(paths, autoplay: false);
        }
    }

    /// <summary>Opens files and folders: one file plays straight away, several files ask you to check their order first.</summary>
    private void OpenPaths(IEnumerable<string> paths, bool autoplay)
    {
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                try { files.AddRange(Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).Where(BookNaming.IsAudioFile)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _say.Say("That folder couldn't be read: " + ex.Message); return; }
            }
            else if (File.Exists(p) && BookNaming.IsAudioFile(p)) files.Add(Path.GetFullPath(p));
        }
        files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0) { _say.Say("No audio files were found in that selection."); return; }
        if (files.Count == 1) { OpenBook(files, autoplay); return; }

        // A set opened before plays in the order you chose last time.
        var known = _store.Find(BookEntry.KeyFor(files));
        if (known is not null && known.Files.Count == files.Count)
        {
            var byName = files.ToDictionary(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);
            var ordered = known.Files.Select(f => byName.GetValueOrDefault(Path.GetFileName(f))).ToList();
            if (ordered.All(f => f is not null)) { OpenBook(ordered!, autoplay); return; }
        }
        ShowOrder(NaturalComparer.Sort(files));
    }

    private void OpenBook(List<string> files, bool autoplay)
    {
        SavePosition();
        _probe?.Cancel();
        _engine.Close();

        var key = BookEntry.KeyFor(files);
        var entry = _store.Find(key) ?? new BookEntry { Key = key };
        bool isSet = files.Count > 1;

        // Keep your place by file name, in case the order or the folder changed.
        var currentName = entry.Files.Count > entry.TrackIndex ? Path.GetFileName(entry.Files[entry.TrackIndex]) : null;
        var oldDurations = entry.Files.Zip(entry.TrackDurations).ToDictionary(x => Path.GetFileName(x.First), x => x.Second, StringComparer.OrdinalIgnoreCase);
        entry.Files = files;
        entry.Title = isSet ? BookNaming.SetTitle(files.Select(Path.GetFileName).ToList()!) : Path.GetFileNameWithoutExtension(files[0]);
        entry.Size = files.Sum(BookEntry.SizeOf);
        entry.Opened = DateTimeOffset.Now;
        entry.TrackDurations = isSet ? files.Select(f => oldDurations.GetValueOrDefault(Path.GetFileName(f))).ToList() : [];
        int idx = currentName is null ? 0 : files.FindIndex(f => string.Equals(Path.GetFileName(f), currentName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) { idx = 0; entry.Position = 0; }
        entry.TrackIndex = idx;
        _store.Add(entry);
        _store.Save();

        _book = entry;
        _chapters = [];
        _shownChapter = -2;
        Title = entry.Title + " – Audiobook Player";

        BookTitle.Text = entry.Title;
        PlayerSection.Visibility = Visibility.Visible;
        SeekLabel.Content = isSet ? "Position in this chapter" : "Position in book";
        ChapterNow.Visibility = Visibility.Collapsed;
        QualityNow.Visibility = Visibility.Collapsed;
        ChangeOrderBtn.Visibility = isSet ? Visibility.Visible : Visibility.Collapsed;
        RenderChapters();
        RenderBookmarks();
        RenderLibrary();

        if (!isSet && IsMp4(files[0])) _chapters = Mp4Chapters.Read(files[0]);
        RenderChapters();

        if (!LoadTrack(idx, entry.Position, autoplay, opening: true)) return;
        if (isSet) ProbeDurations(entry);
    }

    /// <summary>Opens one file of the current book. Returns false if it couldn't be played.</summary>
    private bool LoadTrack(int index, double start, bool play, bool opening = false)
    {
        var book = _book!;
        if (index < 0 || index >= book.Files.Count) return false;
        if (!opening) SavePosition();
        var path = book.Files[index];
        try
        {
            _engine.Open(path);
        }
        catch (Exception ex)
        {
            UpdatePlayState();
            _say.Say($"{Path.GetFileName(path)} couldn't be played. {ex.Message}");
            return false;
        }
        book.TrackIndex = index;
        _engine.Speed = Speeds[Math.Max(0, SpeedBox.SelectedIndex)].Value;

        var d = _engine.Duration.TotalSeconds;
        if (book.IsSet && d > 0) book.TrackDurations[index] = d;
        else if (!book.IsSet) book.Duration = d;
        if (book.IsSet && book.TrackDurations.All(x => x > 0)) book.Duration = book.TrackDurations.Sum();

        _updatingUi = true;
        Seek.Maximum = Math.Floor(d);
        _updatingUi = false;
        ClockTotal.Text = TimeText.Clock(d);
        if (d > 0 && _engine.FileSize > 0)
        {
            var kbps = (int)Math.Round(_engine.FileSize * 8 / d / 1000);
            var grade = kbps >= 128 ? "high quality" : kbps >= 64 ? "good quality" : "standard spoken-word quality";
            QualityNow.Text = $"Audio file quality: about {kbps} kbps, {grade}";
            QualityNow.Visibility = Visibility.Visible;
        }

        var msg = "";
        if (opening) msg += $"Opened {book.Title}. " + (book.IsSet ? $"{book.Files.Count} chapters. " : $"Length {TimeText.Spoken(d)}. ");
        if (book.IsSet) msg += $"Chapter {index + 1}: {TrackTitle(index)}. Length {TimeText.Spoken(d)}. ";
        if (start > 5 && start < d - 5)
        {
            _engine.Position = TimeSpan.FromSeconds(start);
            msg += $"Resuming at {TimeText.Spoken(start)}. ";
        }
        if (!book.IsSet && _chapters.Count > 0) msg += $"{_chapters.Count} chapters. ";
        book.Position = _engine.Position.TotalSeconds;
        _store.Save();

        _shownChapter = -2;
        UpdatePosition(force: true);
        RenderChapters();
        _media?.SetNowPlaying(book.IsSet ? TrackTitle(index) : book.Title, book.IsSet ? book.Title : "Audiobook");
        if (opening) PlayBtn.Focus();

        if (play)
        {
            _engine.Play();
            UpdatePlayState();
            _say.Say(msg);
        }
        else
        {
            UpdatePlayState();
            _say.Say(msg + (opening ? "Press Play to start." : "Press Play to continue."));
        }
        return true;
    }

    private static bool IsMp4(string path) => Path.GetExtension(path).ToLowerInvariant() is ".m4b" or ".m4a" or ".mp4" or ".aac";

    private string TrackTitle(int i) => BookNaming.TrackTitle(Path.GetFileName(_book!.Files[i]), _book.Title);

    /// <summary>Reads how long each file of a set is, in the background, so the whole book's length and progress can be shown.</summary>
    private async void ProbeDurations(BookEntry book)
    {
        var cts = _probe = new CancellationTokenSource();
        bool changed = false;
        for (int i = 0; i < book.Files.Count; i++)
        {
            if (cts.IsCancellationRequested) return;
            if (book.TrackDurations[i] > 0) continue;
            var path = book.Files[i];
            double d = await Task.Run(() =>
            {
                try { return AudioEngine.ReadDuration(path).TotalSeconds; } catch (Exception) { return 0; }
            });
            if (cts.IsCancellationRequested || _book != book) return;
            if (d > 0) { book.TrackDurations[i] = d; changed = true; }
        }
        if (!changed) return;
        if (book.TrackDurations.All(x => x > 0)) book.Duration = book.TrackDurations.Sum();
        _store.Save();
        RenderChapters();
    }

    // ---------------------------------------------------------------- resuming from the library

    private void Resume(BookEntry entry)
    {
        if (_book == entry && _engine.IsOpen)
        {
            if (!_engine.IsPlaying) TogglePlay();
            PlayBtn.Focus();
            return;
        }
        var missing = entry.MissingFiles();
        if (missing.Count > 0 && !Relocate(entry, missing)) return;
        OpenBook(entry.Files.ToList(), autoplay: true);
    }

    /// <summary>When a book's files have been moved or renamed, asks where they are now.</summary>
    private bool Relocate(BookEntry entry, List<string> missing)
    {
        var what = entry.IsSet ? $"{missing.Count} of the {entry.Files.Count} files for {entry.Title}" : "the file for " + entry.Title;
        _say.Say($"Can't find {what}. Choose where it is now.");
        var answer = MessageBox.Show(this,
            $"Can't find {what}. It may have been moved or deleted.\n\nDo you want to show where it is now? Your place and bookmarks will be kept.",
            "Audiobook Player", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return false;

        if (!entry.IsSet)
        {
            var name = Path.GetFileName(entry.Files[0]);
            var dlg = new OpenFileDialog { Title = "Find " + name, FileName = name, Filter = "Audio files|" + string.Join(';', BookNaming.AudioExtensions.Select(x => "*" + x)) };
            if (dlg.ShowDialog(this) != true) return false;
            entry.Files = [dlg.FileName];
            var newKey = BookEntry.KeyFor(entry.Files);
            if (newKey != entry.Key) { _store.Remove(newKey); entry.Key = newKey; }
            _store.Save();
            return true;
        }

        var folder = new OpenFolderDialog { Title = "Choose the folder that now holds " + entry.Title };
        if (folder.ShowDialog(this) != true) return false;
        Dictionary<string, string> found;
        try
        {
            found = Directory.EnumerateFiles(folder.FolderName, "*", SearchOption.AllDirectories)
                .Where(BookNaming.IsAudioFile)
                .GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _say.Say("That folder couldn't be read: " + ex.Message);
            return false;
        }
        var relocated = entry.Files.Select(f => File.Exists(f) ? f : found.GetValueOrDefault(Path.GetFileName(f))).ToList();
        var stillMissing = relocated.Count(f => f is null);
        if (stillMissing > 0)
        {
            _say.Say($"{stillMissing} of the files weren't in that folder, so the book can't be opened.");
            return false;
        }
        entry.Files = relocated!;
        _store.Save();
        return true;
    }

    private void ResumeLatest()
    {
        var e = _store.Recent().FirstOrDefault(x => x != _book);
        if (e is not null) Resume(e); else _say.Say("No other recent book to resume.");
    }

    private void OpenSelectedRecent()
    {
        if (RecentList.SelectedItem is Row { Tag: BookEntry e }) Resume(e);
        else _say.Say("Select a book in the list first.");
    }

    private void RemoveSelectedRecent()
    {
        if (RecentList.SelectedItem is not Row { Tag: BookEntry e }) { _say.Say("Select a book in the list first."); return; }
        int i = RecentList.SelectedIndex;
        _store.Remove(e.Key);
        _store.Save();
        RenderLibrary();
        _say.Say($"Removed {e.Title} from recent books.");
        if (_recentRows.Count > 0)
        {
            RecentList.SelectedIndex = Math.Min(i, _recentRows.Count - 1);
            FocusSelected(RecentList);
        }
        else OpenBtn.Focus();
    }

    // ---------------------------------------------------------------- library display

    private static string EntryMeta(BookEntry e)
    {
        var parts = new List<string>();
        if (e.IsStarted)
        {
            if (e.IsSet) parts.Add($"Chapter {e.TrackIndex + 1} of {e.Files.Count}" + (e.Position > 5 ? $", stopped at {TimeText.Clock(e.Position)}" : ""));
            else parts.Add("Stopped at " + TimeText.Clock(e.Position) + (e.Duration > 0 ? " of " + TimeText.Clock(e.Duration) : ""));
            if (e.ProgressFraction is { } f) parts.Add((int)Math.Round(f * 100) + "% done");
        }
        else parts.Add(e.IsSet ? $"{e.Files.Count} chapters, not started" : "Not started");
        if (e.Opened > DateTimeOffset.MinValue) parts.Add("Last listened " + TimeText.Ago(e.Opened, DateTimeOffset.Now));
        return string.Join(" · ", parts);
    }

    private static string EntryHint(BookEntry e)
    {
        var missing = e.MissingFiles().Count;
        if (missing == 0) return "";
        return e.IsSet ? $"{missing} of its files have moved. You'll be asked where they are." : "The file has moved. You'll be asked where it is.";
    }

    private static string ResumeLabel(BookEntry e) =>
        !e.IsStarted ? "Start listening" :
        e.IsSet ? $"Resume chapter {e.TrackIndex + 1} at {TimeText.Clock(e.Position)}" : "Resume at " + TimeText.Clock(e.Position);

    private static string ResumeSpoken(BookEntry e) =>
        !e.IsStarted ? "Start " + e.Title :
        e.IsSet ? $"Resume {e.Title} at chapter {e.TrackIndex + 1}, {TimeText.Spoken(e.Position)}" : $"Resume {e.Title} at {TimeText.Spoken(e.Position)}";

    private void RenderLibrary()
    {
        var list = _store.Recent();
        var cont = _book is null ? list.FirstOrDefault() : null;

        ContinueSection.Visibility = cont is null ? Visibility.Collapsed : Visibility.Visible;
        if (cont is not null)
        {
            ContTitle.Text = cont.Title;
            ContMeta.Text = EntryMeta(cont);
            var hint = EntryHint(cont);
            ContHint.Text = hint;
            ContHint.Visibility = hint.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            var f = cont.ProgressFraction;
            ContProgress.Visibility = f is not null && cont.IsStarted ? Visibility.Visible : Visibility.Collapsed;
            ContProgress.Value = (f ?? 0) * 100;
            ContBtn.Content = ResumeLabel(cont);
            AutomationProperties.SetName(ContBtn, ResumeSpoken(cont) + (hint.Length > 0 ? ". " + hint : ""));
            ContBtn.Tag = cont;
        }

        var selectedKey = (RecentList.SelectedItem as Row)?.Tag is BookEntry sel ? sel.Key : null;
        _recentRows.Clear();
        foreach (var e in list.Where(e => e != cont && e != _book))
        {
            var meta = EntryMeta(e);
            var hint = EntryHint(e);
            var detail = hint.Length > 0 ? meta + "\n" + hint : meta;
            _recentRows.Add(new Row(e.Title, detail, $"{e.Title}. {meta}" + (hint.Length > 0 ? ". " + hint : ""), e));
        }
        RecentSection.Visibility = _recentRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentList.SelectedIndex = Math.Max(0, _recentRows.ToList().FindIndex(r => ((BookEntry)r.Tag!).Key == selectedKey));
    }

    // ---------------------------------------------------------------- checking the order of a set

    private void ShowOrder(List<string> files)
    {
        SetOrder(files, 0);
        OrderSection.Visibility = Visibility.Visible;
        OrderSection.BringIntoView();
        FocusSelected(OrderList);
        _say.Say($"{files.Count} files. Check that they are in the right order, then choose Start listening.");
    }

    private void HideOrder()
    {
        HideMoveTo(announce: false);
        OrderSection.Visibility = Visibility.Collapsed;
    }

    private void SetOrder(List<string> files, int select)
    {
        _orderFiles = files;
        _orderRows.Clear();
        for (int i = 0; i < files.Count; i++)
        {
            var name = Path.GetFileName(files[i]);
            var folder = Path.GetFileName(Path.GetDirectoryName(files[i])) ?? "";
            _orderRows.Add(new Row($"{i + 1}. {name}", "In folder " + folder, $"{name}, {i + 1} of {files.Count}"));
        }
        OrderIntro.Text = $"{files.Count} files will play in this order. They are sorted by name, with numbers read as numbers. " +
                          "Select a file, then use Move up, Move down or Move to position. Control plus Up or Down arrow also moves it. Your order is remembered for this book.";
        OrderList.SelectedIndex = Math.Clamp(select, 0, files.Count - 1);
        OrderList.ScrollIntoView(OrderList.SelectedItem);
    }

    private void MoveSelected(int delta) => MoveTo(OrderList.SelectedIndex, OrderList.SelectedIndex + delta);

    private void MoveTo(int from, int to)
    {
        if (from < 0) { _say.Say("Select a file in the list first."); return; }
        if (to < 0 || to >= _orderFiles.Count || to == from)
        {
            _say.Say(to < 0 ? "Already first." : to >= _orderFiles.Count ? "Already last." : "");
            return;
        }
        var f = _orderFiles[from];
        _orderFiles.RemoveAt(from);
        _orderFiles.Insert(to, f);
        SetOrder(_orderFiles, to);
        FocusSelected(OrderList);
        _say.Say($"Moved {Path.GetFileName(f)} to position {to + 1} of {_orderFiles.Count}.");
    }

    private void ShowMoveTo()
    {
        if (OrderList.SelectedIndex < 0) { _say.Say("Select a file in the list first."); return; }
        MoveToLabel.Content = $"New position, 1 to {_orderFiles.Count}";
        MoveToBox.Text = (OrderList.SelectedIndex + 1).ToString();
        MoveToPanel.Visibility = Visibility.Visible;
        MoveToBox.Focus();
        MoveToBox.SelectAll();
    }

    private void HideMoveTo(bool announce)
    {
        if (MoveToPanel.Visibility != Visibility.Visible) return;
        MoveToPanel.Visibility = Visibility.Collapsed;
        if (!announce) return;
        FocusSelected(OrderList);
        _say.Say("Move cancelled.");
    }

    private void ApplyMoveTo()
    {
        if (!int.TryParse(MoveToBox.Text.Trim(), out var v) || v < 1 || v > _orderFiles.Count)
        {
            _say.Say($"Enter a number from 1 to {_orderFiles.Count}.");
            MoveToBox.Focus();
            return;
        }
        MoveToPanel.Visibility = Visibility.Collapsed;
        MoveTo(OrderList.SelectedIndex, v - 1);
    }

    // ---------------------------------------------------------------- playback

    private void TogglePlay()
    {
        if (!_engine.IsOpen)
        {
            if (_book is null) PickFiles();
            return;
        }
        if (_engine.IsPlaying)
        {
            _engine.Pause();
            SavePosition();
            UpdatePlayState();
            _say.Say("Paused at " + TimeText.Spoken(_engine.Position));
        }
        else
        {
            _engine.Play();
            UpdatePlayState();
            _say.Say("Playing");
        }
    }

    private void UpdatePlayState()
    {
        bool playing = _engine.IsPlaying;
        PlayBtn.Content = playing ? "Pause" : "Play";
        _media?.SetPlaying(playing);
        if (playing) _tick.Start(); else _tick.Stop();
        UpdatePosition(force: true);
    }

    private void OnTrackEnded()
    {
        var book = _book;
        if (book is null) return;
        if (book.IsSet && book.TrackIndex < book.Files.Count - 1)
        {
            book.Position = 0;
            LoadTrack(book.TrackIndex + 1, 0, play: true);
            return;
        }
        SavePosition();
        UpdatePlayState();
        _say.Say("End of book.");
    }

    private void Skip(int direction)
    {
        if (!_engine.IsOpen) return;
        int amount = Skips[Math.Max(0, SkipBox.SelectedIndex)].Seconds;
        var t = Math.Clamp(_engine.Position.TotalSeconds + direction * amount, 0, _engine.Duration.TotalSeconds);
        _engine.Position = TimeSpan.FromSeconds(t);
        UpdatePosition(force: true);
        _say.Say((direction < 0 ? "Back " : "Forward ") + TimeText.Spoken(amount) + ". Now at " + TimeText.Spoken(t));
    }

    private void GoTo(double seconds, string label)
    {
        _engine.Position = TimeSpan.FromSeconds(seconds);
        UpdatePosition(force: true);
        SavePosition();
        _say.Say($"{label}. Now at {TimeText.Spoken(seconds)}." + (_engine.IsPlaying ? "" : " Press Play to continue."));
    }

    private void WhereAmI()
    {
        if (_book is null || !_engine.IsOpen) { _say.Say("No book open."); return; }
        double rate = _engine.Speed, t = _engine.Position.TotalSeconds, d = _engine.Duration.TotalSeconds;
        string msg;
        if (_book.IsSet)
        {
            msg = $"Chapter {_book.TrackIndex + 1} of {_book.Files.Count}: {TrackTitle(_book.TrackIndex)}. {TimeText.Spoken(t)} of {TimeText.Spoken(d)} in this chapter.";
            _book.Position = t;
            if (_book.Overall is { } overall && _book.Duration > 0)
                msg += $" About {TimeText.SpokenRoughly((_book.Duration - overall) / rate)} left in the book at this speed.";
        }
        else
        {
            msg = $"{TimeText.Spoken(t)} of {TimeText.Spoken(d)}.";
            int ci = ChapterAt(t);
            if (ci >= 0) msg += $" Chapter {ci + 1} of {_chapters.Count}: {_chapters[ci].Title}.";
            msg += $" About {TimeText.SpokenRoughly((d - t) / rate)} left at this speed.";
        }
        msg += _engine.IsPlaying ? " Playing." : " Paused.";
        _say.Say(msg);
    }

    private void SetVolume(int percent, bool speak)
    {
        percent = Math.Clamp(percent, 0, 300);
        _engine.VolumePercent = percent;
        var text = percent + " percent" + (percent > 100 ? ", boosted" : "");
        _updatingUi = true;
        Volume.Value = percent;
        _updatingUi = false;
        Volume.ValueText = text;
        _store.Settings.Volume = percent;
        _store.Save();
        if (speak) _say.Say("Volume " + text);
    }

    // ---------------------------------------------------------------- position

    private void OnTick()
    {
        UpdatePosition(force: false);
        if (DateTime.UtcNow - _lastSave > TimeSpan.FromSeconds(5)) SavePosition();
    }

    private void UpdatePosition(bool force)
    {
        var t = _engine.Position.TotalSeconds;
        ClockNow.Text = TimeText.Clock(t);
        // Don't move the slider under someone who is dragging it.
        if (force || !Seek.IsMouseCaptureWithin)
        {
            _updatingUi = true;
            Seek.Value = Math.Min(Math.Floor(t), Seek.Maximum);
            _updatingUi = false;
        }
        Seek.ValueText = TimeText.Spoken(t) + " of " + TimeText.Spoken(_engine.Duration);
        UpdateChapter(force);
    }

    private void ShowPosition(double seconds)
    {
        ClockNow.Text = TimeText.Clock(seconds);
        Seek.ValueText = TimeText.Spoken(seconds) + " of " + TimeText.Spoken(_engine.Duration);
        UpdateChapter(false);
    }

    private void SavePosition()
    {
        _lastSave = DateTime.UtcNow;
        if (_book is null || !_engine.IsOpen) return;
        _book.Position = _engine.Position.TotalSeconds;
        _book.Opened = DateTimeOffset.Now;
        _store.Save();
    }

    // ---------------------------------------------------------------- chapters

    private bool HasChapters => _book is not null && (_book.IsSet || _chapters.Count > 0);
    private int ChapterCount => _book is null ? 0 : _book.IsSet ? _book.Files.Count : _chapters.Count;
    private int CurrentChapter => _book is null ? -1 : _book.IsSet ? _book.TrackIndex : ChapterAt(_engine.Position.TotalSeconds);
    private string ChapterName(int i) => _book!.IsSet ? TrackTitle(i) : _chapters[i].Title;

    private int ChapterAt(double t)
    {
        int idx = -1;
        for (int i = 0; i < _chapters.Count; i++)
        {
            if (_chapters[i].Start <= t + 0.25) idx = i;
            else break;
        }
        return idx;
    }

    private void RenderChapters()
    {
        _chapterRows.Clear();
        var book = _book;
        if (book is not null && book.IsSet)
        {
            for (int i = 0; i < book.Files.Count; i++)
            {
                var d = book.TrackDurations.ElementAtOrDefault(i);
                var title = TrackTitle(i);
                _chapterRows.Add(new Row($"{i + 1}. {title}", d > 0 ? "Length " + TimeText.Clock(d) : "Length not read yet",
                    $"Chapter {i + 1}: {title}" + (d > 0 ? ", length " + TimeText.Spoken(d) : ""), i));
            }
        }
        else
        {
            for (int i = 0; i < _chapters.Count; i++)
            {
                var c = _chapters[i];
                _chapterRows.Add(new Row($"{i + 1}. {c.Title}", "Starts at " + TimeText.Clock(c.Start),
                    $"Chapter {i + 1}: {c.Title}, starts at {TimeText.Spoken(c.Start)}", i));
            }
        }
        bool has = _chapterRows.Count > 0;
        ChSection.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        PrevChBtn.Visibility = NextChBtn.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _shownChapter = -2;
        UpdateChapter(force: true);
    }

    private void UpdateChapter(bool force)
    {
        if (ChapterCount == 0) return;
        int ci = CurrentChapter;
        if (ci == _shownChapter && !force) return;
        _shownChapter = ci;
        if (ci >= 0)
        {
            ChapterNow.Text = $"Chapter {ci + 1} of {ChapterCount}: {ChapterName(ci)}";
            ChapterNow.Visibility = Visibility.Visible;
        }
        for (int i = 0; i < _chapterRows.Count; i++) _chapterRows[i].IsCurrent = i == ci;
        // Keep the list on the playing chapter, unless you're moving through it yourself.
        if (ci >= 0 && ci < _chapterRows.Count && !ChList.IsKeyboardFocusWithin)
        {
            ChList.SelectedIndex = ci;
            ChList.ScrollIntoView(ChList.SelectedItem);
        }
    }

    private void PlaySelectedChapter()
    {
        if (ChList.SelectedItem is Row { Tag: int i }) JumpChapter(i);
        else _say.Say("Select a chapter in the list first.");
    }

    private void JumpChapter(int i)
    {
        if (_book is null) return;
        if (_book.IsSet)
        {
            if (i == _book.TrackIndex) GoTo(0, $"Chapter {i + 1}: {TrackTitle(i)}");
            else LoadTrack(i, 0, _engine.IsPlaying);
        }
        else GoTo(_chapters[i].Start, $"Chapter {i + 1}: {_chapters[i].Title}");
    }

    private void PreviousChapter()
    {
        if (!HasChapters || !_engine.IsOpen) return;
        var t = _engine.Position.TotalSeconds;
        if (_book!.IsSet)
        {
            if (t > 3 || _book.TrackIndex == 0) GoTo(0, $"Start of chapter {_book.TrackIndex + 1}");
            else JumpChapter(_book.TrackIndex - 1);
            return;
        }
        int ci = ChapterAt(t);
        if (ci >= 0 && t - _chapters[ci].Start < 3) ci--;
        JumpChapter(Math.Max(0, ci));
    }

    private void NextChapter()
    {
        if (!HasChapters || !_engine.IsOpen) return;
        int next = (_book!.IsSet ? _book.TrackIndex : ChapterAt(_engine.Position.TotalSeconds)) + 1;
        if (next >= ChapterCount) { _say.Say("This is the last chapter."); return; }
        JumpChapter(next);
    }

    // ---------------------------------------------------------------- bookmarks

    private void AddBookmark()
    {
        if (_book is null || !_engine.IsOpen) { _say.Say("Open a book first."); return; }
        var t = _engine.Position.TotalSeconds;
        int ci = CurrentChapter;
        var note = BmNote.Text.Trim();
        var b = new Bookmark
        {
            Time = t,
            Note = note,
            Chapter = ci >= 0 ? ChapterName(ci) : "",
            Track = _book.IsSet ? Path.GetFileName(_book.Files[_book.TrackIndex]) : null,
        };
        _book.Bookmarks.Add(b);
        _book.Bookmarks.Sort((x, y) => BookmarkOrder(x).CompareTo(BookmarkOrder(y)));
        _store.Save();
        RenderBookmarks();
        BmNote.Text = "";
        var n = _book.Bookmarks.Count;
        _say.Say($"Bookmark added at {TimeText.Spoken(t)}{(note.Length > 0 ? ": " + note : "")}. You have {n} bookmark{(n == 1 ? "" : "s")}.");
    }

    private double BookmarkOrder(Bookmark b)
    {
        int track = _book!.IsSet && b.Track is not null ? Math.Max(0, _book.Files.FindIndex(f => Path.GetFileName(f) == b.Track)) : 0;
        return track * 1e7 + b.Time;
    }

    private void RenderBookmarks()
    {
        _bookmarkRows.Clear();
        foreach (var b in _book?.Bookmarks ?? [])
        {
            var inChapter = b.Note.Length > 0 && b.Chapter.Length > 0;
            _bookmarkRows.Add(new Row(b.Name,
                "At " + TimeText.Clock(b.Time) + (inChapter ? " in " + b.Chapter : ""),
                $"{b.Name}, at {TimeText.Spoken(b.Time)}" + (inChapter ? ", in " + b.Chapter : ""), b));
        }
        bool any = _bookmarkRows.Count > 0;
        BmEmpty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        BmList.Visibility = BmButtons.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (any && BmList.SelectedIndex < 0) BmList.SelectedIndex = 0;
    }

    private void GoToSelectedBookmark()
    {
        if (BmList.SelectedItem is not Row { Tag: Bookmark b } || _book is null) { _say.Say("Select a bookmark first."); return; }
        if (_book.IsSet && b.Track is not null)
        {
            int j = _book.Files.FindIndex(f => Path.GetFileName(f) == b.Track);
            if (j >= 0 && j != _book.TrackIndex) { LoadTrack(j, b.Time, _engine.IsPlaying); return; }
        }
        GoTo(b.Time, "Jumped to bookmark " + b.Name);
    }

    private void DeleteSelectedBookmark()
    {
        if (BmList.SelectedItem is not Row { Tag: Bookmark b } || _book is null) { _say.Say("Select a bookmark first."); return; }
        int i = BmList.SelectedIndex;
        _book.Bookmarks.Remove(b);
        _store.Save();
        RenderBookmarks();
        _say.Say($"Deleted bookmark {b.Name}. {_book.Bookmarks.Count} left.");
        if (_bookmarkRows.Count > 0)
        {
            BmList.SelectedIndex = Math.Min(i, _bookmarkRows.Count - 1);
            FocusSelected(BmList);
        }
        else AddBmBtn.Focus();
    }

    // ---------------------------------------------------------------- helpers

    private static void FocusSelected(ListBox list)
    {
        list.UpdateLayout();
        if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
        if (list.SelectedItem is null) { list.Focus(); return; }
        list.ScrollIntoView(list.SelectedItem);
        if (list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is ListBoxItem item) item.Focus();
        else list.Focus();
    }
}
