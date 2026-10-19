# BookHeart

An audiobook player for Windows, made for listening with a keyboard and a screen reader.

## Features

- **A library of all your books.** Add single files, a book split into many files, or a whole folder of
  books at once (each subfolder becomes a book; "Disc 1" / "CD 2" folders join their book). Search and sort it,
  and see how far through each book you are and how long is left.
- **Remembers everything per book:** your place, bookmarks with notes, and where its files are. If files move,
  it asks where they went and keeps your place.
- **Listening settings:** speed from 0.5× to 3× without changing the voice's pitch, volume up to 300% with a
  limiter, and a choice of skip amounts. Set defaults for every book in **Settings**, or give a book its own in
  **Book settings**.
- **Chapters** from `.m4b`/`.m4a` chapter markers, or one chapter per file for books in parts.
- **Where am I?** tells you the chapter, position and time left at the current speed.
- **Accessible:** works with NVDA, JAWS and Narrator. Everything is announced, lists work with the arrow keys,
  sliders read their values in words, and every action has a keyboard shortcut (press **F1** for the list).
- Media keys and the Windows media controls work even when the window isn't in front.
- Light, dark and high-contrast colours.
- Opens books from File Explorer, by dragging them onto the window, or from the command line. Only one copy
  runs at a time.
- Plays `.m4b`, `.m4a`, `.mp3`, `.aac`, `.flac`, `.wav`, `.wma`, `.ogg` (and `.opus` where Windows supports it).

## Download

Get the latest version from the [Releases page](https://github.com/James-Trimble/Audiobook-Player/releases):

- **BookHeart-x.y.z-Setup.exe** installs it, with Start menu and desktop shortcuts, file associations, a choice
  of installer language and install folder, and an uninstaller.
- **BookHeart-x.y.z-Portable.zip** needs no installing. Your library is kept in a `Data` folder next to the program.

Windows 10 (version 1809) or later, 64-bit. The installed app keeps its library in `%LOCALAPPDATA%\BookHeart`.

## Building

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```
dotnet test tests/AudiobookPlayer.Core.Tests
dotnet run --project src/AudiobookPlayer
```

`pwsh build/build.ps1` runs the tests and makes the installer and portable zip in `release/` (it installs
[Inno Setup](https://jrsoftware.org/isinfo.php) with Chocolatey if needed). Every push runs this on GitHub Actions.

### Making a release

1. Set the version in `Directory.Build.props` and write `release-notes/v<version>.md`.
2. In the **Actions** tab, run the **Release** workflow with that version number. It builds, tests and publishes
   the installer and portable zip as a GitHub release.

## Project layout

- `src/AudiobookPlayer.Core`: everything that isn't Windows-specific: the library and settings file, finding
  books in folders, chapter reading, natural sorting, titles and time formatting. Kept separate so a future web
  player can reuse it.
- `src/AudiobookPlayer`: the WPF app: library and player screens, dialogs, the audio engine (NAudio +
  SoundTouch), screen reader announcements, media controls, theming and single-instance handling.
- `tests/AudiobookPlayer.Core.Tests`: unit tests for the core library.
- `installer/BookHeart.iss`: the Inno Setup installer script.
- `build/build.ps1`: builds everything for release.

The app's name is set in `Directory.Build.props` and `installer/BookHeart.iss`.
