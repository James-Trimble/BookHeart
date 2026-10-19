using System.Windows;
using BookHeart.Core;

namespace BookHeart.Dialogs;

/// <summary>App-wide settings: default listening settings, colours and startup.</summary>
public partial class SettingsDialog : Window
{
    private static readonly (ThemeChoice Value, string Label)[] Themes =
        [(ThemeChoice.System, "Follow Windows"), (ThemeChoice.Light, "Light"), (ThemeChoice.Dark, "Dark")];

    private bool _saved;
    private bool _clearBooks;

    private SettingsDialog(Window owner, Settings settings)
    {
        InitializeComponent();
        Dialogs.Prepare(this, owner, "Settings");
        Audio.SetValues(settings.Speed, settings.Volume, settings.SkipSeconds);
        foreach (var (_, label) in Themes) ThemeBox.Items.Add(label);
        ThemeBox.SelectedIndex = Array.FindIndex(Themes, t => t.Value == settings.Theme);
        OpenLastBox.IsChecked = settings.OpenLastBookOnStart;
        CheckUpdatesBox.IsChecked = settings.CheckForUpdates;
        TestVersionsBox.IsChecked = settings.IncludeTestVersions;

        ApplyAllBtn.Click += (_, _) =>
        {
            var answer = Dialogs.Ask(this, "Use for every book",
                "Every book will use these default settings, and the speed, volume and skip amount you set for individual books will be cleared. This happens when you choose Save.",
                "Use for every book", "Cancel");
            _clearBooks = answer == 0 || _clearBooks;
            ApplyAllBtn.Focus();
        };
        OkBtn.Click += (_, _) => { _saved = true; Close(); };
        Loaded += (_, _) => Audio.Speed.Focus();
    }

    /// <summary>Shows Settings and applies the changes if saved. Returns true if anything was saved.</summary>
    public static bool Show(Window owner, Settings settings, IEnumerable<BookEntry> books)
    {
        var dlg = new SettingsDialog(owner, settings);
        dlg.ShowDialog();
        if (!dlg._saved) return false;
        settings.Speed = dlg.Audio.SpeedSetting;
        settings.Volume = dlg.Audio.VolumePercent;
        settings.SkipSeconds = dlg.Audio.SkipSeconds;
        settings.Theme = Themes[Math.Max(0, dlg.ThemeBox.SelectedIndex)].Value;
        settings.OpenLastBookOnStart = dlg.OpenLastBox.IsChecked == true;
        settings.CheckForUpdates = dlg.CheckUpdatesBox.IsChecked == true;
        if (dlg.TestVersionsBox.IsChecked == true && !settings.IncludeTestVersions) settings.SkippedVersion = null;
        settings.IncludeTestVersions = dlg.TestVersionsBox.IsChecked == true;
        if (dlg._clearBooks)
            foreach (var b in books) b.Audio = new BookAudio();
        return true;
    }
}
