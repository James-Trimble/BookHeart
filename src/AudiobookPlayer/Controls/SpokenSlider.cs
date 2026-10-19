using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace AudiobookPlayer.Controls;

/// <summary>
/// A slider that tells screen readers its value in words ("12 minutes 5 seconds of 1 hour")
/// rather than a bare number.
/// </summary>
public class SpokenSlider : Slider
{
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(SpokenSlider),
        new PropertyMetadata("", (d, e) => ((SpokenSlider)d).OnValueTextChanged((string)e.OldValue, (string)e.NewValue)));

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private void OnValueTextChanged(string oldValue, string newValue)
    {
        if (UIElementAutomationPeer.FromElement(this) is Peer peer)
            peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, newValue);
    }

    private sealed class Peer(SpokenSlider owner) : SliderAutomationPeer(owner), IValueProvider
    {
        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

        string IValueProvider.Value => owner.ValueText;
        bool IValueProvider.IsReadOnly => true;
        void IValueProvider.SetValue(string value) => throw new InvalidOperationException();
    }
}
