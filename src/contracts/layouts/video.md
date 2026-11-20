Surface: light. Steps: none.

## video

A headline over one YouTube video. The slide plays the video, and a link opens it on YouTube.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `url` | youtube | yes | one YouTube video (watch, youtu.be, embed, shorts or live), up to 500 chars | A YouTube watch, youtu.be, embed, shorts or live link. The slide plays this video |
| `start` | whole number | no | 0 to 86,400 | Seconds from the start. When set, it overrides a time in the link |
| `play` | `click` or `auto` | no | default `click` | 'click' (the default) plays when someone presses the video; 'auto' plays when the slide is shown |
| `caption` | text | no | up to 140 chars |  |
