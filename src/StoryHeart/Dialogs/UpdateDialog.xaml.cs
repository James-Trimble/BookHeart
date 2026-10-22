using System.Windows;
using StoryHeart.Core;
using StoryHeart.Platform;

namespace StoryHeart.Dialogs;

public enum UpdateChoice { Later, Skip, Install }

/// <summary>Tells you a new version is out, shows what's new, and downloads it if you choose to install.</summary>
public partial class UpdateDialog : Window
{
    private readonly ReleaseInfo _release;
    private readonly Announcer _say;
    private CancellationTokenSource? _download;
    private int _lastSpokenQuarter;

    public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    /// <summary>The downloaded file, ready to install, when <see cref="Choice"/> is Install.</summary>
    public string? DownloadedFile { get; private set; }

    private UpdateDialog(Window owner, ReleaseInfo release)
    {
        InitializeComponent();
        _release = release;
        Dialogs.Prepare(this, owner, "Update available");
        _say = new Announcer(this);

        Heading.Text = $"{AppInfo.Name} {release.Version} is available";
        Intro.Text = $"You have version {AppInfo.Version}." +
                     (release.IsPreRelease ? " This is a test version, offered because you chose to get test versions in Settings." : "") +
                     (Updater.CanUpdateItself ? "" : " This copy can't update itself, so Install now opens the download page.");
        var changes = release.Changes.Count > 0 ? release.Changes : ["No details were given for this version."];
        Changes.ItemsSource = changes.Select(c => new Row(c, "", c)).ToList();
        Changes.SelectedIndex = 0;

        InstallBtn.Click += async (_, _) => await InstallAsync();
        LaterBtn.Click += (_, _) => { Choice = UpdateChoice.Later; Close(); };
        SkipBtn.Click += (_, _) => { Choice = UpdateChoice.Skip; Close(); };
        CancelBtn.Click += (_, _) => _download?.Cancel();
        Closing += (_, e) =>
        {
            // Closing mid-download cancels the download first, rather than leaving it running.
            if (_download is not null) { _download.Cancel(); e.Cancel = true; }
        };
        Loaded += (_, _) => InstallBtn.Focus();
    }

    public static UpdateDialog ShowFor(Window owner, ReleaseInfo release)
    {
        var dlg = new UpdateDialog(owner, release);
        dlg.ShowDialog();
        return dlg;
    }

    private async Task InstallAsync()
    {
        if (!Updater.CanUpdateItself)
        {
            OpenPage(_release.PageUrl);
            Choice = UpdateChoice.Later;
            Close();
            return;
        }
        var file = Updater.FileFor(_release);
        if (file is null)
        {
            _say.Say("This version has no download for your copy of StoryHeart. Opening the download page instead.");
            OpenPage(_release.PageUrl);
            return;
        }

        Choices.Visibility = Visibility.Collapsed;
        CancelBtn.Visibility = ProgressPanel.Visibility = Visibility.Visible;
        CancelBtn.Focus();
        ProgressText.Text = "Downloading…";
        _say.Say($"Downloading {AppInfo.Name} {_release.Version}.");
        _download = new CancellationTokenSource();
        var progress = new Progress<double>(f =>
        {
            Progress.Value = f * 100;
            ProgressText.Text = $"Downloading… {(int)(f * 100)}%";
            int quarter = (int)(f * 4);
            if (quarter > _lastSpokenQuarter && quarter < 4) { _lastSpokenQuarter = quarter; _say.Say($"{quarter * 25} percent"); }
        });
        try
        {
            DownloadedFile = await Updater.DownloadAsync(file, progress, _download.Token);
            Choice = UpdateChoice.Install;
            _download = null;
            ProgressText.Text = $"Installing. {AppInfo.Name} will close and open again in a moment.";
            _say.Say(ProgressText.Text);
            Close();
        }
        catch (OperationCanceledException)
        {
            _download = null;
            ShowChoicesAgain("Download cancelled.");
        }
        catch (Exception ex)
        {
            _download = null;
            ShowChoicesAgain("The update couldn't be downloaded. " + ex.Message);
        }
    }

    private void ShowChoicesAgain(string message)
    {
        ProgressPanel.Visibility = CancelBtn.Visibility = Visibility.Collapsed;
        Choices.Visibility = Visibility.Visible;
        _lastSpokenQuarter = 0;
        Intro.Text = message;
        _say.Say(message);
        InstallBtn.Focus();
    }

    private static void OpenPage(string url)
    {
        if (url.Length == 0) url = "https://github.com/James-Trimble/StoryHeart/releases/latest";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { }
    }
}
