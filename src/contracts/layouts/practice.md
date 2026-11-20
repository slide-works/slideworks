Surface: either. Steps: none.

## practice

A practice: what to do, what comes back and what to avoid, with an optional callout that can arrive on the last step. density compact fits a fuller practice. Light or dark.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `density` | `regular` or `compact` | no | default `regular` | 'compact' uses smaller type so a full practice fits; 'regular' is the default |
| `do` | list of object | yes | 1–5 items |  |
| `do[].lead` | text | no | up to 50 chars | Optional bold lead-in |
| `do[].text` | text | yes | up to 120 chars |  |
| `comes_back` | list of text | no | 0–4 items; each up to 100 chars | Optional. What the team gets back |
| `avoid` | list of text | yes | 0–4 items; each up to 100 chars | What to avoid; may be empty |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
