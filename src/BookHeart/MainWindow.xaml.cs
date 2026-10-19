using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BookHeart.Audio;
using BookHeart.Core;
using BookHeart.Dialogs;
using BookHeart.Platform;

namespace BookHeart;

/// <summary>
/// The main window: a menu bar, the library, and the book that's playing. The library lives in
/// MainWindow.Library.cs and playback in MainWindow.Player.cs.
/// </summary>
public partial class MainWindow : Window
{
    private readonly LibraryStore _store;
    private readonly AudioEngine _engine = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Announcer _say;
    private MediaControls? _media;
    private readonly string[] _startupFiles;

    private readonly ObservableCollection<Row> _bookRows = [];
    private readonly ObservableCollection<Row> _bookmarkRows = [];
    private readonly ObservableCollection<Row> _chapterRows = [];

    public MainWindow(LibraryStore store, string[] startupFiles)
    {
        InitializeComponent();
        _store = store;
        _startupFiles = startupFiles;
        _say = new Announcer(this);
        _say.Announced += m => StatusText.Text = m;
        Title = AppInfo.Name;

        BookList.ItemsSource = _bookRows;
        BmList.ItemsSource = _bookmarkRows;
        ChList.ItemsSource = _chapterRows;
        foreach (var (_, label) in SortOptions) SortBox.Items.Add(label);

        WireMenu();
        WireLibrary();
        WirePlayer();
        PreviewKeyDown += OnShortcutKey;
        Drop += OnDrop;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };

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

        SortBox.SelectedIndex = 0;
        RenderLibrary();
        UpdatePlayerAvailability();
        ShowLibrary(focus: true);

        if (_startupFiles.Length > 0) OpenPaths(_startupFiles, play: true);
        else if (_store.Settings.OpenLastBookOnStart && _store.LastPlayed is { } last && last.MissingFiles().Count == 0)
            OpenEntry(last, autoplay: false);
        ProbeLibrary();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SavePosition();
        _store.Save();
        _probeStop.Cancel();
        _engine.Dispose();
    }

    /// <summary>Files sent from another copy of the app, e.g. a book double-clicked in File Explorer.</summary>
    public void ReceiveFiles(string[] paths)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        if (paths.Length > 0) OpenPaths(paths, play: true);
    }

    // ---------------------------------------------------------------- views

    private bool PlayerShown => PlayerView.Visibility == Visibility.Visible;

    private void ShowLibrary(bool focus)
    {
        PlayerView.Visibility = Visibility.Collapsed;
        LibraryView.Visibility = Visibility.Visible;
        RenderLibrary();
        if (!focus) return;
        if (_bookRows.Count > 0) FocusSelected(BookList);
        else AddBooksBtn.Focus();
    }

    private void ShowPlayer(bool focus)
    {
        if (_book is null) { _say.Say("No book is open. Choose a book in the library."); return; }
        LibraryView.Visibility = Visibility.Collapsed;
        PlayerView.Visibility = Visibility.Visible;
        if (focus) PlayBtn.Focus();
    }

    private void UpdatePlayerAvailability()
    {
        bool open = _book is not null;
        MenuNowPlaying.IsEnabled = open;
        foreach (var item in PlaybackMenu.Items.OfType<MenuItem>()) item.IsEnabled = open;
    }

    // ---------------------------------------------------------------- menu and keyboard

    private void WireMenu()
    {
        MenuAddBooks.Click += (_, _) => AddBooks();
        MenuAddFolder.Click += (_, _) => AddFolder();
        MenuSettings.Click += (_, _) => OpenSettings();
        MenuExit.Click += (_, _) => Close();

        MenuPlay.Click += (_, _) => TogglePlay();
        MenuBack.Click += (_, _) => Skip(-1);
        MenuForward.Click += (_, _) => Skip(1);
        MenuPrevChapter.Click += (_, _) => PreviousChapter();
        MenuNextChapter.Click += (_, _) => NextChapter();
        MenuFaster.Click += (_, _) => ChangeSpeed(+1);
        MenuSlower.Click += (_, _) => ChangeSpeed(-1);
        MenuVolUp.Click += (_, _) => ChangeVolume(+10);
        MenuVolDown.Click += (_, _) => ChangeVolume(-10);
        MenuBookmark.Click += (_, _) => AddBookmark(askForNote: false);
        MenuBookmarkNote.Click += (_, _) => AddBookmark(askForNote: true);
        MenuWhere.Click += (_, _) => WhereAmI();
        MenuBookSettings.Click += (_, _) => OpenBookSettings(_book);

        MenuLibrary.Click += (_, _) => ShowLibrary(focus: true);
        MenuNowPlaying.Click += (_, _) => ShowPlayer(focus: true);

        MenuManual.Click += (_, _) => OpenManual();
        MenuShortcuts.Click += (_, _) => Dialogs.Dialogs.ShowShortcuts(this);
        MenuDataFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            try { Process.Start("explorer.exe", $"\"{AppInfo.DataDirectory}\""); } catch (Exception) { }
        };
        MenuAbout.Click += (_, _) => Dialogs.Dialogs.ShowAbout(this);
    }

    private void OnShortcutKey(object sender, KeyEventArgs e)
    {
        if (MainMenu.IsKeyboardFocusWithin) return;

        if (_media is { Available: false })
        {
            Action? media = e.Key switch
            {
                Key.MediaPlayPause => TogglePlay,
                Key.MediaNextTrack => () => { if (HasChapters) NextChapter(); else Skip(1); },
                Key.MediaPreviousTrack => () => { if (HasChapters) PreviousChapter(); else Skip(-1); },
                _ => null,
            };
            if (Run(media)) return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        Action? action = mods switch
        {
            ModifierKeys.Alt => key switch
            {
                Key.P => TogglePlay,
                Key.OemComma => () => Skip(-1),
                Key.OemPeriod => () => Skip(1),
                Key.Left => PreviousChapter,
                Key.Right => NextChapter,
                Key.Up => () => ChangeVolume(+10),
                Key.Down => () => ChangeVolume(-10),
                Key.B => () => AddBookmark(askForNote: false),
                Key.I => WhereAmI,
                Key.R => ResumeLatest,
                Key.O => AddBooks,
                _ => null,
            },
            ModifierKeys.Control => key switch
            {
                Key.O => AddBooks,
                Key.B => () => AddBookmark(askForNote: true),
                Key.E => () => OpenBookSettings(_book),
                Key.OemCloseBrackets => () => ChangeSpeed(+1),
                Key.OemOpenBrackets => () => ChangeSpeed(-1),
                Key.D1 or Key.NumPad1 => () => ShowLibrary(focus: true),
                Key.D2 or Key.NumPad2 => () => ShowPlayer(focus: true),
                Key.OemComma => OpenSettings,
                _ => null,
            },
            ModifierKeys.Control | ModifierKeys.Shift => key == Key.O ? AddFolder : null,
            ModifierKeys.None => key switch
            {
                Key.F1 => () => Dialogs.Dialogs.ShowShortcuts(this),
                Key.Escape when PlayerShown && Keyboard.FocusedElement is not TextBox => () => ShowLibrary(focus: true),
                _ => null,
            },
            _ => null,
        };
        Run(action);

        bool Run(Action? a)
        {
            if (a is null) return false;
            e.Handled = true;
            a();
            return true;
        }
    }

    private void OpenManual()
    {
        var manual = Path.Combine(AppContext.BaseDirectory, AppInfo.Name + " Manual.html");
        if (!File.Exists(manual)) { _say.Say("The manual wasn't found next to the program. Try installing BookHeart again."); return; }
        try { Process.Start(new ProcessStartInfo(manual) { UseShellExecute = true }); }
        catch (Exception ex) { _say.Say("The manual couldn't be opened: " + ex.Message); }
    }

    private void OpenSettings()
    {
        if (!SettingsDialog.Show(this, _store.Settings, _store.Books)) return;
        _store.Save();
        Theme.Apply(Application.Current.Resources, _store.Settings.Theme);
        ApplyAudioSettings();
        RenderLibrary();
        _say.Say("Settings saved.");
    }

    private void OpenBookSettings(BookEntry? book)
    {
        if (book is null) { _say.Say("Choose a book first."); return; }
        var info = book == _book ? QualityText() : "";
        var dlg = new BookSettingsDialog(this, book, _store.Settings, info);
        dlg.Changed += () =>
        {
            _store.Save();
            if (book == _book)
            {
                ApplyAudioSettings();
                BookTitle.Text = book.Title;
                Title = book.Title + " – " + AppInfo.Name;
            }
        };
        dlg.OrderChanged += () =>
        {
            _store.Save();
            if (book == _book) OpenEntry(book, autoplay: _engine.IsPlaying);
        };
        dlg.ShowDialog();
        RenderLibrary();
    }

    // ---------------------------------------------------------------- helpers

    private static void ActivateOnEnter(ListBox list, Action action)
    {
        list.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; action(); } };
        list.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(list, d) is ListBoxItem) action();
        };
    }

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
