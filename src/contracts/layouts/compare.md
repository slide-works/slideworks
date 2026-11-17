Surface: light. Steps: none.

## compare

Two panels side by side: the failure mode and the success mode.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `failure` | object | yes |  |  |
| `failure.label` | text | yes | up to 24 chars | The panel's tag, such as 'Failure mode' |
| `failure.title` | text | yes | up to 50 chars |  |
| `failure.sub` | text | no | up to 100 chars |  |
| `failure.points` | list of text | yes | 1–4 items; each up to 90 chars |  |
| `failure.tone` | `accent` or `positive` or `negative` or `inverse` | no | default `negative`; aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; defaults to 'negative' |
| `failure.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `success` | object | yes |  |  |
| `success.label` | text | yes | up to 24 chars | The panel's tag, such as 'Failure mode' |
| `success.title` | text | yes | up to 50 chars |  |
| `success.sub` | text | no | up to 100 chars |  |
| `success.points` | list of text | yes | 1–4 items; each up to 90 chars |  |
| `success.tone` | `accent` or `positive` or `negative` or `inverse` | no | default `positive`; aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; defaults to 'positive' |
| `success.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
