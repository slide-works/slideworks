Dark: no. Steps: none.

## poll

Poll slide: a question and 2 to 8 options that the room answers on their phones while the deck is live. The poll's settings (how many picks, when results show, the correct answer, when it opens) are set with update_poll, never in the slots.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `question` | text | yes | up to 120 chars |  |
| `options` | list of text | yes | 2–8 items; each up to 60 chars | Known by position: while a round is open they can be reworded or added to, not removed or reordered. Each option needs its own text: two that match, ignoring case and spaces at the ends, are refused |
