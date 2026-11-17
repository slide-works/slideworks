## Inline blocks

| Block | Kind | Required | Limits | Description |
|---|---|---|---|---|
| `callout` | object | — |  | A highlighted sentence with a tone (accent, positive, negative or inverse) and a soft or solid fill. |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `chip` | text | — | up to 24 chars | A small rounded tag, such as a tool or a practice. |
| `badge` | text | — | up to 20 chars | A short label at the top of a card, such as a role. |
| `stamp` | text | — | up to 16 chars | A tilted stamp on the title slide, such as 'Draft'. |
| `journey` | rendered, never set by agents | — |  | The parts of the deck as a timeline on divider slides. Rendered from the deck's parts; agents never set it. |
