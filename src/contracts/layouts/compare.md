Surface: light. Steps: 1 for the success panel when `reveal` is `one-by-one`, then 1 for the stamps when `failure.stamp` or `success.stamp` is set and `reveal` is `one-by-one`.

## compare

Two panels side by side: the failure mode and the success mode. A panel holds points, label-and-value rows or one large line, with an optional stamp, footer and icon. A callout can sit under the panels.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline`, `failure.line`, `success.line` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `failure` | object | yes | exactly one of `points`, `rows`, `line` |  |
| `failure.label` | text | yes | up to 24 chars | The panel's tag, such as 'Failure mode' |
| `failure.title` | text | yes | up to 50 chars |  |
| `failure.sub` | text | no | up to 100 chars |  |
| `failure.points` | list of text | no | 1–4 items; each up to 90 chars |  |
| `failure.rows` | list of object | no | 1–4 items |  |
| `failure.rows[].label` | text | yes | up to 16 chars |  |
| `failure.rows[].value` | text | yes | up to 40 chars |  |
| `failure.line` | text | no | up to 100 chars | One large line, such as a quoted sentence. Write the quotation marks yourself |
| `failure.stamp` | block `stamp` | no | up to 40 chars |  |
| `failure.footer` | text | no | up to 90 chars |  |
| `failure.tone` | `accent` or `positive` or `negative` or `inverse` | no | default `negative`; aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; defaults to 'negative' |
| `failure.fill` | `soft` or `solid` or `plain` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour; 'plain' leaves it on the slide's ground |
| `failure.icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `success` | object | yes | exactly one of `points`, `rows`, `line` |  |
| `success.label` | text | yes | up to 24 chars | The panel's tag, such as 'Failure mode' |
| `success.title` | text | yes | up to 50 chars |  |
| `success.sub` | text | no | up to 100 chars |  |
| `success.points` | list of text | no | 1–4 items; each up to 90 chars |  |
| `success.rows` | list of object | no | 1–4 items |  |
| `success.rows[].label` | text | yes | up to 16 chars |  |
| `success.rows[].value` | text | yes | up to 40 chars |  |
| `success.line` | text | no | up to 100 chars | One large line, such as a quoted sentence. Write the quotation marks yourself |
| `success.stamp` | block `stamp` | no | up to 40 chars |  |
| `success.footer` | text | no | up to 90 chars |  |
| `success.tone` | `accent` or `positive` or `negative` or `inverse` | no | default `positive`; aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; defaults to 'positive' |
| `success.fill` | `soft` or `solid` or `plain` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour; 'plain' leaves it on the slide's ground |
| `success.icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
