Surface: light. Steps: none.

## timeline

2–6 events down the slide, each a short when and what happens then.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `events` | list of object | yes | 2–6 items |  |
| `events[].when` | text | yes | up to 24 chars |  |
| `events[].text` | text | yes | up to 140 chars |  |
