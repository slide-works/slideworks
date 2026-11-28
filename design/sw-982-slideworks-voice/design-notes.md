# SW-982 design notes: the SlideWorks voice

28 Nov 2026. SW-982 sits under the Translations epic (SW-942) and comes after SW-885. The design PR adds `design/sw-982-slideworks-voice/`: the mockup page, 8 PNGs and these notes. ShipLean has the same HTML and PNGs attached to SW-982.

## The problem

SW-885 lets a guest listen to the translated talk through the phone's own voice. When the phone has no voice for the language, Listen can't start (SW-885 D13). Android Chrome without a Thai voice pack is the common case. SW-982 adds a fallback voice that SlideWorks runs itself, called the **SlideWorks voice**.

## Files

| File | What it shows |
|---|---|
| sw-982-mockups.html | The whole page, sections 01 to 08 |
| sw-982-01-gap.png | Where phones have no voice: the research by platform, and the two findings below |
| sw-982-02-where.png | The three places the voice could run (A the server, B the browser, C native in the app), with download, CPU and delay for each |
| sw-982-03-web.png | Mobile web, 6 states: no phone voice, both voices offered, listening with the SlideWorks voice, catching up, the voice drops mid-talk, neither voice |
| sw-982-04-app.png | The app's audience mode, 3 states: the Listen sheet with no phone voice, listening, screen locked |
| sw-982-05-admin.png | The new Voices tab on `/admin/translations`: voice engines, the voice per language, and Try it |
| sw-982-06-flow.png | How a line becomes audio, timing and Wi-Fi estimates, the proposed contracts, and what to check during planning |
| sw-982-07-models.png | Open and cloud voices per language, what was ruled out, and how to run them |
| sw-982-08-decisions.png | The decisions table, D1–D8 |

## Decisions

Joel answered Q1–Q8 on 28 Nov, all as recommended.

- **D1, where it runs:** on the SlideWorks server. There is one synthesis per line per language, and guests fetch each line's audio. A browser model costs ~60–110 MB per guest per language before the first line; 40 Thai guests would pull ~2.4 GB through the venue Wi-Fi. A native model in the app leaves mobile web guests with nothing.
- **D2, when it is offered:** it is picked for the guest when the phone has no voice for the language. When the phone has a voice, the SlideWorks voice is listed under it as a second choice. The phone's voice stays the default (SW-885 D3).
- **D3, Thai, Malay and Cantonese:** open voices are used where the licence is clean (vi, id, zh, ja, ko and the European languages). A cloud voice (Azure) behind the same seam reads th, ms and yue on our instance; for self-hosters it is optional config, as SW-981's cloud STT is. Open models with unclear provenance (VachanaTTS, KhanomTan, canto-tts-nano) can be heard in admin Try it but aren't used live until their training data is checked.
- **D4, when the server synthesises:** only while a language has at least one guest listening with the SlideWorks voice, from the next line. With nobody on it, nothing runs.
- **D5, delivery:** the caption arrives as text, as today. The guest GETs that line's audio when it is next to play, through one audio element unlocked by the Start tap. Skipped lines are never fetched.
- **D6, name:** "SlideWorks voice" in the Listen sheet.
- **D7, admin:** a read-mostly Voices tab on `/admin/translations`. It shows the voice engines from config with their health and speed, the voice per language with a sample, and Try it. Which voice serves a language stays in server config.
- **D8, failure mid-talk:** that line is skipped. After 2 misses in a row the reserved skip row says so, and listening carries on with the next line that has audio.

## Findings that affect other Features

- **SW-885 D13 can't always tell.** Android Chrome's `getVoices()` lists languages whose pack isn't installed, then reads Thai lines in an English voice without any error. The no-voice check can pass when it shouldn't. With D2, a guest who hears English on Try can switch to the SlideWorks voice in the same list.
- **SW-885 D17 (lock screen) is at risk in the app.** Developers report that `AVSpeechSynthesizer` pauses when the screen locks (Apple forum 849627). Server audio played as media avoids this. Check it on a device before SW-885 is planned.
- **No clean open voice exists for Malay**, and the Thai and Cantonese candidates don't say what they were trained on. A local model alone can't close the Thai gap that started this Feature; D3 closes it with the cloud voice.

## Proposed for the plan to confirm

These contracts come from section 06:

```text
// guest → hub: SW-885's message gains the voice
SetListening({ listening: true, voice: "server" })   // or "phone"

// guest → server, per line it is about to play
GET /api/live/{code}/speech/{lang}/{seq}
  200 audio/mpeg   ready, or synthesised now (single-flight)
  404              no voice for {lang}: skip the line
  504              slower than Speech:Timeout: skip the line

// server config, like SW-943's Translation:Models
Speech:Voices: [
  { id: "open",  adapter: "sherpa", endpoint: "http://voices:8080",
    languages: { vi: "vi_VN-vais1000-medium", id: "supertonic-3", … } },
  { id: "azure", adapter: "azure", region: "southeastasia",
    languages: { th: "th-TH-PremwadeeNeural", ms: "ms-MY-YasminNeural", yue: "zh-HK-HiuMaanNeural" } } ]
```

- **Synthesis starts when a line is translated**, not when a guest asks for it, which hides most of the delay. A GET for a line that isn't ready yet waits on the same job.
- **The voice container runs sherpa-onnx** (Apache-2.0), which handles VITS/Piper, Supertonic, Kokoro and Matcha voices on CPU in one process. Piper's runtime and espeak-ng are GPL, so they stay in that container, reached over HTTP, and out of SlideWorks' MIT code.
- **Metrics:** for each call, the engine, voice, language, characters, synthesis time and outcome go to VictoriaMetrics, as in SW-943.

## Check during planning

- **Web playback:** play every line through one reused `<audio>` element started by the Start tap. Safari suspends Web Audio in the background, but an `<audio>` element keeps playing (WebKit 261554, 198277). Nobody knows yet whether JS can start a new line while an iPhone is locked, so test that on a device before promising screen-off listening on web. SW-885 D12's Wake Lock stays.
- **Re-arming:** iOS can leave audio "interrupted" after a call (WebKit 273511, 276016). Watch for it and show Resume, as SW-885 D12 does.
- **Format:** MP3 or AAC plays everywhere. Opus is smaller; check it in Safari before choosing it.
- **Gap data:** count, per language and platform, how often guests switch from the phone's voice to the SlideWorks voice after Try. Nobody publishes this.
- **Self-hosting:** both engines are optional. With neither, SW-885 D13 is unchanged. `docs/self-hosting.md` gets a section.
- **Model cards:** re-read each voice's licence before it ships. Piper's Thai pronunciation step (TLTK, Python) and MeloTTS still need checking for whether they fit the same container.

Voice names, sizes, latencies and costs in the mockups are illustrative or estimates; the page marks estimates with ~.
