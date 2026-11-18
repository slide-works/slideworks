Surface: light. Steps: one per `bars` item when `reveal` is `one-by-one`.

## bars

2–6 horizontal bars that compare named things. A bar can carry a note, a dashed outline for what it was, a tone and a tag. Can reveal one bar per step. For a quantity over time, use chart.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `bars` | list of object | yes | 2–6 items | In the order to draw them |
| `bars[].label` | text | yes | up to 30 chars |  |
| `bars[].sub` | text | no | up to 40 chars | A small line under the label |
| `bars[].value` | number | yes | 0 to 1,000,000,000 | The bar's length; decimals are allowed |
| `bars[].was` | number | no | 0 to 1,000,000,000 | A dashed outline at this length: what it was, or what was expected |
| `bars[].note` | text | no | up to 50 chars | Written inside the bar, or after it when the bar is too short |
| `bars[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Draws the bar in this tone; defaults to 'accent' |
| `bars[].tag` | text | no | up to 16 chars | A short marker after the bar's end, such as 'Bottleneck' |
| `unit` | text | no | up to 8 chars | Written after each value |
| `values` | `shown` or `hidden` | no | default `shown` | 'hidden' leaves the numbers off, for lengths that are illustrative |
| `wasLabel` | text | no | up to 24 chars | What the dashed outline means, such as 'Before'; shown as a small legend. Needs a bar with a was |
| `caption` | text | no | up to 160 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
