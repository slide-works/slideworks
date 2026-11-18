Surface: light. Steps: one per `cards` item when `reveal` is `one-by-one`.

## numbered-cards

2–4 numbered cards, each a title and text, filling the slide. A card can be highlighted with a tone, a fill and a pin. Can reveal one card per step and bring the callout in last.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `cards` | list of object | yes | 2–4 items |  |
| `cards[].title` | text | yes | up to 40 chars |  |
| `cards[].text` | text | yes | up to 140 chars |  |
| `cards[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; left out, the card is plain |
| `cards[].fill` | `soft` or `solid` or `plain` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour; 'plain' leaves it on the slide's ground |
| `cards[].pin` | text | no | up to 24 chars | A short label in the card's corner, such as 'Hold on to this one' |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
