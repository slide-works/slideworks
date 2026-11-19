Surface: light. Steps: none.

## columns-list

3–5 columns of bullets under one headline, for a checklist that will not fit a single list.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `columns` | list of object | yes | 3–5 items |  |
| `columns[].title` | text | yes | up to 40 chars |  |
| `columns[].items` | list of text | yes | 1–6 items; each up to 80 chars |  |
