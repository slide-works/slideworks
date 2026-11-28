# SlideWorks store listing

The text in `store/ios` and `store/android` and the images exported from `store/assets/<set>/asset.html` are the
whole listing. `store/stage_images.py` and `store/check_listing.py` check it; `store/push_listing.sh` pushes it.

## Redrawing the images

```bash
(cd src && pnpm install --frozen-lockfile)
node store/assets/export.mjs            # every set; or name sets: node store/assets/export.mjs ios-6.9
```

Each `asset.html` draws its images from the design tokens (`_shared/common.css` copies them from
`src/SlideWorks.Client/src/theme/tokens.css`). `export.mjs` writes `export/<set>/<n>-<screen>.png`, checks each image's
exact pixel size, and copies the `app-icon` set over the app's icons and splash in `src/SlideWorks.Mobile/assets`
(no `app.json` change). Commit the PNGs.

## Sets and sizes

| Set | Size | Images |
|---|---|---|
| `ios-6.9` | 1320 x 2868 | 5 screenshots |
| `ios-6.5` | 1284 x 2778 | 5 screenshots |
| `ios-6.3` | 1206 x 2622 | 5 screenshots |
| `android-phone` | 1080 x 1920 | 5 screenshots |
| `android-icon` | 512 x 512 | Play icon |
| `android-feature` | 1024 x 500 | Play feature graphic |
| `app-icon` | 1024 x 1024 (splash 512 x 512) | iOS light, dark, tinted; Android adaptive foreground, background, monochrome; splash light, dark |

## Captions (in upload order)

1. `1-home`: Join a talk with a code. Type the six digits from the screen.
2. `2-join`: Follow the slide live. See the slide number the speaker is on.
3. `3-talks`: Your talks, one tap away. Sign in and open any of your decks.
4. `4-talk`: Your deck, live on your phone. The current slide, updated as it changes.
5. `5-sign-in`: Sign in through your browser. The hosted server, or your own.

## Limits

| Field | Limit |
|---|---|
| App Store name, subtitle | 30 |
| App Store keywords | 100 (comma separated, no spaces) |
| App Store promotional text | 170 |
| App Store description | 4000 |
| Play title | 30 |
| Play short description | 80 |
| Play full description | 4000 |
| Play changelog | 500 |

Text files have no trailing newline or space.

## Decisions

- Honest v0 copy: the app joins a talk by code, lists your decks and shows the current slide number live. The listing
  claims no subtitles, polls, Q&A or presenter controls; presenting happens in the browser.
- Categories: `PRODUCTIVITY` primary, `BUSINESS` secondary. Age rating: no mature content (`violence: NONE`).
- One language (`en-US`); `MIRROR_LOCALES` in `stage_images.py` stays empty.
- The screenshots draw Home, Join with a live slide number, Talks, Talk (dark) and Sign in, with sample data.
- The mark is a slide with a second slide behind it and a live dot, in the cobalt accent and night colours of the
  default theme. Android's adaptive icon keeps the mark inside the 66 percent safe zone; iOS icons draw it larger.
- `support_url` is the repository's issue tracker (public and always there).

## Needs a person

- `privacy_url` is `https://slideworks.app/privacy`, and no such page exists yet. Publish one, or change the URL,
  before the first App Store push.
- Approve the copy (name, subtitle, descriptions, promotional text, captions).
- Set by hand in the consoles: App Review contact and demo login, App Privacy, Data safety, content rating (see the
  spec's "Set by hand" tables). The review notes say the demo login is set by hand.
