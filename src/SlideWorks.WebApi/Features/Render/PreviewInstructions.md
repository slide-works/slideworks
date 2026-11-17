# Preview the slides yourself

Render each slide in your own headless Chromium and look at the PNGs before you say the deck is done.

1. **Check for Playwright.** Run `npx -y playwright@{{version}} --version` (needs Node 18 or later). If `npx` is missing, install Node first, or use Python in a virtual environment instead: `python3 -m venv .pw && .pw/bin/pip install 'playwright=={{minor}}.*'`, then run each command below as `.pw/bin/python -m playwright …` in place of `npx -y playwright@{{version}} …`.
2. **Install the browser if it's missing:** `npx -y playwright@{{version}} install --only-shell chromium`. Headless screenshots need only the headless shell. On Linux, if Chromium then fails to launch for missing libraries, run `npx -y playwright@{{version}} install-deps chromium` as root.
3. **Screenshot each slide** at {{width}}×{{height}}, quoting each URL so the shell keeps the part after `#`:

```bash
{{commands}}
```

4. **Look at every PNG** before you say the deck is done. A slide that couldn't be drawn shows its error as text on a blank page instead (for example `Deck not found` or `Rendering failed: image <id>`): report that text, or, for `Deck not found`, see step 5.
5. These URLs expire in {{minutes}} minutes, and don't share them. An expired URL screenshots as `Deck not found`: call `preview_deck` again for fresh URLs and take the screenshots again. A command that stops with a timeout after {{timeout_seconds}} seconds means the page never finished loading: run it once more, and if it times out again, report it.
6. With no shell, or no way to install a browser, say so to the person and give them the player link instead: {{player_url}}
