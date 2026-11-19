Surface: light. Steps: one per `points` item when `reveal` is `one-by-one`.

## image

A headline over one large image, with optional points beside it and a caption. The image can go without its card, sit on the right at full height, and the points can arrive one per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 60 chars |  |
| `headline` | text | yes | up to 100 chars |  |
| `emphasis` | emphasis | no | 1–3 entries, each `text` (up to 60 chars; the phrase must occur exactly once in its target), `tone` and optional `in`; targets `headline` | Phrases of the slide's display text drawn in a tone. Each must occur exactly once in the text it is in |
| `image` | image `{image, alt}` | yes |  |  |
| `image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `image.alt` | text | yes | up to 500 chars | What the image shows, for screen readers |
| `frame` | `card` or `none` | no | default `card` | 'card' (the default) puts the image on a white card with a shadow; 'none' draws it as it is, for a transparent image |
| `position` | `below` or `right` | no | default `below` | 'below' (the default) puts the image under the headline; 'right' puts it beside the headline at full height |
| `points` | list of text or object | no | 1–4 items; each text up to 80 chars | Each point is a string, or {icon?, lead?, text} for an icon and a bold lead-in |
| `points[].icon` | icon | no | one of the 48 icon names in icons.json (list_blocks returns them) |  |
| `points[].lead` | text | no | up to 30 chars |  |
| `points[].text` | text | yes | up to 80 chars |  |
| `caption` | text | no | up to 140 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 220 chars |  |
| `callout.reveal` | `with-slide` or `last` | no | default `with-slide` | 'with-slide' (the default) shows it with the slide; 'last' shows it as one more step after the others |
