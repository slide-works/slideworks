Surface: light. Steps: one per `nodes` item when `reveal` is `one-by-one`.

## flow

2–4 nodes joined by arrows, left to right. A node is a card with a title; one node can hold 2–4 items and is drawn as a box of tiles. Can reveal one node per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `nodes` | list of object | yes | 2–4 items | Left to right |
| `nodes[].title` | text | yes | up to 30 chars |  |
| `nodes[].label` | text | no | up to 20 chars | A small line above the title |
| `nodes[].text` | text | no | up to 120 chars |  |
| `nodes[].items` | list of text | no | 2–4 items; each up to 28 chars | Draws the node as a box of tiles; one node only |
| `nodes[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | 'inverse' and 'accent' fill the card; 'positive' and 'negative' colour its rule |
| `caption` | text | no | up to 140 chars |  |
| `chips` | list of block `chip` | no | 1–4 items; each up to 24 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
