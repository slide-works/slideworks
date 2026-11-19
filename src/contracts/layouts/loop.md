Surface: light. Steps: none.

## loop

3–6 steps in a cycle: the last step leads back to the first.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `steps` | list of object | yes | 3–6 items |  |
| `steps[].label` | text | yes | up to 24 chars |  |
| `steps[].text` | text | yes | up to 80 chars |  |
