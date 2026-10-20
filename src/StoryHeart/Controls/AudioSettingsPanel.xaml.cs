using System.Windows;
using System.Windows.Controls;

namespace StoryHeart.Controls;

/// <summary>Speed, volume and skip amount controls, shared by Book settings and Settings.</summary>
public partial class AudioSettingsPanel : UserControl
{
    public static readonly (int Seconds, string Label)[] Skips =
        [(5, "5 seconds"), (10, "10 seconds"), (15, "15 seconds"), (30, "30 seconds"), (60, "1 minute"), (120, "2 minutes"), (300, "5 minutes")];

    private bool _updating;

    /// <summary>Raised when you change any of the values.</summary>
    public event Action? Changed;

    /// <summary>Raised by the "Use default" buttons with "speed", "volume" or "skip".</summary>
    public event Action<string>? ResetRequested;

    public AudioSettingsPanel()
    {
        InitializeComponent();
        foreach (var (_, label) in Skips) Skip.Items.Add(label);
        Speed.ValueChanged += (_, _) => { ShowValues(); if (!_updating) Changed?.Invoke(); };
        Volume.ValueChanged += (_, _) => { ShowValues(); if (!_updating) Changed?.Invoke(); };
        Skip.SelectionChanged += (_, _) => { if (!_updating) Changed?.Invoke(); };
        SpeedReset.Click += (_, _) => ResetRequested?.Invoke("speed");
        VolumeReset.Click += (_, _) => ResetRequested?.Invoke("volume");
        SkipReset.Click += (_, _) => ResetRequested?.Invoke("skip");
    }

    public double SpeedSetting => Math.Round(Speed.Value, 2);
    public int VolumePercent => (int)Math.Round(Volume.Value);
    public int SkipSeconds => Skips[Math.Max(0, Skip.SelectedIndex)].Seconds;

    public void SetValues(double speed, int volume, int skipSeconds)
    {
        _updating = true;
        Speed.Value = speed;
        Volume.Value = volume;
        int i = Array.FindIndex(Skips, s => s.Seconds == skipSeconds);
        Skip.SelectedIndex = i >= 0 ? i : Array.FindIndex(Skips, s => s.Seconds == 30);
        _updating = false;
        ShowValues();
    }

    /// <summary>Shows a "Use default" button under each setting that differs from the defaults.</summary>
    public void ShowResets(bool speed, bool volume, bool skip, double defSpeed, int defVolume, int defSkip)
    {
        SetReset(SpeedReset, speed, $"Use default speed ({SpeedText(defSpeed)})");
        SetReset(VolumeReset, volume, $"Use default volume ({defVolume} percent)");
        SetReset(SkipReset, skip, $"Use default skip amount ({SkipText(defSkip)})");
    }

    private static void SetReset(Button b, bool show, string text)
    {
        b.Content = text;
        b.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowValues()
    {
        SpeedValue.Text = SpeedText(Speed.Value);
        Speed.ValueText = SpeedText(Speed.Value);
        var vol = VolumePercent + " percent" + (VolumePercent > 100 ? ", boosted" : "");
        VolumeValue.Text = vol;
        Volume.ValueText = vol;
    }

    public static string SpeedText(double speed) =>
        Math.Abs(speed - 1) < 0.001 ? "Normal speed" : Math.Round(speed, 2).ToString("0.0#") + " times";

    public static string SkipText(int seconds) =>
        Skips.FirstOrDefault(s => s.Seconds == seconds).Label ?? Core.TimeText.Spoken(seconds);
}
