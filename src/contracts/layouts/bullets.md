Surface: either. Steps: one per `items` item when `reveal` is `one-by-one`.

## bullets

A headline over 1–8 points, each plain text or an optional icon, bold lead and text. Can reveal one point per step and bring the callout in last.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no |  | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `items` | list of text or object | yes | 1–8 items; each text up to 200 chars | Each point is a string, or {icon?, lead?, text} for an icon and a bold lead-in |
| `items[].icon` | icon | no |  |  |
| `items[].lead` | text | no | up to 80 chars |  |
| `items[].text` | text | yes | up to 160 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
