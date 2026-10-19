using System.Windows;
using System.Windows.Threading;
using BookHeart.Core;
using BookHeart.Dialogs;
using BookHeart.Platform;

namespace BookHeart;

/// <summary>Checking for new versions: at startup, once a day, and from Help → Check for updates.</summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(1) };
    private ReleaseInfo? _pendingUpdate;
    private bool _checkingForUpdates;

    private void StartUpdateChecks()
    {
        _updateTimer.Tick += async (_, _) =>
        {
            if (UpdateCheckDue) await CheckForUpdatesAsync(manual: false);
            else ShowPendingUpdate();
        };
        _updateTimer.Start();
        Activated += (_, _) => ShowPendingUpdate();
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
            if (manual) ShowUpdate(update);
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

    /// <summary>Shows an update found earlier, but never in the middle of listening or over another window.</summary>
    private void ShowPendingUpdate()
    {
        if (_pendingUpdate is null || _engine.IsPlaying || !IsActive || OwnedWindows.Count > 0) return;
        var update = _pendingUpdate;
        _pendingUpdate = null;
        ShowUpdate(update);
    }

    private void ShowUpdate(ReleaseInfo update)
    {
        var dlg = UpdateDialog.ShowFor(this, update);
        switch (dlg.Choice)
        {
            case UpdateChoice.Skip:
                _store.Settings.SkippedVersion = update.Version.ToString();
                _store.Save();
                _say.Say($"Skipped version {update.Version}. You'll be told about the next one.");
                break;
            case UpdateChoice.Install when dlg.DownloadedFile is not null:
                SavePosition();
                // Remember where you were, to come back to it once the new version opens.
                _store.Settings.ResumeView = PlayerShown && _book is not null ? "player" : "library";
                _store.Settings.ResumeBookKey = _book?.Key;
                _store.Save();
                try
                {
                    Updater.StartInstall(dlg.DownloadedFile);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    _store.Settings.ResumeView = _store.Settings.ResumeBookKey = null;
                    _store.Save();
                    _say.Say("The update couldn't be started. " + ex.Message);
                }
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

    /// <summary>Says so when this is the first run of a newer version, however BookHeart was opened.</summary>
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
