using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StoryHeart.Core;
using StoryHeart.Platform;
using Microsoft.Win32;

namespace StoryHeart;

public partial class App : Application
{
    private const string SmokeTestFlag = "--smoke-test";
    private SingleInstance? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool smokeTest = e.Args.Contains(SmokeTestFlag);
        var files = e.Args.Where(a => a != SmokeTestFlag).ToArray();
        DispatcherUnhandledException += (_, args) => OnCrash(args, smokeTest);

        if (!smokeTest)
        {
            // If the app is already open, hand it the files (or just bring it forward) and quit.
            _instance = new SingleInstance();
            if (!_instance.IsFirst && _instance.SendToFirst(files))
            {
                Shutdown(0);
                return;
            }
        }

        EventManager.RegisterClassHandler(typeof(ListBox), UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(FocusSelectedItemOnEntry));
        AppInfo.MigrateOldData();
        var store = LibraryStore.Load(AppInfo.LibraryPath);
        Theme.Apply(Resources, store.Settings.Theme);
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(() => Theme.Apply(Resources, store.Settings.Theme));
        SystemParameters.StaticPropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SystemParameters.HighContrast)) Theme.Apply(Resources, store.Settings.Theme);
        };

        // Files passed on the command line, e.g. from "Open with" in File Explorer.
        var window = new MainWindow(store, files);
        MainWindow = window;
        _instance?.Listen(paths => Dispatcher.BeginInvoke(() => window.ReceiveFiles(paths)));
        if (smokeTest)
        {
            // Used by the build to check the window loads; closes as soon as it has drawn once.
            window.ContentRendered += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => Shutdown(0));
        }
        window.Show();
    }

    /// <summary>
    /// Tabbing into a list lands on its selected item, not on the first item or the last one you were on.
    /// </summary>
    private static void FocusSelectedItemOnEntry(object sender, KeyboardFocusChangedEventArgs e)
    {
        var list = (ListBox)sender;
        // Only when arriving from outside the list with the keyboard; a mouse click selects what was clicked.
        if (InputManager.Current.MostRecentInputDevice is not KeyboardDevice) return;
        if (e.OldFocus is DependencyObject old && (old == list || list.IsAncestorOf(old))) return;
        if (list.SelectedItem is null || IsOnSelectedItem(list, e.NewFocus)) return;
        list.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!list.IsKeyboardFocusWithin || list.SelectedItem is null || IsOnSelectedItem(list, Keyboard.FocusedElement)) return;
            list.ScrollIntoView(list.SelectedItem);
            list.UpdateLayout();
            (list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) as ListBoxItem)?.Focus();
        });

        static bool IsOnSelectedItem(ListBox list, IInputElement? focused) =>
            focused is DependencyObject d && ItemsControl.ContainerFromElement(list, d) is ListBoxItem { IsSelected: true };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnCrash(DispatcherUnhandledExceptionEventArgs args, bool smokeTest)
    {
        var log = AppInfo.ErrorLogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTimeOffset.Now:u} {AppInfo.Name} {AppInfo.Version}\n{args.Exception}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        args.Handled = true;
        if (smokeTest)
        {
            Shutdown(1);
            return;
        }
        MessageBox.Show($"Something went wrong: {args.Exception.Message}\n\nThe details were saved to {log}.",
            AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
