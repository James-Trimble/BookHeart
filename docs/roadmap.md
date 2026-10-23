# Roadmap

These are ideas, not promises. StoryHeart is very new, and anything here can change, move, or be dropped at any time. Nothing has a date, and the version numbers may change too. Versions keep counting up from 0.2 towards 1.0.

## Now

- **A coming soon page at storyheart.app**: the name and icon, what StoryHeart is, and how to get in touch.
- **A feedback email address**, feedback@storyheart.app.

## 0.2: updates and settings

### Next test version (0.2.0-beta.9)

- **Closing StoryHeart.** If a book is playing, StoryHeart asks before closing. A setting lets closing the window (including Alt+F4) hide StoryHeart in the notification area instead, where it keeps playing. Its icon there has Play, Pause, Open and Exit.
- **Back up a little when you resume**, a few seconds after a short pause and more after a long one, so you don't lose the thread.
- **Go to a time or percentage**: type 1:23:00 or 45% and jump there.
- **A sleep timer**, which can also stop at the end of the chapter, and fades out instead of stopping suddenly.
- **Downloads that pick up where they stopped.** If StoryHeart closes or the connection drops partway through an update, the next try carries on from there instead of starting over.
- **Careful with metered connections.** On a phone hotspot or a capped data plan, StoryHeart asks before downloading an update instead of doing it in the background.
- **Check for updates at storyheart.app.** StoryHeart asks the website for the list of versions instead of GitHub, so the place the code lives can change without breaking updates. If the website can't be reached, it asks GitHub as before. Older versions keep checking GitHub, so new versions stay published there too.
- **Send feedback** in the Help menu: a new email to feedback@storyheart.app, with your StoryHeart and Windows versions already filled in.

Then 0.2.0, the finished version of all of the above.

## 0.3: getting around

- **A feedback form inside StoryHeart**, so you don't need an email program. You choose what it's about (a problem, an idea, or something else), write your message, and add your email if you'd like a reply. Technical details (the StoryHeart and Windows versions, and the error log if there is one) are included unless you untick them. Nothing about your books is ever sent. It arrives as an email that can be answered like any other.
- **Shortcuts that work anywhere**, so you can play, pause and skip without switching to StoryHeart. Especially handy when it's hidden in the notification area.
- **Choose your own shortcuts.**
- **How much StoryHeart says**: shorter or longer announcements.
- **Search chapters by name.**
- **Watch a folder**: new books you put in your audiobooks folder appear in the library by themselves.
- **Shelves and filters**: not started, listening and finished, your own collections, and books in a series kept together.

## 0.4: the first public release

- **The full website at storyheart.app:**
  - a home page saying what StoryHeart does, with screenshots described for screen readers, and a download button;
  - downloads of the installer and portable copy, with what Windows you need;
  - the manual, made from the same file as the one that comes with StoryHeart;
  - what's new in every version;
  - the same feedback form as inside StoryHeart, for questions and for anyone who can't get StoryHeart to install or start;
  - a privacy page, in plain words;
  - the licence agreement and third-party notices;
  - a way to donate (Ko-fi or GitHub Sponsors);
  - an accessibility page: how StoryHeart and the site work with screen readers, what's been tested, and how to report problems;
  - questions and answers, like which formats work and why Audible files can't;
  - a friendly "page not found" page.
- **Changes to the privacy policy** shown when you next open StoryHeart.
- **Cover art on screen**, in the library and while a book plays.
- **Signing**, so Windows stops warning that StoryHeart is from an unknown publisher.
- **A sighted tester**, to check how StoryHeart looks and point out anything odd.
- **Decide whether the code stays public**, or moves to a private repository with only the releases public.

## 0.5: better listening

- **Skip silences**: long pauses in a recording are shortened.
- **Clearer voices**, easier to hear over background music, and a mono option for people who hear better in one ear.
- **Guess chapters** for books that have none, from the pauses between them.
- **Sound cues**, for things like reaching a bookmark or the end of a chapter.
- **Listening history and stats**, like how long you've listened this week and when you finished each book.
- **Show what you're listening to on Discord**, if you turn it on: "Listening to StoryHeart", with the book's title, or just "an audiobook" if you'd rather keep that private.

## 0.6: more books, more places

- **DAISY talking books**, the format many blind readers get books in from libraries and organisations like RNIB. (Locked books, like NLS BARD's or Audible's, can't be supported.)
- **Your place on every computer**, by keeping your library in your OneDrive or Dropbox folder.

## Later

- **Reading ebooks aloud**: EPUB books read with Windows' own voices.
- StoryHeart in more languages, starting in your Windows language.
- A version that plays in a web browser.
