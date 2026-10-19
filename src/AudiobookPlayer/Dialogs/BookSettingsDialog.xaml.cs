using System.Diagnostics;
using System.IO;
using System.Windows;
using AudiobookPlayer.Core;

namespace AudiobookPlayer.Dialogs;

/// <summary>A book's own title and audio settings, plus its files.</summary>
public partial class BookSettingsDialog : Window
{
    private readonly BookEntry _book;
    private readonly Settings _defaults;

    /// <summary>Raised whenever a setting changes, so playback can follow along.</summary>
    public event Action? Changed;

    /// <summary>Raised when the file order is changed; the new order is already in the book.</summary>
    public event Action? OrderChanged;

    public BookSettingsDialog(Window owner, BookEntry book, Settings defaults, string audioInfo)
    {
        InitializeComponent();
        Dialogs.Prepare(this, owner, "Book settings – " + book.Title);
        _book = book;
        _defaults = defaults;

        Heading.Text = book.Title;
        TitleBox.Text = book.Title;
        Load();

        var folder = Path.GetDirectoryName(book.Files[0]) ?? "";
        FilesInfo.Text = (book.IsSet ? $"{book.Files.Count} files in {folder}" : book.Files[0]) +
                         (book.Size > 0 ? $"\n{TimeText.Size(book.Size)}" : "") +
                         (book.Duration > 0 ? $", {TimeText.Spoken(book.Duration)} long" : "") +
                         (audioInfo.Length > 0 ? "\n" + audioInfo : "");
        OrderBtn.Visibility = book.IsSet ? Visibility.Visible : Visibility.Collapsed;

        TitleBox.LostKeyboardFocus += (_, _) => SaveTitle();
        Closing += (_, _) => SaveTitle();
        Audio.Changed += () =>
        {
            book.Audio.Speed = Same(Audio.SpeedSetting, defaults.Speed) ? null : Audio.SpeedSetting;
            book.Audio.Volume = Audio.VolumePercent == defaults.Volume ? null : Audio.VolumePercent;
            book.Audio.SkipSeconds = Audio.SkipSeconds == defaults.SkipSeconds ? null : Audio.SkipSeconds;
            ShowResets();
            Changed?.Invoke();
        };
        Audio.ResetRequested += what =>
        {
            if (what == "speed") book.Audio.Speed = null;
            else if (what == "volume") book.Audio.Volume = null;
            else book.Audio.SkipSeconds = null;
            Load();
            Changed?.Invoke();
        };
        OrderBtn.Click += (_, _) =>
        {
            var order = FileOrderDialog.Show(this, book.Files);
            if (order is null) return;
            book.Files = order;
            OrderChanged?.Invoke();
        };
        ExplorerBtn.Click += (_, _) => ShowInExplorer(book.Files[0]);
        CloseBtn.Click += (_, _) => Close();
        Loaded += (_, _) => TitleBox.Focus();
    }

    public static void ShowInExplorer(string file)
    {
        try
        {
            if (File.Exists(file)) Process.Start("explorer.exe", $"/select,\"{file}\"");
            else if (Path.GetDirectoryName(file) is { } dir && Directory.Exists(dir)) Process.Start("explorer.exe", $"\"{dir}\"");
        }
        catch (Exception) { }
    }

    private void Load()
    {
        Audio.SetValues(_defaults.SpeedFor(_book), _defaults.VolumeFor(_book), _defaults.SkipFor(_book));
        ShowResets();
    }

    private void ShowResets() => Audio.ShowResets(
        _book.Audio.Speed is not null, _book.Audio.Volume is not null, _book.Audio.SkipSeconds is not null,
        _defaults.Speed, _defaults.Volume, _defaults.SkipSeconds);

    private void SaveTitle()
    {
        var t = TitleBox.Text.Trim();
        if (t.Length == 0 || t == _book.Title) return;
        _book.Title = t;
        _book.TitleEdited = true;
        Heading.Text = t;
        Title = "Book settings – " + t;
        Changed?.Invoke();
    }

    private static bool Same(double a, double b) => Math.Abs(a - b) < 0.001;
}
