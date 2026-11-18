Surface: light. Steps: none.

## levels

A ladder of levels with the current ones highlighted, beside a column of points, a paragraph or an image, with optional fact tiles under it.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `levels` | block `levels` | yes |  | The ladder, top rung first |
| `levels.title` | text | no | up to 30 chars | A small heading over the ladder |
| `levels.items` | list of object | yes | 2–7 items | Top rung first |
| `levels.items[].text` | text | yes | up to 50 chars |  |
| `levels.items[].mark` | text | no | up to 3 chars | In the rung's circle: a number or a letter. Left out, the circle is empty |
| `levels.items[].state` | `plain` or `current` | no | default `plain` | Any number of rungs can be current |
| `main` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image; an icon needs a title |
| `main.title` | text | no | up to 60 chars |  |
| `main.icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) | Drawn beside the title, so it needs one |
| `main.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `main.items[].icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `main.items[].lead` | text | no | up to 40 chars |  |
| `main.items[].text` | text | yes | up to 120 chars |  |
| `main.text` | text | no | up to 400 chars |  |
| `main.image` | image `{image, alt}` | no |  |  |
| `main.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `main.image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
| `facts` | block `facts` | no | 2–4 items | Tiles under the main column |
| `facts[].label` | text | yes | up to 20 chars | Small capitals |
| `facts[].value` | text | yes | up to 28 chars |  |
| `facts[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours the tile's top rule |
| `side` | `right` or `left` | no | default `right` | 'right' (the default) puts the ladder on the right of the main column; 'left' puts it on the left |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
