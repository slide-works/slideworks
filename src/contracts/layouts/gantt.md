Dark: no. Steps: none.

## gantt

A timeline of 1–5 rows across 2–8 columns. Each bar has a status: done, slips, stopped or ship.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `columns` | list of text | yes | 2–8 items; each up to 16 chars |  |
| `rows` | list of object | yes | 1–5 items |  |
| `rows[].label` | text | yes | up to 30 chars |  |
| `rows[].start` | whole number | yes | 1 to 8 | The 1-based column the bar starts in |
| `rows[].end` | whole number | yes | 1 to 8 | The 1-based column the bar ends in (inclusive) |
| `rows[].status` | `done` or `slips` or `stopped` or `ship` | yes |  | 'ship' draws a ship marker instead of a bar |
| `rows[].note` | text | no | up to 40 chars | Text on the bar; defaults to the status |
| `caption` | text | no | up to 140 chars |  |
