Dark: no. Steps: one per `items` item when `reveal` is `one-by-one`.

## bullets

A headline over 1–8 points, each plain text or a bold lead plus text. Can reveal one point per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `items` | list of text or object | yes | 1–8 items; each text up to 200 chars | Each point is a string, or {lead, text} for a bold lead-in |
| `items[].lead` | text | yes | up to 60 chars |  |
| `items[].text` | text | yes | up to 160 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
