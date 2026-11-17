Dark: no. Steps: one per `cards` item when `reveal` is `one-by-one`.

## numbered-cards

2–4 numbered cards, each a title and text. Can reveal one card per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `cards` | list of object | yes | 2–4 items |  |
| `cards[].title` | text | yes | up to 40 chars |  |
| `cards[].text` | text | yes | up to 140 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
