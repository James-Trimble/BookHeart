# BookHeart

BookHeart is an audiobook player for Windows. It's built to be used from the keyboard first and to work well with screen readers: every action has a shortcut, it tells you what happened after each thing you do, and its lists and sliders make sense when they're read aloud.

It keeps a library of your books and remembers where you stopped in each one, along with your bookmarks and how fast and loud you like each book. You can listen at up to three times normal speed without the narrator sounding like a chipmunk, boost quiet recordings up to 300% volume, and jump between chapters in `.m4b` files or in books that come as a folder of `.mp3`s.

The [manual](docs/manual.md) explains everything it does.

## Download

Releases are on the [Releases page](https://github.com/James-Trimble/BookHeart/releases). Each one has two downloads:

- **BookHeart-x.y.z-Setup.exe** installs BookHeart with Start menu and desktop shortcuts, and can set it to open `.m4b` files when you double-click them.
- **BookHeart-x.y.z-Portable.zip** doesn't install anything. Unzip it anywhere and run `BookHeart.exe`. It keeps its library in a `Data` folder beside it.

It needs 64-bit Windows 10 (version 1809 or newer) or Windows 11.

## Building it

You'll need Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet test tests/BookHeart.Core.Tests
dotnet run --project src/BookHeart
```

To build what goes out in a release, run `pwsh build/build.ps1`. It runs the tests, builds the program twice (a normal folder for the installer and a single `.exe` for the portable copy), checks that both start, turns the manual into a web page, and puts the installer and portable zip in `release/`. It uses [Inno Setup](https://jrsoftware.org/isinfo.php) for the installer and installs it through Chocolatey if it isn't there.

GitHub Actions runs the same script on every push. The results are attached to each run.

## Making a release

1. Change the version in `Directory.Build.props`.
2. Write the release notes in `release-notes/v<version>.md`.
3. On GitHub, go to **Actions**, pick **Release**, choose **Run workflow** and enter the version number. It builds everything, tests it, and publishes the release.

## How the code is laid out

- `src/BookHeart.Core` holds everything that doesn't depend on Windows: the library file, working out which files make up a book, reading chapters from `.m4b` files, sorting, titles and time formatting. It's kept separate so the web player planned for later can use it.
- `src/BookHeart` is the Windows app itself, written in C# with WPF. The audio engine uses [NAudio](https://github.com/naudio/NAudio) to decode and play, and [SoundTouch](https://github.com/owoudenberg/soundtouch.net) to change speed without changing pitch.
- `tests/BookHeart.Core.Tests` has the unit tests.
- `installer/BookHeart.iss` is the installer script.
- `build/` has the build scripts.
- `docs/manual.md` is the user manual. The build turns it into `BookHeart Manual.html`, which ships with the app and opens from Help → User manual.

The app's name is set in two places: `Directory.Build.props` and `installer/BookHeart.iss`.

## Plans

- A website, with a web version of the player for people who don't want to install anything.
- Feedback from inside the app and from the website.
- Automatic updates.
- The app itself in more languages. The installer already speaks seventeen.

BookHeart is by James Trimble.
