using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace AudiobookPlayer.Platform;

/// <summary>Speaks short messages through the screen reader (NVDA, JAWS, Narrator) using UI Automation notifications.</summary>
internal sealed class Announcer(UIElement source)
{
    /// <summary>Raised with each message, so it can also be shown on screen.</summary>
    public event Action<string>? Announced;

    public void Say(string message)
    {
        message = message.Trim();
        if (message.Length == 0) return;
        Announced?.Invoke(message);
        // Let focus changes from the same action settle first, so the message isn't cut off.
        source.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var peer = UIElementAutomationPeer.FromElement(source) ?? UIElementAutomationPeer.CreatePeerForElement(source);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.Other,
                AutomationNotificationProcessing.ImportantMostRecent,
                message,
                "AudiobookPlayer.Status");
        });
    }
}
