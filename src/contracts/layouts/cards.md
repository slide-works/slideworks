Surface: either. Steps: one per `cards` item when `reveal` is `one-by-one`.

## cards

1–4 cards, each with an optional icon and badge, a title, text, chips and a pin. Four cards lay out 2×2. A card can be plain with a top rule, and a callout can sit under the cards. Can reveal one card per step. Light or dark.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `cards` | list of object | yes | 1–4 items |  |
| `cards[].icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `cards[].badge` | block `badge` | no | up to 36 chars |  |
| `cards[].title` | text | yes | up to 70 chars |  |
| `cards[].text` | text | yes | up to 140 chars |  |
| `cards[].chips` | list of block `chip` | no | 0–4 items; each up to 24 chars | Optional. May be empty |
| `cards[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; left out, the card is plain |
| `cards[].fill` | `soft` or `solid` or `plain` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour; 'plain' keeps the card white and draws a top rule in the tone's colour |
| `cards[].pin` | text | no | up to 24 chars | A short label in the card's corner, such as 'Hold on to this one' |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
