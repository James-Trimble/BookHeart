using System.Windows;
using System.Windows.Interop;
using Windows.Media;

namespace StoryHeart.Platform;

/// <summary>
/// Connects to the Windows media controls: keyboard and headphone media keys, and the
/// volume flyout / lock screen panel that shows what's playing.
/// </summary>
internal sealed class MediaControls
{
    private readonly SystemMediaTransportControls? _smtc;

    public event Action? PlayPressed;
    public event Action? PausePressed;
    public event Action? NextPressed;
    public event Action? PreviousPressed;
    public event Action? ForwardPressed;
    public event Action? BackPressed;

    /// <summary>True when Windows media controls are connected; false means media keys are handled by the window instead.</summary>
    public bool Available => _smtc is not null;

    public MediaControls(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            _smtc = SystemMediaTransportControlsInterop.GetForWindow(hwnd);
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = _smtc.IsPreviousEnabled = true;
            _smtc.IsFastForwardEnabled = _smtc.IsRewindEnabled = true;
            _smtc.ButtonPressed += (_, e) => window.Dispatcher.BeginInvoke(() => Raise(e.Button));
        }
        catch (Exception)
        {
            _smtc = null;
        }
    }

    public void SetPlaying(bool playing)
    {
        if (_smtc is null) return;
        _smtc.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
    }

    public void SetClosed()
    {
        if (_smtc is null) return;
        _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
        _smtc.DisplayUpdater.ClearAll();
        _smtc.DisplayUpdater.Update();
    }

    public void SetNowPlaying(string title, string artist)
    {
        if (_smtc is null) return;
        var d = _smtc.DisplayUpdater;
        d.Type = MediaPlaybackType.Music;
        d.MusicProperties.Title = title;
        d.MusicProperties.Artist = artist;
        d.Update();
    }

    private void Raise(SystemMediaTransportControlsButton button)
    {
        switch (button)
        {
            case SystemMediaTransportControlsButton.Play: PlayPressed?.Invoke(); break;
            case SystemMediaTransportControlsButton.Pause: PausePressed?.Invoke(); break;
            case SystemMediaTransportControlsButton.Next: NextPressed?.Invoke(); break;
            case SystemMediaTransportControlsButton.Previous: PreviousPressed?.Invoke(); break;
            case SystemMediaTransportControlsButton.FastForward: ForwardPressed?.Invoke(); break;
            case SystemMediaTransportControlsButton.Rewind: BackPressed?.Invoke(); break;
        }
    }
}
