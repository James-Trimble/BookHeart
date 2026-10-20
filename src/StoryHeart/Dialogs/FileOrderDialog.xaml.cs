using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StoryHeart.Core;
using StoryHeart.Platform;

namespace StoryHeart.Dialogs;

/// <summary>Lets you check and change the order a book's files play in.</summary>
public partial class FileOrderDialog : Window
{
    private readonly List<string> _files;
    private readonly ObservableCollection<Row> _rows = [];
    private readonly Announcer _say;
    private bool _saved;

    private FileOrderDialog(Window owner, List<string> files)
    {
        InitializeComponent();
        Dialogs.Prepare(this, owner, "Change file order");
        _files = files.ToList();
        _say = new Announcer(this);
        List.ItemsSource = _rows;
        Intro.Text = $"{files.Count} files play in this order. Select a file, then use Move up, Move down or Move to position. " +
                     "Control plus Up or Down arrow also moves it.";

        UpBtn.Click += (_, _) => Move(List.SelectedIndex, List.SelectedIndex - 1);
        DownBtn.Click += (_, _) => Move(List.SelectedIndex, List.SelectedIndex + 1);
        ToBtn.Click += (_, _) => MoveToPrompt();
        ResetBtn.Click += (_, _) =>
        {
            var sorted = NaturalComparer.Sort(_files);
            _files.Clear();
            _files.AddRange(sorted);
            Render(0);
            _say.Say("Sorted by file name.");
        };
        OkBtn.Click += (_, _) => { _saved = true; Close(); };
        List.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.Up) { e.Handled = true; Move(List.SelectedIndex, List.SelectedIndex - 1); }
            else if (e.Key == Key.Down) { e.Handled = true; Move(List.SelectedIndex, List.SelectedIndex + 1); }
        };
        Loaded += (_, _) => { Render(0); FocusSelected(); };
    }

    /// <summary>Shows the dialog. Returns the new order, or null if cancelled.</summary>
    public static List<string>? Show(Window owner, IReadOnlyList<string> files)
    {
        var dlg = new FileOrderDialog(owner, files.ToList());
        dlg.ShowDialog();
        return dlg._saved ? dlg._files : null;
    }

    private void Render(int select)
    {
        _rows.Clear();
        for (int i = 0; i < _files.Count; i++)
        {
            var name = Path.GetFileName(_files[i]);
            var folder = Path.GetFileName(Path.GetDirectoryName(_files[i])) ?? "";
            _rows.Add(new Row($"{i + 1}. {name}", "In folder " + folder, $"{name}, {i + 1} of {_files.Count}"));
        }
        List.SelectedIndex = Math.Clamp(select, 0, _files.Count - 1);
        List.ScrollIntoView(List.SelectedItem);
    }

    private void Move(int from, int to)
    {
        if (from < 0) { _say.Say("Select a file first."); return; }
        if (to < 0) { _say.Say("Already first."); return; }
        if (to >= _files.Count) { _say.Say("Already last."); return; }
        var f = _files[from];
        _files.RemoveAt(from);
        _files.Insert(to, f);
        Render(to);
        FocusSelected();
        _say.Say($"Moved {Path.GetFileName(f)} to position {to + 1} of {_files.Count}.");
    }

    private void MoveToPrompt()
    {
        int from = List.SelectedIndex;
        if (from < 0) { _say.Say("Select a file first."); return; }
        var answer = Dialogs.Prompt(this, "Move to position", $"New position, 1 to {_files.Count}", (from + 1).ToString());
        if (answer is null) { FocusSelected(); return; }
        if (!int.TryParse(answer, out var v) || v < 1 || v > _files.Count)
        {
            _say.Say($"Enter a number from 1 to {_files.Count}.");
            FocusSelected();
            return;
        }
        Move(from, v - 1);
    }

    private void FocusSelected()
    {
        List.UpdateLayout();
        if (List.ItemContainerGenerator.ContainerFromIndex(Math.Max(0, List.SelectedIndex)) is ListBoxItem item) item.Focus();
        else List.Focus();
    }
}
