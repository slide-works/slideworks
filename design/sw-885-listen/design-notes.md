# SW-885 design notes: live speech V2, listening

28 Nov 2026, updated the same day for round 2 (landscape). Design PR: https://github.com/slide-works/slideworks/pull/56. The PR adds `design/sw-885-listen/` (the mockups and 8 PNGs) and a README section with D1–D27. The same HTML and PNGs are attached here.

## Files

| File | What it shows |
|---|---|
| sw-885-mockups-2026-11-28.html | The whole page. Both demos work. In section 02, "Talk fast" shows the D9 catch-up and "Hear it in this browser" uses real `speechSynthesis`. Section 05 is the interactive landscape demo. |
| sw-885-01-scope.png | What V2 ships, what V1 already has, and what is elsewhere (SW-981, SW-982, SW-944) |
| sw-885-02-demo.png | The speaker app, a listening guest, and the guest's speech queue (waiting, reading, read, skipped) |
| sw-885-03-web.png | The 9 mobile web screens: Listen, the sheet, listening, catching up, no voice, Cantonese or Mandarin, after a screen lock, a guest in the speaker's language, paused |
| sw-885-04-app.png | The 9 audience-mode screens: first open, scan, following, the Listen sheet, a better voice, the mini player, the lock screen, headphones out, signed in |
| **sw-885-05-landscape.png** | **Round 2.** The landscape demo, 14 states, implementation notes, and the D22–D27 summary |
| sw-885-06-speaker.png | The mic tip, the Microphone row (phone mic and lapel mic), and headphone counts in the Subtitles sheet and the presenter window |
| sw-885-07-flow.png | The path from speaker to ears, the guest's queue code, and the hub messages |
| sw-885-08-decisions.png | The decisions table, D10–D27 |

Some older attachment names are stale from before the renumbering. sw-885-05-speaker.png and sw-885-06-flow.png show the same content as 06-speaker and 07-flow. sw-885-07-decisions.png was overwritten with the current decisions table.

## Decisions

D1–D9 were settled before the mockups. D10–D21 came from Joel's mockup review on 28 Nov: Q1–Q8, Q11 and Q12 all went as recommended.

- **D10:** Listen sits in the subtitle strip, next to Hide. The first tap opens a sheet whose Start is the iOS gesture.
- **D11:** only translated lines are read. A guest in the speaker's language gets no Listen.
- **D12:** web holds the screen on with Wake Lock, then offers Resume and one line about the app.
- **D13:** with no voice for the language, Start is off and the guest is shown how to add one. A local TTS model is SW-982.
- **D14:** the app home puts Join first, with no sign-in. Presenting comes below it.
- **D15:** slideworks.live links open the app (app links). Web mentions the app only in the Listen sheet.
- **D16:** the app pauses when headphones come out. Web reminds the guest in the sheet.
- **D17:** the lock screen shows Now Playing: slide artwork, Pause, Jump to live.
- **D18:** a headphone count per language in Reading in.
- **D19:** the mic tip shows once per phone, then a Microphone row.
- **D20:** cloud STT is out of V2 and is now SW-981. The pre-review mockups with the cloud screens are attached there.
- **D21:** the subtitle strip keeps a fixed height. The line being read clamps to 3 lines, the next to 1, and the skip row is reserved.

## Landscape (round 2, section 05)

V2 had left out what a guest sees with the phone turned sideways. They should see the slide full screen, in their language, with the translated lines over it while they listen. Round 2 builds on three things already decided: SW-5's full-screen landscape (`GuestFollowing`, `orientation: landscape` and `max-height: 500px`), SW-883's tap overlay with the Android Full screen button (SW-883 D6), and SW-884's film captions (SW-884 D17). Joel answered Q13–Q18 on 28 Nov, all as recommended.

- **D22, captions:** over the bottom of the slide, film style, with the newest line at the bottom. While listening, the line being read has a purple edge and the next one waits under it, dimmer. The guest can move the captions into a fixed-height band below the slide, which shrinks the slide about 18%.
- **D23, Captions menu:** the overlay's CC button opens show or hide, size S/M/L, and over or below the slide. The choice is remembered on the phone; turned upright, the portrait strip (D21) comes back.
- **D24, listening rail:** a rail in the black bar beside the 16:9 slide, on the side away from the island or punch hole. It shows headphones, level bars, the language and the skipped count (D9) for 6 seconds, and turns amber when paused (D16). On a 16:9 phone with no bar, it becomes a corner pill over the slide.
- **D25:** with captions off in landscape, listening carries on and the rail shows it.
- **D26, app immersive:** only the guest screen unlocks rotation. Sideways, it goes immersive at once: no status or system bars, home indicator auto-hidden, screen kept awake. A swipe from the edge brings the bars back. The mini player folds into the rail.
- **D27, poll chip:** while a poll or question is open, a chip over the top of the slide says "Poll open · turn upright to vote".

What else the landscape mockups settle (no decision needed):

- **The overlay** (SW-883 D6) gains Listen or Stop and the CC button, beside the slide number, Live, Full screen (Android web only) and the language chip. It still hides after 3 seconds.
- **The Listen sheet** opens as a right-hand side panel in landscape, like SW-883's language list. Start is still the iOS gesture (D10).
- **Large text** shows only the line being read (up to 2 lines); S and M also show the dimmed next or previous line.
- **Mobile web holds the Wake Lock while captions show in landscape**, not only while listening, because a guest reads without touching the screen.
- **iPhone Safari keeps its bar.** It has no element full screen, so no Full screen button there.
- A guest reading in the speaker's language gets plain film captions and no rail (D11).

## Proposed for the plan to confirm

- **Guest speech queue:** only final, translated lines are queued. Each is spoken with `lang`, `voice` and `rate` set, and the next starts on `onend`. With more than 3 waiting, the older ones are dropped and the strip says how many.
- **Hub messages:** the guest sends `SetListening({ listening })`. `AudienceRoster` gains `listening` counts per language next to `reading`. Captions, language groups and translation are unchanged from SW-884.
- **Landscape on web:** caption overlay, band, rail and overlay controls are siblings of the slide in `GuestFollowing`'s landscape branch, absolutely placed so nothing reflows when a line arrives. Slide width stays `min(100cqw, 100cqh*16/9)` (less the band in Below mode). Caption size and position go in `localStorage` next to the zh-Hant voice choice.
- **Landscape in the app:** `expo-screen-orientation` unlocks rotation on the guest screen only, `expo-status-bar` hides the status bar, `expo-navigation-bar` makes Android's bars immersive, iOS uses `prefersHomeIndicatorAutoHidden`, and `expo-keep-awake` keeps the screen on while following.

## Things to check during planning (not verified yet)

- **Voice lists:** `speechSynthesis.getVoices()` fills in asynchronously on Chrome (`voiceschanged`). The no-voice check (D13) has to wait for it and run again when the sheet reopens.
- **Wake Lock** (`navigator.wakeLock`) needs a recent iOS Safari. On older browsers, D12 falls back to Resume only.
- **Background audio in the app** needs the iOS audio background mode and an Android media foreground service. Check which Expo library gives TTS that keeps playing with the screen locked, plus Now Playing controls, and whether that is system TTS or synthesised audio played as media.
- **Headphone disconnect** (D16) needs the platform's audio-route events. Check what the chosen library exposes.
- **App links** (D15) need `apple-app-site-association` and `assetlinks.json` served on slideworks.live, which SW-5 D8 owns.
- **Landscape safe areas (D24):** the island or punch hole is left or right depending on which way the phone turned. The rail goes on the other side, so read `env(safe-area-inset-left/right)` on web and the safe-area insets in the app.
- **Bar width (D24):** an iPhone 15 on its side (852×393) leaves about 76 pt each side of a 16:9 slide, less about 47 pt of safe area on the island side. Below a minimum width (for example 36 pt), use the corner pill. Check this on narrow Androids (20:9 and 21:9) too.
- **Caption contrast (D22):** the 80% black box behind each line should pass 4.5:1 on any slide colour. Check against the light and dark templates.
- **Fullscreen on iPad:** iPadOS Safari supports element full screen, so the Android Full screen button could show there too. Decide during the plan.
- **Poll chip (D27)** reads SW-6's open-poll state, which landscape already receives but hides. No new hub message is expected.
- **Mockup figures are illustrative:** the voice names (Kanya, Sin-ji, Mei-Jia, Kyoko), the reading-time estimates, and the 18% shrink for Below mode are not measured.

## Related

- SW-884: V1 subtitles. SW-885 builds on it and is blocked by it.
- SW-883: slide translations, the language chip that Listen follows, and the landscape overlay and Full screen button that D22–D27 extend.
- SW-5: the guest page's full-screen landscape (`GuestFollowing`).
- SW-981: the cloud speech-to-text engine (split out by D20).
- SW-982: a local TTS model for languages with no system voice (from D13).
- SW-944: V3, multilingual typed Q&A.
