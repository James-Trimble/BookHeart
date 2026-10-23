using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using StoryHeart.Core;

namespace StoryHeart.Platform;

/// <summary>
/// Finds, downloads and installs new versions of StoryHeart from its GitHub releases. Installed copies
/// update by running the new installer quietly; portable copies replace their own files.
/// </summary>
internal static class Updater
{
    private static readonly HttpClient Http = CreateClient();

    public static AppVersion CurrentVersion { get; } = AppVersion.TryParse(AppInfo.Version) ?? new AppVersion(0, 0, 0, "");

    /// <summary>Installed by the StoryHeart installer (it leaves an uninstaller beside the program).</summary>
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>True when this copy can update itself; otherwise the download page is offered instead.</summary>
    public static bool CanUpdateItself => IsInstalled || AppInfo.IsPortable;

    public static async Task<List<ReleaseInfo>> FetchReleasesAsync(CancellationToken ct)
    {
        using var response = await Http.GetAsync(UpdateFeed.ReleasesUrl, ct);
        response.EnsureSuccessStatusCode();
        return UpdateFeed.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>The file this copy should download: the installer, or the zip for a portable copy.</summary>
    public static ReleaseFile? FileFor(ReleaseInfo release) => AppInfo.IsPortable ? release.Portable : release.Installer;

    /// <summary>Downloads a release file and checks its SHA-256 fingerprint. Reports progress from 0 to 1.</summary>
    public static async Task<string> DownloadAsync(ReleaseFile file, IProgress<double> progress, CancellationToken ct)
    {
        var dir = Path.Combine(Path.GetTempPath(), AppInfo.Name + "-update");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, file.Name);

        using (var response = await Http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? file.Size;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long done = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        if (file.Sha256 is { Length: > 0 } expected)
        {
            await using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            if (actual != expected)
            {
                File.Delete(path);
                throw new InvalidDataException("The download was damaged or changed on the way. Nothing was installed.");
            }
        }
        return path;
    }

    /// <summary>
    /// Starts installing a downloaded update. The caller must close StoryHeart straight afterwards. The new
    /// version opens by itself when it's done, unless <paramref name="reopen"/> is false, as when an update
    /// installs because you closed StoryHeart.
    /// </summary>
    public static void StartInstall(string downloadedFile, bool reopen = true)
    {
        if (AppInfo.IsPortable) StartPortableUpdate(downloadedFile, reopen);
        else StartInstaller(downloadedFile, reopen);
    }

    private static void StartInstaller(string setup, bool reopen)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        bool allUsers = AppContext.BaseDirectory.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase);
        // The installer remembers the folder and choices from last time. Installs for everyone ask Windows for permission.
        var args = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS " + (allUsers ? "/ALLUSERS" : "/CURRENTUSER");
        // Tells the installer not to open StoryHeart when it has finished.
        if (!reopen) args += " /NOLAUNCH";
        var installer = Process.Start(new ProcessStartInfo(setup, args) { UseShellExecute = true })
                        ?? throw new InvalidOperationException("The installer didn't start.");
        if (!reopen) return;

        // A small helper waits for the installer to finish, then opens the new version.
        RunHiddenScript("reopen-after-install.ps1", """
            param([int]$InstallerId, [string]$Exe)
            Wait-Process -Id $InstallerId -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
            if (Test-Path -LiteralPath $Exe) { Start-Process -FilePath $Exe }
            """, "-InstallerId", installer.Id.ToString(), "-Exe", Path.Combine(AppContext.BaseDirectory, AppInfo.Name + ".exe"));
    }

    private static void StartPortableUpdate(string zip, bool reopen)
    {
        var work = Path.Combine(Path.GetTempPath(), AppInfo.Name + "-update", "portable");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        ZipFile.ExtractToDirectory(zip, work);
        var exe = AppInfo.Name + ".exe";
        var source = Directory.EnumerateFiles(work, exe, SearchOption.AllDirectories).FirstOrDefault()
                     ?? throw new InvalidDataException("The update didn't contain " + exe + ".");
        var sourceDir = Path.GetDirectoryName(source)!;
        var target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        // A small script waits for StoryHeart to close, copies the new files over (leaving the Data folder alone),
        // and opens the new version.
        RunHiddenScript("update-portable.ps1", """
            param([int]$ProcessId, [string]$Source, [string]$Target, [string]$Exe, [int]$Reopen)
            Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue
            for ($try = 0; $try -lt 10; $try++) {
                try {
                    Get-ChildItem -LiteralPath $Source | Where-Object Name -ne 'Data' |
                        Copy-Item -Destination $Target -Recurse -Force -ErrorAction Stop
                    break
                } catch { Start-Sleep -Milliseconds 500 }
            }
            if ($Reopen) { Start-Process -FilePath (Join-Path $Target $Exe) }
            """, "-ProcessId", Environment.ProcessId.ToString(), "-Source", sourceDir, "-Target", target, "-Exe", exe, "-Reopen", reopen ? "1" : "0");
    }

    /// <summary>Runs a short PowerShell script in the background, with no window.</summary>
    private static void RunHiddenScript(string name, string script, params string[] args)
    {
        var path = Path.Combine(Path.GetTempPath(), AppInfo.Name + "-update", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, script);
        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", path }.Concat(args))
            psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
