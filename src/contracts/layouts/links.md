Surface: light. Steps: none.

## links

A list of 1–6 links, each a kind, a title and a source, with an optional QR code for one address. Titles open the link in the player.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `links` | list of object | yes | 1–6 items |  |
| `links[].kind` | text | no | up to 16 chars | Such as 'Talk' or 'Article' |
| `links[].title` | text | yes | up to 90 chars | Drawn as the link |
| `links[].source` | text | no | up to 100 chars |  |
| `links[].url` | link | yes | http or https address, printable ASCII, no user info, never trimmed; up to 500 chars |  |
| `qr` | object | no |  | A QR code beside the list for one address |
| `qr.url` | link | yes | http or https address, printable ASCII, no user info, never trimmed; up to 200 chars | The address the QR code opens. Short addresses scan best |
| `qr.caption` | text | no | up to 60 chars | Under the code; defaults to the address without its scheme |
