Surface: either. Steps: 1 for the stat when `stat` is set and `reveal` is `one-by-one`, then 1 for the follow-up line when `follow_up` is set and `reveal` is `one-by-one`.

## quote

A quotation with its attribution, and optionally a headline, a supporting stat, a follow-up line and a source.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | no | up to 80 chars |  |
| `quote` | text | yes | up to 280 chars | The words only; the layout adds the quotation marks |
| `attribution` | text | yes | up to 80 chars | Who said it. With a role, this is the name |
| `role` | text | no | up to 160 chars | The speaker's role, and where and when it was said |
| `stat` | object | no |  |  |
| `stat.value` | text | yes | up to 12 chars |  |
| `stat.caption` | text | yes | up to 90 chars |  |
| `follow_up` | text | no | up to 160 chars |  |
| `source` | text | no | up to 200 chars | Without a 'Source:' prefix |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows the stat, then the follow-up line, on steps; 'all' (the default) shows them with the slide |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `quote`, `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
