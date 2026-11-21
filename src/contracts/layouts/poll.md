Surface: light. Steps: none.

## poll

Poll slide: a question the room answers on their phones while the deck is live. A choice poll has 2 to 8 options. An open question (poll.answers "text" or "cloud" on add_slide) has none: send an empty options list. Settings (how many picks, when results show, the correct answer, when it opens, text or cloud) go on add_slide's poll object, or later with update_poll. They are never slots.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `question` | text | yes | up to 120 chars |  |
| `options` | list of text | yes | 0–8 items; each up to 60 chars | 2 to 8 for a choice poll. [] for an open question (poll.answers text or cloud). Known by position: while a round is open they can be reworded or added to, not removed or reordered. Each option needs its own text: two that match, ignoring case and spaces at the ends, are refused |
