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
                _store.Save();
                try
                {
                    Updater.StartInstall(dlg.DownloadedFile);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    _say.Say("The update couldn't be started. " + ex.Message);
                }
                break;
        }
    }

    /// <summary>After an update, says so and opens the book you were listening to.</summary>
    private void WelcomeBackAfterUpdate()
    {
        if (_store.LastPlayed is { } last && last.MissingFiles().Count == 0) OpenEntry(last, autoplay: false);
        _say.Say($"{AppInfo.Name} has been updated to version {AppInfo.Version}." +
                 (_book is not null ? $" {_book.Title} is ready where you left off." : ""));
    }
}
