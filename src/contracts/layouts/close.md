Surface: dark. Steps: none.

## close

Closing slide (dark): a short headline of up to three written lines, with an optional sub and author.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `headline` | text | yes | up to 80 chars, line breaks allowed | Set at 112 px, about 22 characters a line: use \n for a line break, at most 3 written and 4 shown lines; it shrinks to 60 px to keep 4 lines. An emphasised phrase sits within one written line: a phrase that spans a line break is refused |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline`, `sub` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `sub` | text | no | up to 110 chars |  |
| `author` | text | no | up to 60 chars |  |
