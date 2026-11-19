Surface: either. Steps: 1 for the sub line when `sub` is set and `reveal` is `one-by-one`. No tracker.

## statement

One big sentence that carries the slide, light or dark, with an optional sub line. Can reveal the sub line on a step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `statement` | text | yes | up to 70 chars, line breaks allowed | Set at 210 px: use \n for a line break, at most 3 written and 3 shown lines; it shrinks to 110 px to keep them |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `statement`, `sub` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `sub` | text | no | up to 100 chars | One or two lines under the sentence |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows the sub line on a step; 'all' (the default) shows it with the slide |
