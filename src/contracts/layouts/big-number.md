Surface: either. Steps: 1 for the detail when `detail` is set and `reveal` is `one-by-one`.

## big-number

One number that carries the slide, with a caption, a detail and a source. The detail can arrive on a step, and a callout can sit under the number.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline`, `caption` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `value` | text | yes | up to 10 chars | The number with a short unit, such as '11 wks' or '42%'; the layout sets a unit smaller than the number. The number part, up to and including its last digit, is at most 6 characters |
| `caption` | text | yes | up to 120 chars |  |
| `detail` | text | no | up to 200 chars |  |
| `source` | text | no | up to 100 chars | Where the number comes from, without a 'Source:' prefix |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows the detail on a step; 'all' (the default) shows it with the slide |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
