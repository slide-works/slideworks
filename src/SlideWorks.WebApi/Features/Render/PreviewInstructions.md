# Preview the slides yourself

Render each slide in your own headless Chromium and look at the PNGs before you say the deck is done.

1. **Check for Playwright.** Run `npx -y playwright@{{version}} --version` (needs Node 18 or later). If `npx` is missing, install Node first, or use Python instead: `pip install playwright=={{version}}`, then run the same commands as `python -m playwright …`.
2. **Install the browser if it's missing:** `npx -y playwright@{{version}} install chromium`. On Linux, if Chromium then fails to launch for missing libraries, run `npx -y playwright@{{version}} install-deps chromium` as root.
3. **Screenshot each slide** at {{width}}×{{height}}, quoting each URL so the shell keeps the part after `#`:

```bash
{{commands}}
```

4. **Look at every PNG** before you say the deck is done. If a page shows `data-error` instead of the slide, report that text.
5. These URLs expire in {{minutes}} minutes. Call `preview_deck` again for fresh ones, and don't share them.
6. With no shell, or no way to install a browser, say so to the person and give them the player link instead: {{player_url}}
