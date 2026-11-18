Surface: light. Steps: 1 for the right column when `reveal` is `one-by-one`.

## two-column

A headline over two columns. Each column holds a list of points, a paragraph or an image.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `left` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image; an icon needs a title |
| `left.title` | text | no | up to 60 chars |  |
| `left.icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) | Drawn beside the title, so it needs one |
| `left.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `left.items[].icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `left.items[].lead` | text | no | up to 40 chars |  |
| `left.items[].text` | text | yes | up to 120 chars |  |
| `left.text` | text | no | up to 400 chars |  |
| `left.image` | image `{image, alt}` | no |  |  |
| `left.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `left.image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
| `right` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image; an icon needs a title |
| `right.title` | text | no | up to 60 chars |  |
| `right.icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) | Drawn beside the title, so it needs one |
| `right.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `right.items[].icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `right.items[].lead` | text | no | up to 40 chars |  |
| `right.items[].text` | text | yes | up to 120 chars |  |
| `right.text` | text | no | up to 400 chars |  |
| `right.image` | image `{image, alt}` | no |  |  |
| `right.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `right.image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
