using System.IO;
using System.Windows;
using System.Windows.Threading;
using AudiobookPlayer.Platform;
using Microsoft.Win32;

namespace AudiobookPlayer;

public partial class App : Application
{
    private const string SmokeTestFlag = "--smoke-test";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool smokeTest = e.Args.Contains(SmokeTestFlag);
        DispatcherUnhandledException += (_, args) => OnCrash(args, smokeTest);

        Theme.Apply(Resources);
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(() => Theme.Apply(Resources));
        SystemParameters.StaticPropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SystemParameters.HighContrast)) Theme.Apply(Resources);
        };

        // Files passed on the command line, e.g. from "Open with" in File Explorer.
        var window = new MainWindow(e.Args.Where(a => a != SmokeTestFlag).ToArray());
        MainWindow = window;
        if (smokeTest)
        {
            // Used by the build to check the window loads; closes as soon as it has drawn once.
            window.ContentRendered += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => Shutdown(0));
        }
        window.Show();
    }

    private void OnCrash(DispatcherUnhandledExceptionEventArgs args, bool smokeTest)
    {
        var log = Path.Combine(Path.GetDirectoryName(Core.LibraryStore.DefaultPath)!, "error.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTimeOffset.Now:u}\n{args.Exception}\n\n");
        }
        catch (IOException) { }

        if (smokeTest)
        {
            Console.Error.WriteLine(args.Exception);
            Shutdown(1);
            args.Handled = true;
            return;
        }
        MessageBox.Show($"Something went wrong: {args.Exception.Message}\n\nThe details were saved to {log}.",
            "Audiobook Player", MessageBoxButton.OK, MessageBoxImage.Error);
        args.Handled = true;
    }
}
