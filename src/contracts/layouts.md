# Layouts and inline blocks

<!-- Generated from LayoutCatalogue (src/SlideWorks.Core/Slides). Do not edit by hand. To rewrite it:
     UPDATE_LAYOUTS_DOC=1 dotnet test src/SlideWorks.UnitTest --filter "FullyQualifiedName~LayoutsDoc" -->

A slide is a layout plus slots (K2). Slot text is plain text: Markdown and HTML are not interpreted,
and line breaks are allowed only where a slot says so. Every slide also has `part` (1-based, or null)
and `notes`, which are set next to the slots, not inside them. A JSON null counts as an absent slot.
Error paths name the failing field with 0-based indexes, such as `practice.do[2].lead: required`.

| Layout | Dark | Steps | Description |
|---|---|---|---|
| [`title`](#title) | yes | none | Opening slide (dark): the deck's title with a kicker, subtitle, tagline, byline and an optional stamp. |
| [`divider`](#divider) | yes | none | Section divider (dark) that opens a part. Its number and the journey come from the deck's parts and the slide's part. |
| [`bullets`](#bullets) | no | one per `items` item when `reveal` is `one-by-one` | A headline over 1–8 points, each plain text or a bold lead plus text. Can reveal one point per step. |
| [`two-column`](#two-column) | no | none | A headline over two columns. Each column holds a list of points, a paragraph or an image. |
| [`practice`](#practice) | no | none | A practice: what to do, what comes back and what to avoid, with an optional callout. |
| [`compare`](#compare) | no | none | Two panels side by side: the failure mode and the success mode. |
| [`big-number`](#big-number) | no | none | One number that carries the slide, with a caption, a detail and a source. |
| [`quote`](#quote) | no | none | A quotation with its attribution. |
| [`image`](#image) | no | none | A headline over one large image, with optional points beside it and a caption. |
| [`table`](#table) | no | none | A table of 2–6 columns and 1–6 rows. A cell can carry a tone, and one row can be highlighted. |
| [`chart`](#chart) | no | none | A line or bar chart of 1–3 series, with an optional marker line and caption. |
| [`gantt`](#gantt) | no | none | A timeline of 1–5 rows across 2–8 columns. Each bar has a status: done, slips, stopped or ship. |
| [`numbered-cards`](#numbered-cards) | no | one per `cards` item when `reveal` is `one-by-one` | 2–4 numbered cards, each a title and text. Can reveal one card per step. |
| [`rules`](#rules) | no | one per `rules` item when `reveal` is `one-by-one` | 1–4 numbered rules, each a title and text. Can reveal one rule per step. |
| [`cards`](#cards) | no | one per `cards` item when `reveal` is `one-by-one` | 1–3 cards, each with an optional badge, a title, text and chips. Can reveal one card per step. |
| [`close`](#close) | yes | none | Closing slide (dark): a short headline of up to three written lines, with an optional sub and author. |
| [`join`](#join) | yes | none | Join slide (dark): a QR code, the join code and the address, so the room can follow the talk on their phones. The code and the QR come from the live session, never from the slots. |
| [`poll`](#poll) | no | none | Poll slide: a question and 2 to 8 options that the room answers on their phones while the deck is live. The poll's settings (how many picks, when results show, the correct answer, when it opens) are set with update_poll, never in the slots. |
| [`qa`](#qa) | no | none | Q&A slide: the room's questions, most upvoted first, while the deck is live, and one question at full size when the presenter shows it. The questions come from the live session, never from the slots; the Q&A settings are set with update_qa. |

## title

Opening slide (dark): the deck's title with a kicker, subtitle, tagline, byline and an optional stamp.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `title` | text | yes | up to 34 chars | Set at 168 px: two lines at most, shrinking to 96 px to keep them |
| `subtitle` | text | no | up to 100 chars |  |
| `tagline` | text | no | up to 90 chars |  |
| `author` | text | no | up to 60 chars | The presenter's name |
| `affiliation` | text | no | up to 60 chars | The presenter's team or organisation |
| `stamp` | block `stamp` | no | up to 16 chars |  |

## divider

Section divider (dark) that opens a part. Its number and the journey come from the deck's parts and the slide's part.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `title` | text | yes | up to 44 chars | Set at 104 px: two lines at most, shrinking to 64 px to keep them |
| `sub` | text | no | up to 80 chars |  |

## bullets

A headline over 1–8 points, each plain text or a bold lead plus text. Can reveal one point per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `items` | list of text or object | yes | 1–8 items; each text up to 200 chars | Each point is a string, or {lead, text} for a bold lead-in |
| `items[].lead` | text | yes | up to 60 chars |  |
| `items[].text` | text | yes | up to 160 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |

## two-column

A headline over two columns. Each column holds a list of points, a paragraph or an image.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `left` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image |
| `left.title` | text | no | up to 50 chars |  |
| `left.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `left.items[].lead` | text | yes | up to 40 chars |  |
| `left.items[].text` | text | yes | up to 120 chars |  |
| `left.text` | text | no | up to 400 chars |  |
| `left.image` | image `{image, alt}` | no |  |  |
| `left.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `left.image.alt` | text | yes | up to 200 chars | What the image shows, for screen readers |
| `right` | object | yes | exactly one of `items`, `text`, `image` | Exactly one of items, text or image |
| `right.title` | text | no | up to 50 chars |  |
| `right.items` | list of text or object | no | 1–6 items; each text up to 120 chars |  |
| `right.items[].lead` | text | yes | up to 40 chars |  |
| `right.items[].text` | text | yes | up to 120 chars |  |
| `right.text` | text | no | up to 400 chars |  |
| `right.image` | image `{image, alt}` | no |  |  |
| `right.image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `right.image.alt` | text | yes | up to 200 chars | What the image shows, for screen readers |

## practice

A practice: what to do, what comes back and what to avoid, with an optional callout.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `do` | list of object | yes | 1–5 items |  |
| `do[].lead` | text | yes | up to 50 chars |  |
| `do[].text` | text | yes | up to 120 chars |  |
| `comes_back` | list of text | yes | 0–4 items; each up to 100 chars | What the team gets back; may be empty |
| `avoid` | list of text | yes | 0–4 items; each up to 100 chars | What to avoid; may be empty |
| `callout` | block `callout` | no |  |  |
| `callout.tone` | `accent` or `positive` or `negative` or `inverse` | yes | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette |
| `callout.fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `callout.text` | text | yes | up to 160 chars |  |

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

## big-number

One number that carries the slide, with a caption, a detail and a source.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `value` | text | yes | up to 6 chars | The number with a short unit, such as '11 wks' or '42%'; the layout sets a unit smaller than the number |
| `caption` | text | yes | up to 120 chars |  |
| `detail` | text | no | up to 200 chars |  |
| `source` | text | no | up to 100 chars | Where the number comes from, without a 'Source:' prefix |

## quote

A quotation with its attribution.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `quote` | text | yes | up to 280 chars | The words only; the layout adds the quotation marks |
| `attribution` | text | yes | up to 80 chars |  |

## image

A headline over one large image, with optional points beside it and a caption.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `image` | image `{image, alt}` | yes |  |  |
| `image.image` | text | yes | up to 64 chars | The image id that upload_image returned |
| `image.alt` | text | yes | up to 200 chars | What the image shows, for screen readers |
| `points` | list of text | no | 1–4 items; each up to 80 chars |  |
| `caption` | text | no | up to 140 chars |  |

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

## numbered-cards

2–4 numbered cards, each a title and text. Can reveal one card per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `cards` | list of object | yes | 2–4 items |  |
| `cards[].title` | text | yes | up to 40 chars |  |
| `cards[].text` | text | yes | up to 140 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |

## rules

1–4 numbered rules, each a title and text. Can reveal one rule per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `rules` | list of object | yes | 1–4 items |  |
| `rules[].title` | text | yes | up to 60 chars |  |
| `rules[].text` | text | yes | up to 110 chars |  |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |

## cards

1–3 cards, each with an optional badge, a title, text and chips. Can reveal one card per step.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars |  |
| `headline` | text | yes | up to 80 chars |  |
| `cards` | list of object | yes | 1–3 items |  |
| `cards[].badge` | block `badge` | no | up to 20 chars |  |
| `cards[].title` | text | yes | up to 50 chars |  |
| `cards[].text` | text | yes | up to 140 chars |  |
| `cards[].chips` | list of block `chip` | yes | 0–4 items; each up to 24 chars | May be empty |
| `cards[].tone` | `accent` or `positive` or `negative` or `inverse` | no | aliases `good` = `positive`, `bad` = `negative`, `info` = `accent` | Colours from the deck's palette; left out, the card is plain |
| `cards[].fill` | `soft` or `solid` | no | default `soft` | 'soft' (the default) tints it; 'solid' fills it with the tone's colour |
| `reveal` | `all` or `one-by-one` | no |  | 'one-by-one' shows one item per step; 'all' (the default) shows them all at once |

## close

Closing slide (dark): a short headline of up to three written lines, with an optional sub and author.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `headline` | text | yes | up to 80 chars, line breaks allowed | Set at 112 px, about 22 characters a line: use \n for a line break, at most 3 written and 4 shown lines; it shrinks to 60 px to keep 4 lines |
| `sub` | text | no | up to 110 chars |  |
| `author` | text | no | up to 60 chars |  |

## join

Join slide (dark): a QR code, the join code and the address, so the room can follow the talk on their phones. The code and the QR come from the live session, never from the slots.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | up to 40 chars | Defaults to 'Join on your phone' |
| `count` | `shown` or `hidden` | no | default `shown` | 'shown' (the default) shows how many have joined; 'hidden' leaves the count off |

## poll

Poll slide: a question and 2 to 8 options that the room answers on their phones while the deck is live. The poll's settings (how many picks, when results show, the correct answer, when it opens) are set with update_poll, never in the slots.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `question` | text | yes | up to 120 chars |  |
| `options` | list of text | yes | 2–8 items; each up to 60 chars | Known by position: while a round is open they can be reworded or added to, not removed or reordered |

## qa

Q&A slide: the room's questions, most upvoted first, while the deck is live, and one question at full size when the presenter shows it. The questions come from the live session, never from the slots; the Q&A settings are set with update_qa.

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `title` | text | no | up to 80 chars | Defaults to 'Questions from the room' |

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
