# Web player prototype

This is the browser-based audiobook player StoryHeart started from, kept as the starting point for the StoryHeart web player.

It's a single page with no build step and no server: open `index.html` in Chrome or Edge and pick some audiobook files. Your places and bookmarks are kept in the browser.

It isn't the finished web player. It still uses its original name and doesn't share code with the Windows app yet. When the web version is built, the plan is to reuse the logic in `src/StoryHeart.Core` where it makes sense.
