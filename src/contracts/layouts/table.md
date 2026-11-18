Surface: light. Steps: one per `rows` item when `reveal` is `one-by-one`.

## table

A table of 2–6 columns and 1–6 rows. A cell can carry a tone, and one row can be highlighted. The rows can arrive one per step, or a two-column table can strike each left cell as its answer arrives.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `columns` | list of text | yes | 2–6 items; each up to 30 chars |  |
| `rows` | list of list of text or object | yes | 1–6 items; each 2–6 items; each text up to 80 chars | Each row has one cell per column |
| `rows[][].text` | text | yes | up to 80 chars |  |
| `rows[][].tone` | `good` or `warn` or `bad` or `info` | yes |  | Draws the cell as a badge in this tone |
| `highlightRow` | whole number | no | 1 to 6 | The 1-based number of the row to highlight |
| `reveal` | `all` or `one-by-one` or `strike-and-answer` | no |  | 'one-by-one' shows one row per step; 'strike-and-answer' (two columns) shows every left cell from the start and, one row per step, strikes it and shows its answer; 'all' (the default) shows everything at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
