Surface: light. Steps: none.

## chart

A line or bar chart of 1–3 series, with an optional marker line and caption.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `kind` | `line` or `bar` | yes |  |  |
| `xLabels` | list of text | yes | 2–12 items; each up to 12 chars |  |
| `series` | list of object | yes | 1–3 items |  |
| `series[].name` | text | yes | up to 30 chars |  |
| `series[].values` | list of number | yes | 2–12 items; each -1,000,000,000 to 1,000,000,000 | One number per xLabel |
| `yUnit` | text | no | up to 8 chars | A short unit for the y axis, such as 'wk' or '%' |
| `caption` | text | no | up to 160 chars |  |
| `marker` | object | no |  | A dashed vertical line with a label |
| `marker.x` | number | yes | 1 to 12 | 1 is the first xLabel; 3.5 sits between the third and the fourth |
| `marker.label` | text | yes | up to 20 chars |  |
