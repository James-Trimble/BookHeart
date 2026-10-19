using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using BookHeart.Controls;
using BookHeart.Core;
using BookHeart.Platform;

namespace BookHeart;

/// <summary>The book that's playing: playback, position, chapters, bookmarks and its audio settings.</summary>
public partial class MainWindow
{
    private const double SpeedStep = 0.05;

    private BookEntry? _book;
    private IReadOnlyList<Chapter> _chapters = [];
    private int _shownChapter = -2;
    private DateTime _lastSave;
    private bool _updatingUi;

    private void WirePlayer()
    {
        ToLibraryBtn.Click += (_, _) => ShowLibrary(focus: true);
        BookSettingsBtn.Click += (_, _) => OpenBookSettings(_book);
        PlayBtn.Click += (_, _) => TogglePlay();
        BackBtn.Click += (_, _) => Skip(-1);
        FwdBtn.Click += (_, _) => Skip(1);
        PrevChBtn.Click += (_, _) => PreviousChapter();
        NextChBtn.Click += (_, _) => NextChapter();
        WhereBtn.Click += (_, _) => WhereAmI();
        AddBmBtn.Click += (_, _) => AddBookmark(askForNote: false);

        Seek.ValueChanged += (_, e) =>
        {
            if (_updatingUi || !_engine.IsOpen) return;
            _engine.Position = TimeSpan.FromSeconds(e.NewValue);
            ShowPosition(e.NewValue);
        };

        ActivateOnEnter(ChList, PlaySelectedChapter);
        ChGoBtn.Click += (_, _) => PlaySelectedChapter();
        ChangeOrderBtn.Click += (_, _) =>
        {
            if (_book is not { IsSet: true }) return;
            var order = Dialogs.FileOrderDialog.Show(this, _book.Files);
            if (order is null) return;
            _book.Files = order;
            _store.Save();
            OpenEntry(_book, autoplay: _engine.IsPlaying);
        };

        BmAddNoteBtn.Click += (_, _) => AddBookmark(askForNote: true);
        ActivateOnEnter(BmList, GoToSelectedBookmark);
        BmList.KeyDown += (_, e) => { if (e.Key == Key.Delete) { e.Handled = true; DeleteSelectedBookmark(); } };
        BmGoBtn.Click += (_, _) => GoToSelectedBookmark();
        BmEditBtn.Click += (_, _) => EditSelectedBookmark();
        BmDeleteBtn.Click += (_, _) => DeleteSelectedBookmark();
    }

    // ---------------------------------------------------------------- opening a book

    /// <summary>Opens a library book in the player, at the place you stopped.</summary>
    private void OpenEntry(BookEntry entry, bool autoplay)
    {
        if (_book is not null && _book != entry) SavePosition();
        _engine.Close();

        if (entry.Finished)
        {
            entry.Finished = false;
            entry.TrackIndex = 0;
            entry.Position = 0;
        }
        if (entry.IsSet && entry.TrackDurations.Count != entry.Files.Count) entry.TrackDurations = entry.Files.Select(_ => 0.0).ToList();
        if (!entry.TitleEdited && entry.IsSet) entry.Title = BookNaming.SetTitle(entry.Files.Select(Path.GetFileName).ToList()!);
        entry.TrackIndex = Math.Clamp(entry.TrackIndex, 0, entry.Files.Count - 1);
        entry.Opened = DateTimeOffset.Now;
        _store.Save();

        _book = entry;
        _chapters = !entry.IsSet && IsMp4(entry.Files[0]) ? Mp4Chapters.Read(entry.Files[0]) : [];
        _shownChapter = -2;
        Title = entry.Title + " – " + AppInfo.Name;
        BookTitle.Text = entry.Title;
        SeekLabel.Content = entry.IsSet ? "Position in this chapter" : "Position in book";
        ChangeOrderBtn.Visibility = entry.IsSet ? Visibility.Visible : Visibility.Collapsed;
        RenderChapters();
        RenderBookmarks();
        UpdatePlayerAvailability();
        ApplyAudioSettings();
        ShowPlayer(focus: true);

        if (LoadTrack(entry.TrackIndex, entry.Position, autoplay, opening: true)) ProbeLibrary();
        RenderLibrary(select: entry);
    }

    /// <summary>Closes the open book, e.g. when it is removed from the library.</summary>
    private void CloseBook()
    {
        SavePosition();
        _engine.Close();
        _book = null;
        _chapters = [];
        _media?.SetClosed();
        Title = AppInfo.Name;
        UpdatePlayState();
        UpdatePlayerAvailability();
        ShowLibrary(focus: false);
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
        ApplyAudioSettings();

        var d = _engine.Duration.TotalSeconds;
        if (book.IsSet && d > 0) book.TrackDurations[index] = d;
        else if (!book.IsSet) book.Duration = d;
        if (book.IsSet && book.TrackDurations.All(x => x > 0)) book.Duration = book.TrackDurations.Sum();

        _updatingUi = true;
        Seek.Maximum = Math.Floor(d);
        _updatingUi = false;
        ClockTotal.Text = TimeText.Clock(d);

        var msg = "";
        if (opening) msg += $"Opened {book.Title}. " + (book.IsSet ? $"{book.Files.Count} chapters. " : $"Length {TimeText.Spoken(d)}. ");
        if (book.IsSet) msg += $"Chapter {index + 1}: {TrackTitle(index)}. ";
        if (start > 5 && start < d - 5)
        {
            _engine.Position = TimeSpan.FromSeconds(start);
            msg += $"Resuming at {TimeText.Spoken(start)}. ";
        }
        if (!book.IsSet && _chapters.Count > 0 && opening) msg += $"{_chapters.Count} chapters. ";
        book.Position = _engine.Position.TotalSeconds;
        _store.Save();

        _shownChapter = -2;
        UpdatePosition(force: true);
        RenderChapters();
        _media?.SetNowPlaying(book.IsSet ? TrackTitle(index) : book.Title, book.IsSet ? book.Title : AppInfo.Name);

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

    private string QualityText()
    {
        var d = _engine.Duration.TotalSeconds;
        if (d <= 0 || _engine.FileSize <= 0) return "";
        var kbps = (int)Math.Round(_engine.FileSize * 8 / d / 1000);
        var grade = kbps >= 128 ? "high quality" : kbps >= 64 ? "good quality" : "standard spoken-word quality";
        return $"Audio quality: about {kbps} kbps, {grade}.";
    }

    // ---------------------------------------------------------------- playback

    private void TogglePlay()
    {
        if (!_engine.IsOpen)
        {
            if (_book is not null) OpenEntry(_book, autoplay: true);
            else if (_store.LastPlayed is { } last) Play(last);
            else _say.Say("Choose a book in the library first.");
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
        PlayLabel.Text = playing ? "Pause" : "Play";
        PlayIcon.Text = playing ? "" : "";
        AutomationProperties.SetName(PlayBtn, playing ? "Pause" : "Play");
        _media?.SetPlaying(playing);
        if (playing) _tick.Start(); else _tick.Stop();
        UpdatePosition(force: true);
        if (!PlayerShown) RenderContinueCard();
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
        book.Finished = true;
        _store.Save();
        UpdatePlayState();
        _say.Say($"End of {book.Title}. It's marked as finished.");
    }

    private void Skip(int direction)
    {
        if (!_engine.IsOpen) return;
        int amount = _store.Settings.SkipFor(_book);
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
        _book.Position = t;
        if (_book.IsSet)
        {
            msg = $"Chapter {_book.TrackIndex + 1} of {_book.Files.Count}: {TrackTitle(_book.TrackIndex)}. {TimeText.Spoken(t)} of {TimeText.Spoken(d)} in this chapter.";
            if (_book.Remaining is { } left) msg += $" About {TimeText.SpokenRoughly(left / rate)} left in the book at this speed.";
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

    // ---------------------------------------------------------------- speed, volume and skip amount

    /// <summary>Sets playback to the open book's own settings, or the defaults where it has none.</summary>
    private void ApplyAudioSettings()
    {
        var s = _store.Settings;
        _engine.Speed = s.SpeedFor(_book);
        _engine.VolumePercent = s.VolumeFor(_book);
        var skip = AudioSettingsPanel.SkipText(s.SkipFor(_book));
        BackLabel.Text = "−" + ShortSkip(s.SkipFor(_book));
        FwdLabel.Text = "+" + ShortSkip(s.SkipFor(_book));
        AutomationProperties.SetName(BackBtn, "Back " + skip);
        AutomationProperties.SetName(FwdBtn, "Forward " + skip);
        SpeedBadge.Text = AudioSettingsPanel.SpeedText(_engine.Speed) + " · Volume " + _engine.VolumePercent + "%";
    }

    private static string ShortSkip(int seconds) => seconds % 60 == 0 ? seconds / 60 + " min" : seconds + " s";

    private void ChangeSpeed(int direction)
    {
        if (_book is null) { _say.Say("Open a book first."); return; }
        var s = _store.Settings;
        var speed = Math.Round(Math.Clamp(s.SpeedFor(_book) + direction * SpeedStep, Settings.MinSpeed, Settings.MaxSpeed), 2);
        _book.Audio.Speed = Math.Abs(speed - s.Speed) < 0.001 ? null : speed;
        _store.Save();
        ApplyAudioSettings();
        _say.Say(AudioSettingsPanel.SpeedText(speed));
    }

    private void ChangeVolume(int delta)
    {
        if (_book is null) { _say.Say("Open a book first."); return; }
        var s = _store.Settings;
        var volume = Math.Clamp(s.VolumeFor(_book) + delta, 0, Settings.MaxVolume);
        _book.Audio.Volume = volume == s.Volume ? null : volume;
        _store.Save();
        ApplyAudioSettings();
        _say.Say("Volume " + volume + " percent" + (volume > 100 ? ", boosted" : ""));
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
        if (_book is not null && _engine.IsOpen)
        {
            _book.Position = t;
            BookLeft.Text = _book.Remaining is { } left && _engine.Speed > 0
                ? $"About {TimeText.SpokenRoughly(left / _engine.Speed)} left in the book"
                : "";
        }
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
                _chapterRows.Add(new Row($"{i + 1}. {title}", d > 0 ? "Length " + TimeText.Clock(d) : "",
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
        NoChapters.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        ChList.Visibility = ChGoBtn.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        PrevChBtn.IsEnabled = NextChBtn.IsEnabled = has;
        ChapterNow.Visibility = Visibility.Collapsed;
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
        if (!_engine.IsOpen) return;
        if (!HasChapters) { _say.Say("This book has no chapters."); return; }
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
        if (!_engine.IsOpen) return;
        if (!HasChapters) { _say.Say("This book has no chapters."); return; }
        int next = (_book!.IsSet ? _book.TrackIndex : ChapterAt(_engine.Position.TotalSeconds)) + 1;
        if (next >= ChapterCount) { _say.Say("This is the last chapter."); return; }
        JumpChapter(next);
    }

    // ---------------------------------------------------------------- bookmarks

    private void AddBookmark(bool askForNote)
    {
        if (_book is null || !_engine.IsOpen) { _say.Say("Open a book first."); return; }
        var t = _engine.Position.TotalSeconds;
        var note = "";
        if (askForNote)
        {
            var answer = Dialogs.Dialogs.Prompt(this, "Add bookmark", $"Note for the bookmark at {TimeText.Clock(t)}");
            if (answer is null) return;
            note = answer;
        }
        int ci = CurrentChapter;
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
        RenderBookmarks(select: b);
        var n = _book.Bookmarks.Count;
        _say.Say($"Bookmark added at {TimeText.Spoken(t)}{(note.Length > 0 ? ": " + note : "")}. You have {n} bookmark{(n == 1 ? "" : "s")}.");
    }

    private double BookmarkOrder(Bookmark b)
    {
        int track = _book!.IsSet && b.Track is not null ? Math.Max(0, _book.Files.FindIndex(f => Path.GetFileName(f) == b.Track)) : 0;
        return track * 1e7 + b.Time;
    }

    private void RenderBookmarks(Bookmark? select = null)
    {
        select ??= (BmList.SelectedItem as Row)?.Tag as Bookmark;
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
        BmList.Visibility = BmGoBtn.Visibility = BmEditBtn.Visibility = BmDeleteBtn.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        BookmarksTab.Header = any ? $"Bookmarks ({_bookmarkRows.Count})" : "Bookmarks";
        int index = select is null ? -1 : _bookmarkRows.ToList().FindIndex(r => r.Tag == select);
        BmList.SelectedIndex = index >= 0 ? index : any ? 0 : -1;
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

    private void EditSelectedBookmark()
    {
        if (BmList.SelectedItem is not Row { Tag: Bookmark b } || _book is null) { _say.Say("Select a bookmark first."); return; }
        var note = Dialogs.Dialogs.Prompt(this, "Edit bookmark note", $"Note for the bookmark at {TimeText.Clock(b.Time)}", b.Note);
        if (note is not null)
        {
            b.Note = note;
            _store.Save();
            RenderBookmarks(select: b);
            _say.Say("Bookmark note saved.");
        }
        FocusSelected(BmList);
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
}
