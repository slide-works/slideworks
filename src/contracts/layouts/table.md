Surface: light. Steps: none.

## table

A table of 2–6 columns and 1–6 rows. A cell can carry a tone, and one row can be highlighted.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `columns` | list of text | yes | 2–6 items; each up to 30 chars |  |
| `rows` | list of list of text or object | yes | 1–6 items; each 2–6 items; each text up to 40 chars | Each row has one cell per column |
| `rows[][].text` | text | yes | up to 40 chars |  |
| `rows[][].tone` | `good` or `warn` or `bad` or `info` | yes |  | Draws the cell as a badge in this tone |
| `highlightRow` | whole number | no | 1 to 6 | The 1-based number of the row to highlight |
