Surface: light. Steps: none.

## chart

A line or bar chart of 1–3 series, with an optional marker line and caption.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `kind` | `line` or `bar` | yes |  |  |
| `xLabels` | list of text | yes | 2–12 items; each up to 12 chars |  |
| `series` | list of object | yes | 1–3 items |  |
| `series[].name` | text | yes | up to 30 chars |  |
| `series[].values` | list of number | yes | 2–12 items; each -1,000,000,000 to 1,000,000,000 | One number per xLabel |
| `yUnit` | text | no | up to 8 chars | A short unit for the y axis, such as 'wk' or '%' |
| `caption` | text | no | up to 160 chars |  |
| `callout` | block `callout` | no |  | Under the chart; the plot shrinks to make room |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
| `marker` | object | no |  | A dashed vertical line with a label |
| `marker.x` | number | yes | 1 to 12 | 1 is the first xLabel; 3.5 sits between the third and the fourth |
| `marker.label` | text | yes | up to 20 chars |  |
