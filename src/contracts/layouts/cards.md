Dark: no. Steps: one per `cards` item when `reveal` is `one-by-one`.

## cards

1–3 cards, each with an optional badge, a title, text and chips. Can reveal one card per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `cards` | list of object | yes | 1–3 items |  |
| `cards[].badge` | block `badge` | no | up to 20 chars |  |
| `cards[].title` | text | yes | up to 50 chars |  |
| `cards[].text` | text | yes | up to 140 chars |  |
| `cards[].chips` | list of block `chip` | yes | 0–4 items; each up to 24 chars | May be empty |
| `cards[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; left out, the card is plain |
| `cards[].fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
