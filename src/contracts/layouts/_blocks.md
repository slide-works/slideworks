## Inline blocks

| Block | Kind | Required | Limits | Description |
|---|---|---|---|---|
| `callout` | object | — |  | A highlighted sentence with a tone (accent, positive, negative or inverse) and a soft or solid fill. |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
| `chip` | text | — | up to 24 chars | A small rounded tag, such as a tool or a practice. |
| `badge` | text | — | up to 36 chars | A short label at the top of a card, such as a role. |
| `stamp` | text | — | up to 40 chars | A tilted stamp on the title slide, such as 'Draft'. Also used on compare panels. |
| `journey` | rendered, never set by agents | — |  | The parts of the deck as a timeline on divider slides. Rendered from the deck's parts; agents never set it. |
| `levels` | object | — |  | A ladder of 2–7 levels, top to bottom, with any of them marked as current. |
| `levels.title` | text | no | up to 30 chars | A small heading over the ladder |
| `levels.items` | list of object | yes | 2–7 items | Top rung first |
| `levels.items[].text` | text | yes | up to 50 chars |  |
| `levels.items[].mark` | text | no | up to 3 chars | In the rung's circle: a number or a letter. Left out, the circle is empty |
| `levels.items[].state` | `plain` or `current` | no | default `plain` | Any number of rungs can be current |
| `facts` | list of object | — | 2–4 items | A row of 2–4 fact tiles, each a label and a value. |
| `facts[].label` | text | yes | up to 20 chars | Small capitals |
| `facts[].value` | text | yes | up to 28 chars |  |
| `facts[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours the tile's top rule |
