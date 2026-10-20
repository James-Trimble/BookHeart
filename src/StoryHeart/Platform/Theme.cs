using System.Windows;
using System.Windows.Media;
using StoryHeart.Core;
using Microsoft.Win32;

namespace StoryHeart.Platform;

/// <summary>
/// Sets the app's colours: light or dark, following Windows unless chosen in Settings. When a
/// high-contrast theme is on, its colours are always used.
/// </summary>
internal static class Theme
{
    public static void Apply(ResourceDictionary r, ThemeChoice choice)
    {
        if (SystemParameters.HighContrast)
        {
            r["Bg"] = SystemColors.WindowBrush;
            r["Surface"] = SystemColors.WindowBrush;
            r["Ink"] = SystemColors.WindowTextBrush;
            r["Muted"] = SystemColors.WindowTextBrush;
            r["Primary"] = SystemColors.HighlightBrush;
            r["PrimaryInk"] = SystemColors.HighlightTextBrush;
            r["Line"] = SystemColors.WindowTextBrush;
            r["Focus"] = SystemColors.HighlightBrush;
            r["FocusRing"] = SystemColors.WindowTextBrush;
            r["Danger"] = SystemColors.WindowTextBrush;
            return;
        }

        bool dark = choice switch { ThemeChoice.Dark => true, ThemeChoice.Light => false, _ => IsDarkMode() };
        Set(r, "Bg", dark ? "#0A1628" : "#F3F7FB");
        Set(r, "Surface", dark ? "#13233B" : "#FFFFFF");
        Set(r, "Ink", dark ? "#F2F6FA" : "#0A2540");
        Set(r, "Muted", dark ? "#B8C8DA" : "#3D5570");
        Set(r, "Primary", dark ? "#FFD100" : "#1747B8");
        Set(r, "PrimaryInk", dark ? "#0A1628" : "#FFFFFF");
        Set(r, "Line", dark ? "#3A5475" : "#B9C7D8");
        Set(r, "Focus", dark ? "#7FC4FF" : "#FFD100");
        Set(r, "FocusRing", dark ? "#F2F6FA" : "#0A2540");
        Set(r, "Danger", dark ? "#FF8A8A" : "#A61B1B");
    }

    private static void Set(ResourceDictionary r, string key, string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        r[key] = brush;
    }

    private static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception) { return false; }
    }
}
