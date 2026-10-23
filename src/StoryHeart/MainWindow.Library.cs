using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using StoryHeart.Audio;
using StoryHeart.Core;
using StoryHeart.Dialogs;
using Microsoft.Win32;

namespace StoryHeart;

/// <summary>The library: adding, listing, searching and removing books.</summary>
public partial class MainWindow
{
    private static readonly (string Key, string Label)[] SortOptions =
        [("recent", "Recently played"), ("title", "Title"), ("author", "Author"), ("added", "Recently added")];

    private readonly CancellationTokenSource _probeStop = new();
    private bool _probing;

    private static string AudioFilter =>
        "Audio files|" + string.Join(';', BookNaming.AudioExtensions.Select(x => "*" + x)) + "|All files|*.*";

    private void WireLibrary()
    {
        AddBooksBtn.Click += (_, _) => AddBooks();
        AddFolderBtn.Click += (_, _) => AddFolder();
        ContBtn.Click += (_, _) =>
        {
            if (ContBtn.Tag is BookEntry b && b == _book) ShowPlayer(focus: true);
            else if (ContBtn.Tag is BookEntry other) Play(other);
        };

        SearchBox.TextChanged += (_, _) => RenderLibrary();
        SortBox.SelectionChanged += (_, _) => RenderLibrary();
        ActivateOnEnter(BookList, () => Play(SelectedBook()));
        BookList.KeyDown += (_, e) => { if (e.Key == Key.Delete) { e.Handled = true; RemoveBook(SelectedBook()); } };
        BookList.SelectionChanged += (_, _) => UpdateBookButtons();
        BookPlayBtn.Click += (_, _) => Play(SelectedBook());
        BookSettingsBtn2.Click += (_, _) => OpenBookSettings(SelectedBook());
        BookRemoveBtn.Click += (_, _) => RemoveBook(SelectedBook());

        BookMenu.Opened += (_, _) =>
        {
            var b = SelectedBook();
            foreach (var item in BookMenu.Items.OfType<System.Windows.Controls.MenuItem>()) item.IsEnabled = b is not null;
            CtxFinished.Visibility = b is { Finished: false } ? Visibility.Visible : Visibility.Collapsed;
            CtxRestart.Visibility = b is not null && (b.IsStarted || b.Finished) ? Visibility.Visible : Visibility.Collapsed;
        };
        CtxPlay.Click += (_, _) => Play(SelectedBook());
        CtxSettings.Click += (_, _) => OpenBookSettings(SelectedBook());
        CtxFinished.Click += (_, _) => MarkBook(SelectedBook(), finished: true);
        CtxRestart.Click += (_, _) => MarkBook(SelectedBook(), finished: false);
        CtxExplorer.Click += (_, _) => { if (SelectedBook() is { } b) BookSettingsDialog.ShowInExplorer(b.Files[0]); };
        CtxRemove.Click += (_, _) => RemoveBook(SelectedBook());
    }

    private BookEntry? SelectedBook() => (BookList.SelectedItem as Row)?.Tag as BookEntry;

    // ---------------------------------------------------------------- adding books

    private void AddBooks()
    {
        var dlg = new OpenFileDialog { Title = "Add books", Multiselect = true, Filter = AudioFilter };
        if (dlg.ShowDialog(this) != true) return;
        var files = dlg.FileNames.Where(BookNaming.IsAudioFile).ToList();
        if (files.Count == 0) { _say.Say("Those aren't audio files this app can play."); return; }

        List<List<string>> books;
        if (files.Count == 1) books = [files];
        else
        {
            var choice = Dialogs.Dialogs.Ask(this, "Add books",
                $"You chose {files.Count} files. Are they parts of one book that play in order, or {files.Count} separate books?",
                "One book", $"{files.Count} separate books", "Cancel");
            if (choice < 0 || choice == 2) return;
            books = choice == 0 ? [NaturalComparer.Sort(files)] : files.Select(f => new List<string> { f }).ToList();
        }
        AddToLibrary(books);
    }

    private async void AddFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Choose a folder of audiobooks", Multiselect = true };
        if (dlg.ShowDialog(this) != true) return;
        _say.Say("Looking for books…");
        var folders = dlg.FolderNames;
        var books = await Task.Run(() => folders.SelectMany(LibraryScanner.FindBooks).ToList());
        if (books.Count == 0) { _say.Say("No audio files were found in that folder."); return; }
        AddToLibrary(books);
    }

    /// <summary>Adds books to the library without playing them. Returns the entries, new or already there.</summary>
    private List<BookEntry> AddToLibrary(List<List<string>> books, bool announce = true)
    {
        var entries = new List<BookEntry>();
        var newOnes = new List<BookEntry>();
        foreach (var files in books)
        {
            var key = BookEntry.KeyFor(files);
            if (_store.Find(key) is { } existing)
            {
                // Already in the library: just make sure it points at these files.
                if (existing.MissingFiles().Count > 0 && existing.Files.Count == files.Count) existing.Files = files;
                entries.Add(existing);
                continue;
            }
            var entry = _store.Add(BookEntry.Create(files));
            entries.Add(entry);
            newOnes.Add(entry);
        }
        // A few books get their titles straight away, so they're announced by name; a big folder is read in the background.
        if (newOnes.Count <= 5)
            foreach (var entry in newOnes) entry.ApplyDetails(TagReader.ReadBook(entry.Files));
        _store.Save();
        int added = newOnes.Count;
        if (added > 1) SortBox.SelectedIndex = Array.FindIndex(SortOptions, o => o.Key == "added");
        SearchBox.Text = "";
        RenderLibrary(select: entries.FirstOrDefault());
        if (announce)
        {
            int already = entries.Count - added;
            var msg = added switch
            {
                0 => entries.Count == 1 ? $"{entries[0].Title} is already in your library." : "Those books are already in your library.",
                1 => $"Added {newOnes[0].Title} to your library.",
                _ => $"Added {added} books to your library.",
            };
            if (added > 0 && already > 0) msg += $" {already} were already there.";
            _say.Say(msg);
            if (!PlayerShown) FocusSelected(BookList);
        }
        ProbeLibrary();
        return entries;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            Activate();
            OpenPaths(paths, play: false);
        }
    }

    /// <summary>
    /// Adds dropped or "Open with" files and folders to the library. A single book is opened straight away;
    /// several books are added and shown in the library.
    /// </summary>
    private async void OpenPaths(IEnumerable<string> paths, bool play)
    {
        var list = paths.ToList();
        var books = await Task.Run(() =>
        {
            var found = new List<List<string>>();
            var loose = new List<string>();
            foreach (var p in list)
            {
                if (Directory.Exists(p)) found.AddRange(LibraryScanner.FindBooks(p));
                else if (File.Exists(p) && BookNaming.IsAudioFile(p)) loose.Add(Path.GetFullPath(p));
            }
            // Files opened together from one folder are one book; a single file is its own book.
            found.AddRange(LibraryScanner.GroupIntoBooks(loose));
            return found;
        });
        if (books.Count == 0) { _say.Say("No audio files were found."); return; }
        var entries = AddToLibrary(books, announce: books.Count > 1);
        if (entries.Count == 1) OpenEntry(entries[0], autoplay: play);
        else ShowLibrary(focus: true);
    }

    // ---------------------------------------------------------------- library actions

    private void Play(BookEntry? entry)
    {
        if (entry is null) { _say.Say("Select a book in the list first."); return; }
        if (entry == _book && _engine.IsOpen)
        {
            ShowPlayer(focus: true);
            if (!_engine.IsPlaying) TogglePlay();
            return;
        }
        var missing = entry.MissingFiles();
        if (missing.Count > 0 && !Relocate(entry, missing)) return;
        OpenEntry(entry, autoplay: true);
    }

    private void ResumeLatest()
    {
        var e = _store.Recent().FirstOrDefault(x => x != _book && x.EverPlayed);
        if (e is not null) Play(e); else _say.Say("No other book to resume.");
    }

    private void RemoveBook(BookEntry? entry)
    {
        if (entry is null) { _say.Say("Select a book in the list first."); return; }
        var answer = Dialogs.Dialogs.Ask(this, "Remove from library",
            $"Remove {entry.Title} from your library? Your place and bookmarks in it will be forgotten. The files on your computer won't be deleted.",
            "Remove", "Cancel");
        if (answer != 0) { FocusSelected(BookList); return; }
        if (entry == _book) CloseBook();
        int i = BookList.SelectedIndex;
        _store.Remove(entry.Key);
        _store.Save();
        RenderLibrary();
        _say.Say($"Removed {entry.Title} from your library.");
        if (_bookRows.Count > 0)
        {
            BookList.SelectedIndex = Math.Min(Math.Max(0, i), _bookRows.Count - 1);
            FocusSelected(BookList);
        }
        else AddBooksBtn.Focus();
    }

    private void MarkBook(BookEntry? entry, bool finished)
    {
        if (entry is null) return;
        if (entry == _book) SavePosition();
        entry.Finished = finished;
        if (!finished)
        {
            entry.TrackIndex = 0;
            entry.Position = 0;
            if (entry == _book) OpenEntry(entry, autoplay: false);
        }
        _store.Save();
        RenderLibrary(select: entry);
        _say.Say(finished ? $"Marked {entry.Title} as finished." : $"{entry.Title} will start from the beginning.");
        FocusSelected(BookList);
    }

    /// <summary>When a book's files have been moved or renamed, asks where they are now.</summary>
    private bool Relocate(BookEntry entry, List<string> missing)
    {
        var what = entry.IsSet ? $"{missing.Count} of the {entry.Files.Count} files for {entry.Title}" : "the file for " + entry.Title;
        var answer = Dialogs.Dialogs.Ask(this, "Can't find the book",
            $"Can't find {what}. It may have been moved, renamed or deleted. Do you want to show where it is now? Your place and bookmarks will be kept.",
            entry.IsSet ? "Choose its folder…" : "Find the file…", "Cancel");
        if (answer != 0) return false;

        if (!entry.IsSet)
        {
            var name = Path.GetFileName(entry.Files[0]);
            var dlg = new OpenFileDialog { Title = "Find " + name, FileName = name, Filter = AudioFilter };
            if (dlg.ShowDialog(this) != true) return false;
            entry.Files = [dlg.FileName];
            var newKey = BookEntry.KeyFor(entry.Files);
            if (newKey != entry.Key)
            {
                _store.Books.RemoveAll(b => b.Key == newKey && b != entry);
                entry.Key = newKey;
            }
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

    // ---------------------------------------------------------------- showing the library

    private static string Status(BookEntry e)
    {
        if (e.MissingFiles().Count > 0) return e.IsSet ? "Some files have moved" : "File has moved";
        if (e.Finished) return "Finished";
        if (!e.IsStarted) return "Not started" + (e.Duration > 0 ? " · " + TimeText.SpokenRoughly(e.Duration) : "");
        var parts = new List<string>();
        if (e.ProgressFraction is { } f) parts.Add((int)Math.Round(f * 100) + "% done");
        if (e.Remaining is { } left) parts.Add(TimeText.SpokenRoughly(left) + " left");
        else if (e.IsSet) parts.Add($"Chapter {e.TrackIndex + 1} of {e.Files.Count}");
        return string.Join(" · ", parts);
    }

    private static string LastListened(BookEntry e) =>
        e.EverPlayed ? "Last listened " + TimeText.Ago(e.Opened, DateTimeOffset.Now) : "Added " + TimeText.Ago(e.Added, DateTimeOffset.Now);

    private void RenderLibrary(BookEntry? select = null)
    {
        bool moveSelection = select is not null;
        select ??= SelectedBook();
        var books = _store.Books.AsEnumerable();
        var query = SearchBox.Text.Trim();
        if (query.Length > 0)
            books = books.Where(b => new[] { b.Title, b.Author, b.Narrator, b.Series }.Any(t => t?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true) ||
                                     b.Files.Any(f => Path.GetFileName(f).Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        books = SortOptions[Math.Max(0, SortBox.SelectedIndex)].Key switch
        {
            "title" => books.OrderBy(b => b.Title, NaturalComparer.Instance),
            // Books without an author go last; a series stays together, in title order.
            "author" => books.OrderBy(b => b.Author is null).ThenBy(b => b.Author, NaturalComparer.Instance)
                             .ThenBy(b => b.Series ?? b.Title, NaturalComparer.Instance).ThenBy(b => b.Title, NaturalComparer.Instance),
            "added" => books.OrderByDescending(b => b.Added),
            _ => books.OrderByDescending(b => b.Opened).ThenByDescending(b => b.Added),
        };

        var rows = books.Select(b =>
        {
            var status = Status(b);
            var when = LastListened(b);
            var playing = b == _book ? (_engine.IsPlaying ? "Playing now · " : "Open · ") : "";
            var by = b.Byline.Length > 0 ? b.Byline + " · " : "";
            var spokenBy = b.Byline.Length > 0 ? ", " + b.Byline : "";
            return new Row(b.Title, by + playing + status + " · " + when, $"{b.Title}{spokenBy}. {playing}{status}. {when}", b);
        }).ToList();
        if (rows.Select(r => r.Tag).SequenceEqual(_bookRows.Select(r => r.Tag)))
        {
            // Same books in the same order: update the text without disturbing focus.
            for (int i = 0; i < rows.Count; i++) _bookRows[i].Update(rows[i].Title, rows[i].Detail, rows[i].Spoken);
            if (moveSelection) BookList.SelectedIndex = rows.FindIndex(r => r.Tag == select);
            UpdateBookButtons();
            RenderContinueCard();
            UpdateLibraryCount(query);
            return;
        }
        _bookRows.Clear();
        foreach (var r in rows) _bookRows.Add(r);
        UpdateLibraryCount(query);

        int index = select is null ? -1 : _bookRows.ToList().FindIndex(r => r.Tag == select);
        BookList.SelectedIndex = index >= 0 ? index : _bookRows.Count > 0 ? 0 : -1;
        UpdateBookButtons();
        RenderContinueCard();
    }

    private void UpdateLibraryCount(string query)
    {
        int total = _store.Books.Count;
        LibraryCount.Text = total == 0 ? "" :
            query.Length > 0 ? $"{_bookRows.Count} of {total} books match \"{query}\"." :
            total == 1 ? "1 book." : $"{total} books.";
        EmptyLibrary.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        BookList.Visibility = total == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateBookButtons()
    {
        BookButtons.Visibility = _bookRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var b = SelectedBook();
        BookPlayBtn.Content = b is null ? "Play" : b == _book ? "Go to book" : b.IsStarted ? "Resume" : "Play";
    }

    private void RenderContinueCard()
    {
        var card = _book ?? _store.LastPlayed;
        ContinueCard.Visibility = card is null ? Visibility.Collapsed : Visibility.Visible;
        if (card is null) return;
        bool open = card == _book;
        ContLabel.Text = open ? (_engine.IsPlaying ? "Playing now" : "Open now") : "Continue listening";
        ContTitle.Text = card.Title;
        ContMeta.Text = (card.Byline.Length > 0 ? card.Byline + " · " : "") + Status(card);
        var f = card.ProgressFraction;
        ContProgress.Visibility = f is not null && (card.IsStarted || card.Finished) ? Visibility.Visible : Visibility.Collapsed;
        ContProgress.Value = (f ?? 0) * 100;
        ContBtn.Content = open ? "Go to book" : card.IsStarted ? "Resume" : "Play";
        AutomationProperties.SetName(ContBtn, (open ? "Go to " : card.IsStarted ? "Resume " : "Play ") + card.Title + ". " + ContMeta.Text);
        ContBtn.Tag = card;
    }

    // ---------------------------------------------------------------- reading lengths in the background

    /// <summary>Reads how long each book is, so the library can show lengths and progress. The open book goes first.</summary>
    private async void ProbeLibrary()
    {
        if (_probing) return;
        _probing = true;
        try
        {
            while (!_probeStop.IsCancellationRequested)
            {
                var book = NeedsReading(_book) ? _book : _store.Books.FirstOrDefault(NeedsReading);
                if (book is null) break;
                var files = book.Files.ToList();
                if (!book.DetailsRead)
                {
                    var details = await Task.Run(() => TagReader.ReadBook(files));
                    if (_probeStop.IsCancellationRequested) return;
                    book.ApplyDetails(details);
                    _store.Save();
                    if (book == _book) ShowBookHeading();
                    if (!PlayerShown) RenderLibrary();
                    if (!NeedsLength(book)) continue;
                }
                var lengths = await Task.Run(() => files.Select(f =>
                {
                    try { return AudioEngine.ReadDuration(f).TotalSeconds; } catch (Exception) { return -1; }
                }).ToList());
                if (_probeStop.IsCancellationRequested) return;
                if (!files.SequenceEqual(book.Files)) continue; // The order changed while reading; try again.

                if (book.IsSet)
                {
                    if (book.TrackDurations.Count != lengths.Count) book.TrackDurations = lengths.Select(_ => 0.0).ToList();
                    for (int i = 0; i < lengths.Count; i++) book.TrackDurations[i] = lengths[i];
                    if (lengths.All(x => x > 0)) book.Duration = lengths.Sum();
                }
                else book.Duration = lengths[0];
                _store.Save();
                if (book == _book) RenderChapters();
                if (!PlayerShown) RenderLibrary();
            }
        }
        finally { _probing = false; }
    }

    private static bool NeedsReading(BookEntry? b) => b is not null && b.MissingFiles().Count == 0 && (!b.DetailsRead || NeedsLength(b));

    /// <summary>True when a book's length hasn't been read yet (-1 marks a file that couldn't be read, so it isn't retried).</summary>
    private static bool NeedsLength(BookEntry? b)
    {
        if (b is null || b.MissingFiles().Count > 0) return false;
        if (b.IsSet) return b.TrackDurations.Count != b.Files.Count || b.TrackDurations.Any(d => d == 0);
        return b.Duration == 0;
    }
}
