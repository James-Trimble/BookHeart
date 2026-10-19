using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BookHeart.Platform;

namespace BookHeart.Dialogs;

/// <summary>Small dialogs built in code: questions with named buttons, a text prompt, and the shortcut list.</summary>
internal static class Dialogs
{
    /// <summary>Gives a dialog the app's look and centres it on its owner.</summary>
    public static void Prepare(Window dialog, Window owner, string title)
    {
        dialog.Owner = owner;
        dialog.Title = title;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.ShowInTaskbar = false;
        dialog.SetResourceReference(Control.BackgroundProperty, "Bg");
        dialog.SetResourceReference(Control.ForegroundProperty, "Ink");
        dialog.FontFamily = (System.Windows.Media.FontFamily)Application.Current.Resources["BodyFont"];
        dialog.FontSize = 20;
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; dialog.Close(); } };
    }

    /// <summary>Asks a question with buttons that say what they do. Returns the chosen button's index, or -1 if closed.</summary>
    public static int Ask(Window owner, string title, string message, params string[] buttons)
    {
        var dlg = new Window { SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, MaxWidth = 640 };
        Prepare(dlg, owner, title);
        int chosen = -1;
        var panel = new StackPanel { Margin = new Thickness(24) };
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) };
        panel.Children.Add(text);
        var row = new WrapPanel();
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            var b = new Button { Content = buttons[i], IsDefault = i == 0 };
            if (i == 0) b.Style = (Style)Application.Current.Resources["PrimaryButton"];
            b.Click += (_, _) => { chosen = index; dlg.Close(); };
            row.Children.Add(b);
        }
        panel.Children.Add(row);
        dlg.Content = panel;
        System.Windows.Automation.AutomationProperties.SetHelpText(dlg, message);
        dlg.Loaded += (_, _) => (row.Children[0] as Button)?.Focus();
        dlg.ShowDialog();
        return chosen;
    }

    /// <summary>Asks for a line of text. Returns null if cancelled.</summary>
    public static string? Prompt(Window owner, string title, string label, string initial = "")
    {
        var dlg = new Window { SizeToContent = SizeToContent.Height, Width = 560, ResizeMode = ResizeMode.NoResize };
        Prepare(dlg, owner, title);
        string? result = null;
        var panel = new StackPanel { Margin = new Thickness(24) };
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 0, 0, 20) };
        var lbl = new Label { Content = label, Target = box, Style = (Style)Application.Current.Resources["FieldLabel"] };
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        panel.Children.Add(lbl);
        panel.Children.Add(box);
        var row = new WrapPanel();
        var ok = new Button { Content = "OK", IsDefault = true, Style = (Style)Application.Current.Resources["PrimaryButton"] };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        ok.Click += (_, _) => { result = box.Text.Trim(); dlg.Close(); };
        row.Children.Add(ok);
        row.Children.Add(cancel);
        panel.Children.Add(row);
        dlg.Content = panel;
        dlg.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        dlg.ShowDialog();
        return result;
    }

    public static readonly (string Keys, string Action)[] Shortcuts =
    [
        ("Alt + P", "Play or pause"),
        ("Alt + Left arrow", "Skip back"),
        ("Alt + Right arrow", "Skip forward"),
        ("Control + Left arrow", "Previous chapter"),
        ("Control + Right arrow", "Next chapter"),
        ("Control + Up arrow", "Faster"),
        ("Control + Down arrow", "Slower"),
        ("Alt + Up arrow", "Volume up"),
        ("Alt + Down arrow", "Volume down"),
        ("Alt + B", "Add a bookmark at the current spot"),
        ("Control + B", "Add a bookmark with a note"),
        ("Alt + I", "Hear where you are in the book"),
        ("Control + E", "Book settings: speed, volume, skip amount, title"),
        ("Alt + R", "Resume the book you listened to most recently"),
        ("Control + O", "Add books to the library"),
        ("Control + Shift + O", "Add a folder of books"),
        ("Control + 1", "Go to the library"),
        ("Control + 2", "Go to the book that's playing"),
        ("Escape", "From the book that's playing, back to the library"),
        ("Control + Comma", "Settings"),
        ("Enter", "In a list: play or open the selected item"),
        ("Delete", "In a list: remove the selected book or bookmark"),
        ("Applications key", "In the book list: more actions for the selected book"),
        ("F1", "This list of shortcuts"),
    ];

    public static void ShowShortcuts(Window owner)
    {
        var dlg = new Window { Width = 720, Height = 640 };
        Prepare(dlg, owner, "Keyboard shortcuts");
        var list = new ListBox
        {
            ItemContainerStyle = (Style)Application.Current.Resources["RowItem"],
            ItemTemplate = (DataTemplate)Application.Current.Resources["RowTemplate"],
            MaxHeight = double.PositiveInfinity,
            Margin = new Thickness(24, 24, 24, 12),
        };
        System.Windows.Automation.AutomationProperties.SetName(list, "Keyboard shortcuts");
        list.ItemsSource = Shortcuts.Select(s => new Row(s.Keys, s.Action, $"{s.Keys}: {s.Action}")).ToList();
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, Margin = new Thickness(24, 0, 24, 24), HorizontalAlignment = HorizontalAlignment.Left };
        close.Click += (_, _) => dlg.Close();
        var dock = new DockPanel();
        DockPanel.SetDock(close, Dock.Bottom);
        dock.Children.Add(close);
        dock.Children.Add(list);
        dlg.Content = dock;
        dlg.Loaded += (_, _) => { list.SelectedIndex = 0; (list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus(); };
        dlg.ShowDialog();
    }

    public static void ShowAbout(Window owner)
    {
        var where = AppInfo.IsPortable ? "Portable copy. Your library is kept next to the program." : "Your library is kept in your user folder.";
        Ask(owner, "About " + AppInfo.Name,
            $"{AppInfo.Name} version {AppInfo.Version}\n\nAn audiobook player made for listening with a keyboard and a screen reader.\n\n" +
            $"Developed by {AppInfo.Developer}.\n{AppInfo.Copyright}. All rights reserved.\n\n" +
            "Use of this program is covered by the licence agreement in the Help menu. " +
            "It includes open-source components listed under Third-party notices.\n\n" + where,
            "Close");
    }
}
