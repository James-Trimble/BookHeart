# BookHeart

**An accessible audiobook player for Windows.**

BookHeart is made to be used from the keyboard and with a screen reader. Every action has a shortcut, it tells you what happened after each thing you do, and its lists and sliders make sense when they're read aloud. It works with NVDA, JAWS and Narrator.

It keeps a library of your audiobooks and remembers where you stopped in each one, along with your bookmarks and how fast and loud you like each book.

**[Download the latest version](https://github.com/James-Trimble/BookHeart/releases/latest)**

## What it does

- **Keeps your library.** Add a single file, a book that comes as a folder of parts, or a whole folder of books at once. Search it, sort it, and see how far through each book you are and how much is left.
- **Remembers your place** in every book, even if you close it halfway through a sentence. If you move your files, it asks where they went and keeps your place.
- **Plays at your speed.** Anywhere from half speed to three times as fast, and the narrator's voice stays at its normal pitch.
- **Makes quiet recordings louder**, up to 300% volume, without crackling on the loud parts.
- **Finds the chapters** inside `.m4b` audiobooks, or treats each file as a chapter for books that come in parts.
- **Bookmarks**, with notes if you want them.
- **Tells you where you are.** Press Alt+I to hear the chapter, your position, and how much of the book is left at your current speed.
- **Remembers settings per book.** Set a default speed and volume, and give any book its own.
- **Works with your media keys**, even when BookHeart isn't the window in front.
- **Keeps itself up to date.** It tells you when there's a new version and what's in it, and installs it if you say so.
- Light, dark and high-contrast colours.

It plays `.m4b`, `.m4a`, `.mp3`, `.aac`, `.flac`, `.wav`, `.wma` and `.ogg` files.

## Getting it

Go to the [latest release](https://github.com/James-Trimble/BookHeart/releases/latest) and download one of these:

- **BookHeart-x.y.z-Setup.exe** installs BookHeart with Start menu and desktop shortcuts, and can set it to open `.m4b` audiobooks when you double-click them. This is the one most people want.
- **BookHeart-x.y.z-Portable.zip** doesn't install anything. Unzip it anywhere, even on a USB stick, and run `BookHeart.exe`.

You need Windows 10 (version 1809 or newer) or Windows 11, 64-bit. It's free.

The first time you run it, Windows may say it "protected your PC", because BookHeart is new and not yet signed. Choose **More info**, then **Run anyway**.

## Learning to use it

The [user manual](docs/manual.md) explains everything, and it comes with BookHeart too (Help → User manual). Inside the program, press **F1** to open it, or choose Help → Keyboard shortcuts for the list of shortcuts.

The ones you'll use most:

| Keys | What it does |
| --- | --- |
| Alt+P | Play or pause |
| Alt+Left / Alt+Right | Skip back / forward |
| Ctrl+Left / Ctrl+Right | Previous / next chapter |
| Ctrl+Down / Ctrl+Up | Slower / faster |
| Alt+Down / Alt+Up | Quieter / louder |
| Alt+I | Where am I? |
| Alt+B | Add a bookmark |
| Ctrl+O | Add books |

## Privacy

BookHeart doesn't collect anything about you. Your books, places, bookmarks and settings stay on your computer. The only time it goes online is to check for updates, which means asking GitHub for the list of BookHeart versions, and you can turn that off in Settings.

## What's next

- Feedback from inside BookHeart, so you can tell me what works and what doesn't without leaving the program.
- A website, and a version that plays in your web browser.
- BookHeart in more languages.

These are plans, not promises, and they can change. The [roadmap](docs/roadmap.md) has more.

## Licence

BookHeart is free to use under its [licence agreement](legal/EULA.txt).

The source code is copyright © 2026 James Trimble, all rights reserved. It's published so people can see how BookHeart works, but you may not copy, change, share or build on it without written permission. See [LICENSE.txt](LICENSE.txt).

BookHeart uses some open-source components, which are covered by their own licences. They're listed in [legal/THIRD-PARTY-NOTICES.txt](legal/THIRD-PARTY-NOTICES.txt).

---

<details>
<summary>Building BookHeart (for the developer)</summary>

You need Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet test tests/BookHeart.Core.Tests
dotnet run --project src/BookHeart
```

`pwsh build/build.ps1` runs the tests and builds the installer and portable zip into `release/`. It uses [Inno Setup](https://jrsoftware.org/isinfo.php) for the installer. GitHub Actions runs it on every push.

Releases go out in two stages. Test versions come first, numbered like `0.2.0-beta.1`, `0.2.0-beta.2`. They're published as pre-releases, so only testers who turn on **Include test versions** in Settings are offered them. When a version is ready, the same code is released as a stable version like `0.2.0`, which everyone gets.

To make either kind: set the version in `Directory.Build.props`, write `release-notes/v<version>.md`, then run the **Release** workflow from the Actions tab with that version number. A version with a dash in it is published as a test version automatically. The **Delete release** and **Delete branch** workflows tidy up afterwards.

The code is in three parts: `src/BookHeart.Core` (the library file, finding books in folders, chapters, sorting and formatting, with no Windows code, so a web version can reuse it), `src/BookHeart` (the Windows app in C# and WPF, using NAudio for playback and SoundTouch for speed), and `tests/BookHeart.Core.Tests`. The installer script is `installer/BookHeart.iss` and the manual is `docs/manual.md`. The app's name is set in `Directory.Build.props` and `installer/BookHeart.iss`.

</details>
