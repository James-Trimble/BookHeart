# StoryHeart manual

StoryHeart is an audiobook player for Windows. It keeps a library of your books, remembers where you stopped in each one, and gets out of your way while you listen. It was built from the start to work well with a keyboard and a screen reader, so everything in this manual can be done without a mouse.

This manual covers version 0.2.

## Contents

1. [Installing StoryHeart](#installing-storyheart)
2. [Adding books](#adding-books)
3. [Your library](#your-library)
4. [Listening to a book](#listening-to-a-book)
5. [Chapters](#chapters)
6. [Bookmarks](#bookmarks)
7. [Speed, volume and skip amount](#speed-volume-and-skip-amount)
8. [Book settings](#book-settings)
9. [Settings](#settings)
10. [Updates](#updates)
11. [Using StoryHeart with a screen reader](#using-storyheart-with-a-screen-reader)
12. [When a book's files move](#when-a-books-files-move)
13. [Where your library is kept](#where-your-library-is-kept)
14. [Keyboard shortcuts](#keyboard-shortcuts)
15. [Problems and fixes](#problems-and-fixes)
16. [Feedback](#feedback)

## Installing StoryHeart

There are two downloads. Pick one.

**The installer** (the file ending in `Setup.exe`) is the usual choice. Run it and it will ask you:

- which language to use for the installer itself
- whether to install for just you or for everyone who uses the computer (everyone needs an administrator password)
- where to put the program, if you don't want the normal place
- whether you want a desktop shortcut
- whether `.m4b` audiobook files should open in StoryHeart when you double-click them, and whether other audio files like `.mp3` should too. The second one is off to start with, since most people want their music to keep opening in their music player.

Before it installs anything, the installer shows the StoryHeart licence agreement, and you'll need to accept it to carry on. You can read it again any time from Help → Licence agreement.

When it's done you'll find StoryHeart in the Start menu. To remove it, go to Settings, then Apps, find StoryHeart, and choose Uninstall. Your library isn't deleted when you uninstall, so if you install again later, your books and places are still there.

**The portable copy** (the file ending in `Portable.zip`) doesn't install anything. Unzip it wherever you like, even onto a USB stick, and run `StoryHeart.exe`. It keeps your library in a folder called `Data` right next to the program, so you can carry the whole thing between computers. The portable copy doesn't add Start menu entries or change which program opens your files.

You need Windows 10 (version 1809 or newer) or Windows 11, 64-bit.

### "Windows protected your PC"

The first time you run the installer or the program, Windows may show a blue box saying it doesn't recognise the app. That's because StoryHeart is new and isn't signed with a paid certificate yet, not because anything is wrong with it. Choose **More info**, then **Run anyway**. You only have to do this once.

## Adding books

StoryHeart never moves or copies your audiobook files. It just remembers where they are. Keep them wherever you like.

A book can be:

- **one file**, like an `.m4b` file, which usually holds a whole book with its chapters inside it
- **several files that play in order**, like a folder of `.mp3` files called Part 1, Part 2 and so on. StoryHeart treats each file as a chapter.

StoryHeart plays `.m4b`, `.m4a`, `.mp3`, `.aac`, `.flac`, `.wav`, `.wma` and `.ogg` files. `.opus` files work if Windows can play them (see [Problems and fixes](#problems-and-fixes)).

### Add books

Choose **Add books** on the library screen, or press **Ctrl+O**. Pick a file and it's added.

If you pick more than one file, StoryHeart asks whether they're parts of one book or separate books. Say "One book" for a book that comes as Part 1, Part 2 and so on. Say "separate books" if you've picked, say, three different `.m4b` books at once.

The parts of a book are put in order by name, and numbers are read properly, so "Part 2" comes before "Part 10". If the order is still wrong, you can fix it later in [Book settings](#book-settings).

### Add a folder

If you already have a folder full of audiobooks, choose **Add a folder** or press **Ctrl+Shift+O**, and StoryHeart will go through it and everything inside it. Here's how it decides what a book is:

- Each folder of audio files becomes one book. A folder called "Dune" with twenty `.mp3` files in it becomes a book called Dune with twenty chapters.
- Folders with names like "CD 1", "CD 2", "Disc 1" or "Part 3" are treated as pieces of the book in the folder above them, so a book split across CD folders stays together.
- A folder with several `.m4b` files in it becomes several books, one per file, because an `.m4b` is almost always a complete book.

You can choose more than one folder at once in the folder picker.

### Other ways to add books

- Drag files or folders from File Explorer onto the StoryHeart window. If what you dropped is a single book, it opens straight away.
- Double-click an `.m4b` file in File Explorer, if you told the installer to open them with StoryHeart. You can also right-click any audio file and choose **Open with**, then **StoryHeart**. The book is added to your library and starts playing.

If StoryHeart is already open, the book opens in that window rather than starting StoryHeart a second time.

## Your library

The library is the first thing you see when StoryHeart starts. Get back to it from anywhere with **Ctrl+1**, or press **Escape** while you're on the book you're listening to.

At the top is a box showing the book you listened to last, with a button to pick up where you stopped. **Alt+R** does the same thing from anywhere. While a book is open, the box shows that book instead, with a button to go back to it.

Below that are the **Add books** and **Add a folder** buttons, a search box, and a choice of how to sort the list: by when you last listened, by title, or by when you added the book.

Then comes the list of books. Use the up and down arrow keys to move through it. For each book you'll hear its title, how far through it you are, roughly how much is left, and when you last listened. Books you haven't started say "Not started" along with how long they are. Lengths can take a few seconds to show up after you add books, because StoryHeart reads them quietly in the background.

With a book selected:

- **Enter** plays it, picking up where you stopped.
- **Delete** removes it from your library.
- The **Applications key** (or **Shift+F10**) opens a menu with a few more things: Book settings, mark it as finished, start it again from the beginning, and show its files in File Explorer.

The **Play**, **Book settings** and **Remove from library** buttons under the list do the same things.

Removing a book from your library forgets your place and bookmarks in it. Your files are never deleted. StoryHeart asks before removing anything.

Typing in the search box narrows the list to books whose title or file names contain what you typed. Clear the box to see everything again.

## Listening to a book

When a book opens, you're taken to the listening screen. The **Play** button has focus, so you can just press Space or Enter to start.

From the top:

- A **Library** button to go back to the library (or press **Escape**).
- The book's title, and the chapter you're in.
- A **Book settings** button (or **Ctrl+E**).
- A large clock showing where you are and how long the book or chapter is, and roughly how much of the book is left at your current speed.
- The **position slider**. Left and right arrows move 5 seconds, Page Up and Page Down move a minute, and Home and End jump to the start and end. Because that would lose your place if you pressed one by accident, the first press only tells you what will happen, and you press it again to go. Your screen reader reads the position in words, like "12 minutes 5 seconds of 1 hour 2 minutes".
- A row of five buttons: previous chapter, skip back, play or pause, skip forward, and next chapter.
- **Where am I?** and **Add bookmark** buttons, with your current speed and volume beside them.
- Two tabs, **Chapters** and **Bookmarks**. Move between them with **Ctrl+Tab**.

You don't need the buttons once you know the shortcuts. These work from anywhere in StoryHeart:

| Keys | What it does |
| --- | --- |
| Alt+P | Play or pause |
| Alt+Left arrow | Skip back |
| Alt+Right arrow | Skip forward |
| Ctrl+Left arrow | Previous chapter |
| Ctrl+Right arrow | Next chapter |
| Alt+I | Where am I? |

**Where am I?** tells you the chapter you're in, how far into it you are, how much of the book is left at the speed you're listening at, and whether it's playing or paused.

StoryHeart saves your place every few seconds, and whenever you pause, change book or close the program. You can close it at any time without losing anything.

Playback keeps going while you look around the library. If you want it to stop, pause first.

When you reach the end of a book, StoryHeart marks it as finished. Playing it again starts it from the beginning.

## Chapters

The Chapters tab lists the book's chapters. Select one and press Enter to jump to it.

- For a book that's one `.m4b` or `.m4a` file, the chapters come from the markers inside the file, if it has any. Some files don't. Then the tab says so, and you can move around with the slider, skipping and bookmarks.
- For a book made of several files, each file is a chapter. The chapter names are the file names, with the book's title trimmed off the front so they're easier to listen to.

While the book plays, the list follows along and marks the current chapter as "playing now", unless you're moving through the list yourself.

**Previous chapter** goes to the start of the chapter you're in. If you're already within the first few seconds of it, it goes back to the one before. This is how most players work, and it means a quick double press takes you back a chapter.

## Bookmarks

Press **Alt+B** to drop a bookmark where you are. It's named after the chapter it's in.

Press **Ctrl+B** instead if you want to type a note with it, like "start of the trial" or "recipe for the stew". The note becomes the bookmark's name.

The Bookmarks tab lists them in the order they come in the book. Select one and press **Enter** to jump there, or **Delete** to remove it. The buttons under the list let you go to a bookmark, change its note, or delete it.

Each book has its own bookmarks.

## Speed, volume and skip amount

StoryHeart has default settings for speed, volume and how far the skip buttons jump. Every book uses them until you give a book its own.

- **Speed** goes from 0.5 (half speed) to 3 (three times as fast). The voice stays at its normal pitch however fast you go. **Ctrl+Up arrow** speeds up and **Ctrl+Down arrow** slows down, in small steps.
- **Volume** goes from 0 to 300 percent. Anything over 100 is a boost for quiet recordings. StoryHeart gently holds back the loudest parts so they don't crackle, but a very large boost on a noisy recording can still sound rough. **Alt+Up arrow** and **Alt+Down arrow** change it in steps of 10.
- **Skip amount** is how far Alt+Left arrow and Alt+Right arrow jump: anywhere from 5 seconds to 5 minutes.

When you change the speed or volume with the keys while a book is open, that book remembers it. So you can have a slow narrator at 1.4 and a fast one at 1.1, and each book plays at its own speed when you go back to it.

To change the defaults that every book starts with, go to [Settings](#settings).

## Book settings

Press **Ctrl+E** while listening, or choose **Book settings** from the library, to see the settings for one book. Changes take effect straight away, so you can hear them as you go.

- **Title.** StoryHeart guesses a title from the file names. If its guess is wrong, type a better one.
- **Speed, volume and skip amount** for this book. Anything you change here is kept for this book only. If a setting is different from your default, a "Use default" button appears under it to undo that.
- **Files.** Where the book's files are, how big it is and how long. For a book made of several files there's a **Change file order** button. It opens a list of the files in the order they play. Select one and use **Move up** and **Move down**, or **Ctrl+Up arrow** and **Ctrl+Down arrow**, or **Move to position** to type a number. **Save order** keeps the changes. **Show in File Explorer** opens the folder with the book's files in it.

Press **Escape** or choose **Close** when you're done.

## Settings

Open Settings from the File menu, or press **Ctrl+Comma**.

- **Default listening settings.** The speed, volume and skip amount every book uses unless it has its own. If you've given a lot of books their own settings and want to start fresh, choose **Use these for every book** and they'll all go back to the defaults.
- **Colours.** Follow Windows (light or dark, matching your Windows setting), or always light, or always dark. If you use a Windows high contrast theme, StoryHeart always uses its colours.
- **Updates.** Whether StoryHeart checks for new versions by itself, and whether to include test versions. See [Updates](#updates).
- **Starting up.** Tick "Open the book I was listening to when the app starts" if you'd like StoryHeart to open straight to your book instead of the library. It won't start playing until you press Play.

Choose **Save** to keep your changes, or **Cancel** to leave things as they were.

StoryHeart also remembers the size and position of its window, and whether it was maximised, and opens the same way next time.

## Updates

StoryHeart keeps itself up to date. When it starts, and once a day while it's open, it checks whether there's a new version. If there is, it shows the list of what's new in it and asks what you'd like to do:

- **Install now** downloads the new version, checks it arrived intact, and installs it. StoryHeart closes for a moment and opens again by itself, on the screen you were on, and tells you it has been updated.
- **Remind me later** asks again next time StoryHeart starts, or tomorrow.
- **Skip this version** stops asking about that version. You'll still be told about the one after it.

StoryHeart never interrupts you mid-listen to tell you about an update. If one turns up while a book is playing, it waits until you pause.

You can check whenever you like from **Help → Check for updates**. To stop StoryHeart checking by itself, untick "Check for updates when StoryHeart starts and once a day" in [Settings](#settings).

Checking for updates means asking GitHub, where StoryHeart's releases are kept, for the list of versions. Nothing about you or your books is sent.

If you'd like to help test new versions before they're finished, tick **Include test versions** in Settings. Test versions can have rough edges, so only do this if you don't mind that, and please say if something goes wrong.

## Using StoryHeart with a screen reader

StoryHeart works with NVDA, JAWS and Narrator. A few things worth knowing:

- StoryHeart tells your screen reader what happened after almost everything you do: "Paused at 1 hour 4 minutes", "Forward 30 seconds", "Bookmark added", and so on. The same message also appears in the line at the bottom of the window, if you want to read it again.
- The lists (books, chapters, bookmarks, and the file order list) are ordinary lists. Tab moves into a list, landing on the item that's selected, and Tab again moves out of it. Inside, use the arrow keys, Home and End, or type the first letters of a title to jump to it.
- Settings and Book settings are divided into sections, like Appearance and Updates. Your screen reader says the section's name as you move into it.
- The sliders read their value in words rather than a bare number.
- Press **Alt** or **F10** to get to the menu bar. Everything in StoryHeart is in the menus, along with its shortcut. Tab doesn't go into the menu bar; it moves between the things on the screen.
- Press **F1** at any time to open this manual. The full list of keyboard shortcuts is in the Help menu, under **Keyboard shortcuts**.

**Media keys.** The play/pause, next and previous keys on your keyboard or headphones control StoryHeart even when its window isn't in front. Next and previous move by chapter, or skip if the book has no chapters. StoryHeart also shows up in the Windows media controls (the box that appears when you change the volume), with the book and chapter that's playing.

## When a book's files move

StoryHeart remembers where each book's files are. If you move or rename them, the library tells you the book's files have moved. When you try to play it, StoryHeart asks where they are now:

- For a one-file book, find the file.
- For a book made of several files, choose the folder they're in now. StoryHeart looks through it for files with the same names.

Your place and bookmarks are kept either way.

## Where your library is kept

Everything StoryHeart remembers (your books, places, bookmarks and settings) is in one file called `library.json`.

- If you installed StoryHeart, it's in `%LOCALAPPDATA%\StoryHeart`. That's usually `C:\Users\your name\AppData\Local\StoryHeart`.
- If you're using the portable copy, it's in the `Data` folder next to `StoryHeart.exe`.

StoryHeart used to be called BookHeart. If you used BookHeart, StoryHeart moves your library from the `BookHeart` folder to the `StoryHeart` one the first time it opens, so everything carries on as before. For a portable copy, unzip StoryHeart into the same folder that holds your old `BookHeart` folder and it brings your library over by itself, or copy the old `Data` folder next to `StoryHeart.exe`.

**Help → Open data folder** takes you straight there. To back up your library, copy `library.json` somewhere safe. To move it to another computer, copy it into the same place there. If the book files are in different places on the new computer, StoryHeart will ask where they are the first time you play each one.

## Keyboard shortcuts

These work from anywhere in StoryHeart. To see them inside the program, choose **Help → Keyboard shortcuts**.

### Listening

| Keys | What it does |
| --- | --- |
| Alt+P | Play or pause |
| Alt+Left arrow | Skip back |
| Alt+Right arrow | Skip forward |
| Ctrl+Left arrow | Previous chapter |
| Ctrl+Right arrow | Next chapter |
| Ctrl+Up arrow | Faster |
| Ctrl+Down arrow | Slower |
| Alt+Up arrow | Volume up |
| Alt+Down arrow | Volume down |
| Alt+B | Add a bookmark |
| Ctrl+B | Add a bookmark with a note |
| Alt+I | Where am I? |
| Ctrl+E | Book settings |

### Getting around

| Keys | What it does |
| --- | --- |
| Ctrl+1 | Go to the library |
| Ctrl+2 | Go to the book that's open |
| Escape | From the book back to the library |
| Alt+R | Resume the book you listened to last |
| Ctrl+O | Add books |
| Ctrl+Shift+O | Add a folder of books |
| Ctrl+Comma | Settings |
| F1 | This manual |
| Alt or F10 | Menu bar |

### In lists

| Keys | What it does |
| --- | --- |
| Enter | Play the book, jump to the chapter, or go to the bookmark |
| Delete | Remove the book or bookmark |
| Applications key or Shift+F10 | More actions for the selected book |
| Ctrl+Up / Ctrl+Down arrow | Move a file up or down (in Change file order) |
| Letters | Jump to the item whose title starts with them |

## Problems and fixes

**A file won't play.** StoryHeart uses the sound decoders built into Windows. Almost every audiobook is `.m4b` or `.mp3`, and those always work. `.opus` files need the free **Web Media Extensions** from the Microsoft Store on some computers. If a file still won't play, check it plays in another program. It may be damaged or protected with copy protection (books bought from Audible in their `.aax` format won't play in StoryHeart).

**No sound.** Check the book's volume in Book settings (Ctrl+E) isn't at 0, and that Windows is sending sound to the speakers or headphones you expect. StoryHeart picks the sound device when a book opens, so if you switch to headphones partway through and the sound stays on the speakers, close StoryHeart and open it again.

**Double-clicking a file opens a different program.** Windows sometimes keeps your earlier choice of program, even after StoryHeart is installed. Right-click the file, choose **Open with**, then **Choose another app**, pick StoryHeart and tick "Always use this app".

**A book is in the wrong order.** Open Book settings (Ctrl+E) and choose **Change file order**.

**A book's title is wrong.** Type the right one in Book settings.

**Something went wrong.** If StoryHeart hits an error, it says so and writes the details to a file called `error.log` in your data folder (Help → Open data folder). If you report a problem, sending that file along helps a lot.

## Feedback

StoryHeart is new and there will be rough edges. If something doesn't work the way you'd expect, or there's something you wish it did, please say so. For now, get in touch with James Trimble directly. A feedback option inside StoryHeart, and a website, are on the way.
