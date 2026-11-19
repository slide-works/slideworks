Surface: light. Steps: none.

## gantt

A timeline of 1–5 rows across 2–8 columns. Each bar has a status: done, slips, stopped or ship.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `columns` | list of text | yes | 2–8 items; each up to 16 chars |  |
| `rows` | list of object | yes | 1–5 items |  |
| `rows[].label` | text | yes | up to 30 chars |  |
| `rows[].start` | whole number | yes | 1 to 8 | The 1-based column the bar starts in |
| `rows[].end` | whole number | yes | 1 to 8 | The 1-based column the bar ends in (inclusive) |
| `rows[].status` | `done` or `slips` or `stopped` or `ship` | yes |  | 'ship' draws a ship marker instead of a bar |
| `rows[].note` | text | no | up to 40 chars | Text on the bar; defaults to the status |
| `caption` | text | no | up to 140 chars |  |
| `callout` | block `callout` | no |  | Under the timeline; the rows shrink to make room |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
