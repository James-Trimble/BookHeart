using System.IO;
using System.Windows;
using System.Windows.Threading;
using StoryHeart.Core;
using StoryHeart.Dialogs;
using StoryHeart.Platform;

namespace StoryHeart;

/// <summary>
/// Checking for new versions: at startup, once a day, and from Help → Check for updates. Updates found by
/// themselves download in the background while you listen; when one is ready, your book pauses and you're
/// asked whether to install it now or when you close StoryHeart.
/// </summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(1) };
    private ReleaseInfo? _pendingUpdate;
    private bool _checkingForUpdates;
    private ReleaseInfo? _downloading;
    private (ReleaseInfo Release, string File)? _downloaded;
    /// <summary>An update you chose to install later: it installs when StoryHeart closes.</summary>
    private string? _installOnClose;

    private void StartUpdateChecks()
    {
        _updateTimer.Tick += async (_, _) =>
        {
            if (UpdateCheckDue) await CheckForUpdatesAsync(manual: false);
            else ShowPendingUpdate();
        };
        _updateTimer.Start();
        Activated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ShowPendingUpdate);
        if (_store.Settings.CheckForUpdates)
            Dispatcher.InvokeAsync(async () =>
            {
                // Give the window a moment to settle and announce itself first.
                await Task.Delay(TimeSpan.FromSeconds(3));
                await CheckForUpdatesAsync(manual: false);
            });
    }

    private bool UpdateCheckDue =>
        _store.Settings.CheckForUpdates &&
        (_store.Settings.LastUpdateCheck is not { } last || DateTimeOffset.Now - last >= TimeSpan.FromDays(1));

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_checkingForUpdates) return;
        _checkingForUpdates = true;
        if (manual) _say.Say("Checking for updates…");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var releases = await Updater.FetchReleasesAsync(timeout.Token);
            var s = _store.Settings;
            s.LastUpdateCheck = DateTimeOffset.Now;
            _store.Save();
            var update = UpdateFeed.FindUpdate(releases, Updater.CurrentVersion, s.IncludeTestVersions, s.SkippedVersion, ignoreSkipped: manual);
            if (update is null)
            {
                if (manual) _say.Say($"You have the latest version, {AppInfo.Name} {AppInfo.Version}.");
                return;
            }
            if (_downloaded is { } ready && ready.Release.Version.CompareTo(update.Version) >= 0)
            {
                if (manual) ShowDownloadedUpdate(paused: false);
                return;
            }
            if (_downloading is not null)
            {
                if (manual) _say.Say($"{AppInfo.Name} {_downloading.Version} is downloading. You'll be asked about installing it when it's ready.");
                return;
            }
            if (manual) ShowUpdate(update);
            else if (Updater.CanUpdateItself && Updater.FileFor(update) is not null) DownloadInBackground(update);
            else
            {
                _pendingUpdate = update;
                ShowPendingUpdate();
            }
        }
        catch (Exception ex) when (manual)
        {
            _say.Say("Couldn't check for updates. Check that you're connected to the internet, then try again. " + ex.Message);
        }
        catch (Exception)
        {
            // Automatic checks fail quietly, e.g. when offline. The next one is tomorrow.
        }
        finally
        {
            _checkingForUpdates = false;
        }
    }

    /// <summary>
    /// Shows an update found earlier, never over another window or while you're in another program. One that
    /// still needs downloading waits until you pause; one that has finished downloading pauses your book.
    /// </summary>
    private void ShowPendingUpdate()
    {
        if (!IsActive || OwnedWindows.Count > 0) return;
        if (_downloaded is not null && _installOnClose is null)
        {
            bool wasPlaying = _engine.IsPlaying;
            if (wasPlaying) TogglePlay();
            if (ShowDownloadedUpdate(paused: wasPlaying) == UpdateChoice.Later && wasPlaying && !_engine.IsPlaying) TogglePlay();
            return;
        }
        if (_pendingUpdate is null || _engine.IsPlaying) return;
        var update = _pendingUpdate;
        _pendingUpdate = null;
        ShowUpdate(update);
    }

    /// <summary>Downloads an update while you carry on listening. Nothing is said unless it's ready.</summary>
    private async void DownloadInBackground(ReleaseInfo update)
    {
        _downloading = update;
        try
        {
            var file = await Updater.DownloadAsync(Updater.FileFor(update)!, new Progress<double>(), CancellationToken.None);
            _downloaded = (update, file);
            // A newer update replaces one you'd put off, and is offered in its place.
            _installOnClose = null;
            ShowPendingUpdate();
        }
        catch (Exception)
        {
            // Tried again at the next check, tomorrow.
        }
        finally
        {
            _downloading = null;
        }
    }

    private UpdateChoice ShowDownloadedUpdate(bool paused)
    {
        var (release, file) = _downloaded!.Value;
        var dlg = UpdateDialog.ShowDownloaded(this, release, file, paused);
        switch (dlg.Choice)
        {
            case UpdateChoice.Install:
                InstallNow(file);
                break;
            case UpdateChoice.Skip:
                _downloaded = null;
                _installOnClose = null;
                SkipVersion(release);
                try { File.Delete(file); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                break;
            default:
                _installOnClose = file;
                _say.Say($"{AppInfo.Name} {release.Version} will install when you close {AppInfo.Name}.");
                break;
        }
        return dlg.Choice;
    }

    /// <summary>Installs an update you chose to install later, as StoryHeart closes. It doesn't reopen afterwards.</summary>
    private void InstallOnClose()
    {
        if (_installOnClose is null || !File.Exists(_installOnClose)) return;
        try { Updater.StartInstall(_installOnClose, reopen: false); }
        catch (Exception) { /* The next check finds it again. */ }
    }

    private void SkipVersion(ReleaseInfo update)
    {
        _store.Settings.SkippedVersion = update.Version.ToString();
        _store.Save();
        _say.Say($"Skipped version {update.Version}. You'll be told about the next one.");
    }

    private void InstallNow(string file)
    {
        SavePosition();
        // Remember where you were, to come back to it once the new version opens.
        _store.Settings.ResumeView = PlayerShown && _book is not null ? "player" : "library";
        _store.Settings.ResumeBookKey = _book?.Key;
        _store.Save();
        try
        {
            Updater.StartInstall(file);
            _installOnClose = null;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _store.Settings.ResumeView = _store.Settings.ResumeBookKey = null;
            _store.Save();
            _say.Say("The update couldn't be started. " + ex.Message);
        }
    }

    private void ShowUpdate(ReleaseInfo update)
    {
        var dlg = UpdateDialog.ShowFor(this, update);
        switch (dlg.Choice)
        {
            case UpdateChoice.Skip:
                SkipVersion(update);
                break;
            case UpdateChoice.Install when dlg.DownloadedFile is not null:
                InstallNow(dlg.DownloadedFile);
                break;
        }
    }

    /// <summary>After an update, comes back to the screen you were on: your book if it was open, otherwise the library.</summary>
    private bool RestoreAfterUpdate()
    {
        var s = _store.Settings;
        if (s.ResumeView is null) return false;
        var view = s.ResumeView;
        var book = s.ResumeBookKey is null ? null : _store.Find(s.ResumeBookKey);
        s.ResumeView = s.ResumeBookKey = null;
        _store.Save();
        if (view == "player" && book is not null && book.MissingFiles().Count == 0) OpenEntry(book, autoplay: false);
        return true;
    }

    /// <summary>
    /// Shows the licence agreement once when it has changed since you last saw it, and the first time a portable
    /// copy runs (it has no installer to show it). Returns false if you don't agree, and StoryHeart closes.
    /// </summary>
    private bool CheckAgreement()
    {
        string text;
        try { text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EULA.txt")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return true; }
        if (Agreement.LastUpdated(text) is not { } date) return true;

        var s = _store.Settings;
        if (s.AgreementSeen == date) return true;
        bool firstTime = s.AgreementSeen is null;
        if (firstTime && Updater.IsInstalled)
        {
            // The installer has just shown it.
            s.AgreementSeen = date;
            _store.Save();
            return true;
        }
        var intro = firstTime
            ? $"Before you use {AppInfo.Name}, please read its licence agreement."
            : $"{AppInfo.Name}'s licence agreement has been updated ({date}). Please read it before carrying on.";
        if (!Dialogs.Dialogs.ShowAgreement(this, intro, text)) return false;
        s.AgreementSeen = date;
        _store.Save();
        return true;
    }

    /// <summary>Says so when this is the first run of a newer version, however StoryHeart was opened.</summary>
    private void AnnounceIfUpdated()
    {
        var s = _store.Settings;
        var previous = AppVersion.TryParse(s.LastVersionRun);
        s.LastVersionRun = AppInfo.Version;
        _store.Save();
        if (previous is null || !(Updater.CurrentVersion > previous)) return;
        _say.Say($"{AppInfo.Name} has been updated to version {AppInfo.Version}." +
                 (_book is not null && PlayerShown ? $" {_book.Title} is ready where you left off." : ""));
    }
}
