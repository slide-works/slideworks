# Preview the slides yourself

The images are already in this tool result. If the render URL is blocked, use those images. `images_included` says how many pictures are attached; any slide after that still needs the URL.

Render each slide in your own headless Chromium and look at the PNGs before you say the deck is done.

1. **Check for Playwright.** Run `npx -y playwright@{{version}} --version` (needs Node 18 or later). If `npx` is missing, install Node first, or use Python in a virtual environment instead: `python3 -m venv .pw && .pw/bin/pip install 'playwright=={{minor}}.*'`, then run each command below as `.pw/bin/python -m playwright …` in place of `npx -y playwright@{{version}} …`.
2. **Install the browser if it's missing:** `npx -y playwright@{{version}} install --only-shell chromium`. On Linux, if Chromium then fails to launch for missing libraries, run `npx -y playwright@{{version}} install-deps chromium` as root.
3. **Screenshot each slide** at {{width}}×{{height}}. Keep the quotes so the shell keeps the part after `#`:

```bash
{{loop}}
```

   Faster for many slides: one browser for all of them. Save as `shots.mjs`, run `npm install --no-save playwright@{{version}}` then `node shots.mjs`:

```js
import { chromium } from "playwright";
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: {{width}}, height: {{height}} } });
for (const n of [{{list}}]) {
  await page.goto("{{url}}".replace("{slide}", n));
  await page.waitForSelector("{{wait}}", { timeout: {{timeout_ms}} });
  await page.screenshot({ path: `slide-${n}.png` });
}
await browser.close();
```

4. **Look at every PNG** before you say the deck is done. A slide that couldn't be drawn shows its error as text on a blank page (for example `Deck not found` or `Rendering failed: image <id>`): report that text, or, for `Deck not found`, see step 6.
5. The page reads the token from the part after `#` when it first loads and then removes it from the address. A reload shows `Render token missing`: open the full URL again.
6. The URL expires in {{minutes}} minutes, and don't share it. An expired URL screenshots as `Deck not found`: call `preview_deck` again and take the screenshots again. A command that stops with a timeout after {{timeout_seconds}} seconds means the page never finished loading: run it once more, and if it times out again, report it.
7. With no shell, or no way to install a browser, say so to the person and give them the player link instead: {{player_url}}
