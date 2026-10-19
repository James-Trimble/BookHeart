# Audiobook Player

A Windows audiobook player built for comfortable listening with a keyboard and a screen reader.
It's a native C# / WPF app, a native version of the original browser-based player.

## What it does

- Plays `.m4b`, `.m4a`, `.mp3`, `.aac`, `.flac`, `.wav`, `.wma`, `.ogg` (and `.opus` where Windows supports it).
- A book can be one file or a whole set of files. Sets are sorted by name with numbers read as numbers,
  and you can change the order before you start listening.
- Remembers your place and bookmarks for every book, and where its files are, so **Continue listening**
  resumes in one step. If a book's files are moved, it asks where they went and keeps your place.
- Reads chapter markers inside `.m4b`/`.m4a` files (both Nero and QuickTime formats).
- Speed from 0.75× to 3× without changing the voice's pitch.
- Volume up to 300% for quiet recordings, with a limiter so loud parts don't crackle.
- "Where am I?" tells you the chapter, position and time left at the current speed.
- Works with NVDA, JAWS and Narrator: every action is spoken, lists are navigated with the arrow keys,
  and sliders read their values in words.
- Media keys and the Windows media controls work even when the window isn't in front.
- Follows Windows light, dark and high-contrast modes.
- Open books with the buttons, by dragging files or folders onto the window, or with **Open with** in File Explorer.

### Keyboard shortcuts

| Keys | Action |
| --- | --- |
| Alt + P | Play or pause |
| Alt + , / Alt + . | Skip back / forward |
| Alt + ← / Alt + → | Previous / next chapter |
| Alt + ↑ / Alt + ↓ | Volume up / down |
| Alt + B | Add a bookmark |
| Alt + I | Where am I? |
| Alt + R | Resume the most recent book |
| Alt + O | Open a book |
| Enter / Delete | In a list: open the selected item / remove it |

## Getting the app

Every push builds the app on GitHub Actions. Open the **Actions** tab, choose the latest **Build** run,
and download **AudiobookPlayer-windows-x64** under *Artifacts*. It contains a single `AudiobookPlayer.exe`
that runs without installing anything else (Windows 10 version 1809 or later, 64-bit).

Your library is saved in `%LOCALAPPDATA%\AudiobookPlayer\library.json`.

## Building it yourself

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```
dotnet test tests/AudiobookPlayer.Core.Tests
dotnet run --project src/AudiobookPlayer
```

To make the standalone `.exe`:

```
dotnet publish src/AudiobookPlayer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Project layout

- `src/AudiobookPlayer.Core`: everything that isn't Windows-specific: the library and settings file,
  chapter reading, natural sorting, book titles and time formatting. Kept separate so a future web player
  (for example, Blazor) can reuse it.
- `src/AudiobookPlayer`: the WPF app: window, audio engine (NAudio + SoundTouch), screen reader
  announcements, Windows media controls and theming.
- `tests/AudiobookPlayer.Core.Tests`: unit tests for the core library.
