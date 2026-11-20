Surface: light. Steps: one per `segments` item, last first, when `reveal` is `one-by-one`.

## stack

A stack of 2–8 labelled segments that make up one whole, with optional values, a highlighted segment and points beside it. Can reveal one segment per step, from the bottom up.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `segments` | list of object | yes | 2–8 items | Listed top to bottom |
| `segments[].label` | text | yes | up to 40 chars |  |
| `segments[].value` | number | no | 0 to 1,000,000,000 | A number above 0; a segment's height follows it. Give every segment a value or none |
| `segments[].note` | text | no | up to 40 chars |  |
| `segments[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Highlights the segment in this tone |
| `segments[].tag` | text | no | up to 12 chars | A small tag after the label |
| `unit` | text | no | up to 8 chars | The unit of the values; needs values |
| `total` | text | no | up to 30 chars | A line under the stack |
| `caption` | text | no | up to 60 chars |  |
| `points` | list of text or object | no | 1–4 items; each text up to 120 chars | Each point is a string, or {lead, text} for a bold lead-in; they stand beside the stack |
| `points[].lead` | text | yes | up to 8 chars |  |
| `points[].text` | text | yes | up to 90 chars |  |
| `side` | `right` or `left` | no | default `right` | 'right' (the default) puts the stack on the right of the points; 'left' puts it on the left |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
