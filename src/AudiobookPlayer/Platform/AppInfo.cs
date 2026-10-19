using System.IO;
using System.Reflection;

namespace AudiobookPlayer.Platform;

/// <summary>The app's name, version, and where it keeps its data.</summary>
internal static class AppInfo
{
    public static string Name { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "BookHeart";

    /// <summary>The version from Directory.Build.props, without build metadata.</summary>
    public static string Version { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>
    /// The portable download has a "BookHeart.portable" file beside the program. Then everything is kept
    /// in a Data folder next to it, so the whole app can live on a USB stick.
    /// </summary>
    public static bool IsPortable { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, Name + ".portable"));

    public static string DataDirectory { get; } = IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "Data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);

    public static string LibraryPath => Path.Combine(DataDirectory, "library.json");
    public static string ErrorLogPath => Path.Combine(DataDirectory, "error.log");

    /// <summary>Brings over the library from before the app had its name.</summary>
    public static void MigrateOldData()
    {
        if (IsPortable || File.Exists(LibraryPath)) return;
        var old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudiobookPlayer", "library.json");
        try
        {
            if (!File.Exists(old)) return;
            Directory.CreateDirectory(DataDirectory);
            File.Copy(old, LibraryPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
