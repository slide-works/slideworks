Surface: light. Steps: none.

## practice

A practice: what to do, what comes back and what to avoid, with an optional callout.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `do` | list of object | yes | 1–5 items |  |
| `do[].lead` | text | yes | up to 50 chars |  |
| `do[].text` | text | yes | up to 120 chars |  |
| `comes_back` | list of text | yes | 0–4 items; each up to 100 chars | What the team gets back; may be empty |
| `avoid` | list of text | yes | 0–4 items; each up to 100 chars | What to avoid; may be empty |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
