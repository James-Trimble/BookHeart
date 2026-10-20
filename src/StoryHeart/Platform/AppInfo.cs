using System.IO;
using System.Reflection;
using StoryHeart.Core;

namespace StoryHeart.Platform;

/// <summary>The app's name, version, and where it keeps its data.</summary>
internal static class AppInfo
{
    public static string Name { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "StoryHeart";

    public static string Developer { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "James Trimble";

    public static string Copyright { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    /// <summary>The version from Directory.Build.props, without build metadata.</summary>
    public static string Version { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>
    /// The portable download has a "StoryHeart.portable" file beside the program. Then everything is kept
    /// in a Data folder next to it, so the whole app can live on a USB stick.
    /// </summary>
    public static bool IsPortable { get; } = File.Exists(PortableMarker) || File.Exists(OldPortableMarker);

    private static string PortableMarker => Path.Combine(AppContext.BaseDirectory, Name + ".portable");

    public static string DataDirectory { get; } = IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "Data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);

    public static string LibraryPath => Path.Combine(DataDirectory, "library.json");
    public static string ErrorLogPath => Path.Combine(DataDirectory, "error.log");

    /// <summary>The app was called BookHeart before StoryHeart, and AudiobookPlayer before that.</summary>
    private const string OldName = "BookHeart";
    private static string OldPortableMarker => Path.Combine(AppContext.BaseDirectory, OldName + ".portable");
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Brings over the library from when the app had an earlier name. Runs before anything is saved.</summary>
    public static void MigrateOldData()
    {
        try
        {
            if (IsPortable)
            {
                // A BookHeart portable copy updated in place keeps its Data folder; only the marker needs its new name.
                if (File.Exists(OldPortableMarker) && !File.Exists(PortableMarker)) File.Move(OldPortableMarker, PortableMarker);
                // A new portable copy unzipped next to the old one picks up its library.
                var parent = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
                if (parent is not null) OldData.CopyFolder(Path.Combine(parent, OldName, "Data"), DataDirectory);
                return;
            }

            OldData.MoveFolder(Path.Combine(LocalAppData, OldName), DataDirectory);

            var oldest = Path.Combine(LocalAppData, "AudiobookPlayer", "library.json");
            if (!File.Exists(LibraryPath) && File.Exists(oldest))
            {
                Directory.CreateDirectory(DataDirectory);
                File.Copy(oldest, LibraryPath);
            }

            // The installer moves the program out of a BookHeart folder. If something kept that folder open
            // while it was being removed, it may be left behind empty.
            var installParent = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var oldInstall = installParent is null ? null : Path.Combine(installParent, OldName);
            if (oldInstall is not null && Directory.Exists(oldInstall) && !Directory.EnumerateFileSystemEntries(oldInstall).Any())
                Directory.Delete(oldInstall);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
