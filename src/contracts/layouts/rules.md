Dark: no. Steps: one per `rules` item when `reveal` is `one-by-one`.

## rules

1–4 numbered rules, each a title and text. Can reveal one rule per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `rules` | list of object | yes | 1–4 items |  |
| `rules[].title` | text | yes | up to 60 chars |  |
| `rules[].text` | text | yes | up to 110 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
