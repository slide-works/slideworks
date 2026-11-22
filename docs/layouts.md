# Layouts

This page is for someone building a deck. It covers the layouts added with custom templates and the abilities they share: dark slides, emphasis, icons, steps, callouts and parts. The full slot list of each layout is in [`src/contracts/layouts.md`](../src/contracts/layouts.md) and the files it links to. Those files are generated from the code, so they are always right. This page does not repeat them.

An agent gets the same facts from `list_layouts` (the slots and their limits for each layout) and `list_blocks` (the callout, the chip and the icon names). Give `list_layouts` a `deck` to see the layouts of the template version that deck is on. Every example below is the argument object of one `add_slide` call, with the deck id left out, except the parts example, which is for `set_parts`.

Slot text is plain text. Markdown and HTML are not read. Every limit is in characters unless it says items.

## The layouts added

The built-in templates `talk` and `workshop` allow all ten. The `update` template allows `cards`, `stack`, `bars`, `flow` and `levels`. A layout outside a deck's template is still kept and drawn, and the tool answers with a warning.

| Layout | What it is for | Surface | Steps |
|---|---|---|---|
| [`statement`](../src/contracts/layouts/statement.md) | One big sentence that carries the slide, with an optional sub line | light or dark | the sub line |
| [`quote`](../src/contracts/layouts/quote.md) | A quotation and who said it, with an optional stat, follow-up and source | light or dark | the stat, then the follow-up |
| [`compare`](../src/contracts/layouts/compare.md) | Two panels side by side: the failure mode and the success mode | light | the success panel, then the stamps |
| [`links`](../src/contracts/layouts/links.md) | 1 to 6 links, with an optional QR code for one address | light | none |
| [`cards`](../src/contracts/layouts/cards.md) | 1 to 4 cards; four lay out 2×2 | light or dark | one card at a time |
| [`image`](../src/contracts/layouts/image.md) | A headline over one large image, with optional points | light | one point at a time |
| [`stack`](../src/contracts/layouts/stack.md) | 2 to 8 labelled segments that make up one whole | light | one segment at a time, last first |
| [`bars`](../src/contracts/layouts/bars.md) | 2 to 6 horizontal bars that compare named things | light | one bar at a time |
| [`flow`](../src/contracts/layouts/flow.md) | 2 to 4 nodes joined by arrows, left to right | light | one node at a time |
| [`levels`](../src/contracts/layouts/levels.md) | A ladder of levels with the current ones marked, beside points, a paragraph or an image | light | none |

`quote`, `cards` and `image` existed before. They now take the slots described below. `bullets` and `big-number` take the new abilities too.

## Light and dark slides

A layout whose surface is "either" can be drawn light or dark. The surface is not a slot. It is an argument of `add_slide` and `update_slide`: `"light"` or `"dark"`. Leave it out for the layout's own surface. A layout that cannot take the surface you ask for is refused. `update_slide` takes `null` to go back to the layout's own surface.

The layouts that take either surface are `statement`, `quote`, `bullets`, `big-number`, `cards`, `two-column` and `practice`. `list_layouts` says which one a layout is.

```json
{
  "layout": "statement",
  "surface": "dark",
  "slots": {
    "kicker": "Northwind launch",
    "statement": "Ship the smallest\nthing that works",
    "sub": "Then ship the next one."
  }
}
```

`statement` is set at 210 px. Write a line break as `\n`. It takes at most 3 lines and shrinks to 110 px to keep them. It shows no part tracker.

## Emphasis

`emphasis` draws a phrase of the slide's display text in a tone. It is a list of 1 to 3 entries. Each entry has `text` (the phrase, up to 60 characters), `tone` (`accent`, `positive` or `negative`) and, where the layout has more than one display text, `in` (which one). The phrase must occur exactly once in the text it is in, and two phrases cannot overlap. A phrase that is missing or that occurs twice is refused with its path.

Every layout in the table above has `emphasis`. The texts it can name are in each layout's file. Most name `headline`. `statement` names `statement` and `sub`. `quote` names `quote` and `headline`. `compare` names `headline`, `failure.line` and `success.line`.

```json
{
  "layout": "quote",
  "slots": {
    "quote": "We did not need a bigger plan. We needed a smaller first step.",
    "attribution": "Priya Raman",
    "role": "Head of Delivery, Northwind",
    "emphasis": [{ "text": "smaller first step", "tone": "positive" }]
  }
}
```

## Icons

An icon is one of the 48 names in [`src/contracts/icons.json`](../src/contracts/icons.json). `list_blocks` returns them. An icon goes in these places:

- `bullets`: an item's `icon`, beside its `lead` and `text`.
- `cards`: a card's `icon`. A card's `image` takes its place if you give one.
- `compare`: a panel's `icon`.
- `image`: a point's `icon`.
- `levels`: the main column's `icon` (it needs a `title`) and the `icon` of each of the main column's items.

```json
{
  "layout": "bullets",
  "slots": {
    "headline": "Three habits of a small release",
    "items": [
      { "icon": "bolt", "lead": "Fast", "text": "Ship before the plan goes stale." },
      { "icon": "check", "lead": "Checked", "text": "A person has seen it work." },
      "Reversible: one command takes it back."
    ]
  }
}
```

## Steps

A step is one reveal. With `"reveal": "one-by-one"` a layout shows its items one at a time, and the audience window and the presenter window move through them with the same keys as slides. With `"reveal": "all"`, the default, everything shows with the slide. `add_slide` answers with the number of steps the slide has.

| Layout | One step shows |
|---|---|
| `bullets` | one item |
| `cards` | one card |
| `image` | one point |
| `bars` | one bar |
| `flow` | one node |
| `stack` | one segment, starting at the bottom |
| `statement` | the sub line |
| `big-number` | the detail |
| `quote` | the stat, then the follow-up line |
| `compare` | the success panel, then the stamps |

`links` and `levels` have no steps. A callout can add one more step at the end (below).

```json
{
  "layout": "bars",
  "slots": {
    "headline": "Release time fell in every team",
    "bars": [
      { "label": "Payments", "value": 4, "was": 9, "tone": "positive" },
      { "label": "Search", "value": 6, "was": 8 },
      { "label": "Mobile", "value": 11, "was": 10, "tone": "negative", "tag": "Slower" }
    ],
    "unit": "days",
    "wasLabel": "Last quarter",
    "reveal": "one-by-one"
  }
}
```

## Callouts

A callout is one highlighted sentence under the slide's content. It has a `text` (up to 220 characters) and a `tone`: `accent`, `positive`, `negative` or `inverse`. `fill` is `soft` (tinted, the default) or `solid`. `reveal` is `with-slide` (the default) or `last`. With `last` the callout is one more step after the others.

The layouts that take a callout are `bullets`, `big-number`, `compare`, `cards`, `image`, `stack`, `bars`, `flow` and `levels`.

```json
{
  "layout": "flow",
  "slots": {
    "headline": "A release is three small moves",
    "nodes": [
      { "label": "Step 1", "title": "Merge", "text": "The change lands on main." },
      { "label": "Step 2", "title": "Ship", "text": "It goes to 5% of Northwind users.", "tone": "accent" },
      { "label": "Step 3", "title": "Widen", "text": "It goes to everyone if the dashboard is quiet." }
    ],
    "chips": ["5% first", "One command back"],
    "callout": { "tone": "positive", "text": "Each move can be undone.", "reveal": "last" },
    "reveal": "one-by-one"
  }
}
```

## Parts

A deck can have up to 8 parts. A part is a name of up to 40 characters, or an object `{name, short, caption, mark}`. `short` (up to 16 characters) is what the part tracker shows. `caption` (up to 24 characters) goes under the part's stop on the divider's journey. `mark` is 1 or 2 characters, such as `A`, and makes an unnumbered section. A part with a mark must come after every numbered part.

`create_deck` takes `parts`. `set_parts` replaces the whole list later: send every part you want to keep. Slides keep their part numbers, so a list shorter than the highest part a slide uses is refused and names those slides.

```json
{
  "parts": [
    { "name": "Why small releases", "short": "Why" },
    { "name": "How Northwind slices work", "short": "How", "caption": "Three slices" },
    { "name": "Appendix", "short": "More", "mark": "A" }
  ]
}
```

## The other new layouts

### Compare

Two panels. Each holds `points`, `rows` (a label and a value) or one large `line`: exactly one of the three. Each has a `label`, a `title`, and optionally a `stamp`, a `footer`, an `icon`, a `tone` and a `fill` (`soft`, `solid` or `plain`). The failure panel is `negative` by default and the success panel `positive`.

```json
{
  "layout": "compare",
  "slots": {
    "headline": "Big releases hide their problems",
    "failure": {
      "label": "Big release",
      "title": "Six weeks, one launch",
      "points": ["Nobody remembers what changed.", "A fault could be in any of 40 changes."],
      "stamp": "Slow"
    },
    "success": {
      "label": "Small release",
      "title": "Daily, one change",
      "rows": [
        { "label": "Size", "value": "1 change" },
        { "label": "Undo", "value": "One command" }
      ],
      "stamp": "Fast"
    },
    "callout": { "tone": "accent", "text": "The fix is smaller steps, not more care." }
  }
}
```

### Links and a QR code

A list of 1 to 6 links. Each has a `title` and a `url`, and optionally a `kind` and a `source`. A URL is an http or https address of printable ASCII, with no user name or password, up to 500 characters. `qr` adds a QR code for one address (up to 200 characters, and short ones scan best) with an optional `caption`.

A title opens its link in a new tab. Clicking it does not move the talk to the next slide.

```json
{
  "layout": "links",
  "slots": {
    "headline": "Read more",
    "links": [
      { "kind": "Article", "title": "Why Northwind ships daily", "source": "northwind.example", "url": "https://northwind.example/blog/daily" },
      { "kind": "Talk", "title": "Small releases, big calm", "url": "https://northwind.example/talks/small" }
    ],
    "qr": { "url": "https://northwind.example/small", "caption": "northwind.example/small" }
  }
}
```

### Cards

1 to 4 cards. Each has a `title` and `text`, and optionally an `icon` or an `image`, a `badge`, up to 4 `chips`, a `pin` (a short label in the corner), a `tone` and a `fill`. With `fill: "plain"` the card stays white and gets a top rule in its tone. Four cards lay out 2×2.

```json
{
  "layout": "cards",
  "surface": "dark",
  "slots": {
    "headline": "Four checks before it ships",
    "cards": [
      { "icon": "check", "title": "Tests pass", "text": "Every one, on the branch." },
      { "icon": "bolt", "title": "Fast to undo", "text": "One command takes it back.", "tone": "positive", "fill": "plain" },
      { "icon": "calendar", "title": "Not on Friday", "text": "Ship early in the week.", "pin": "House rule" },
      { "badge": "Owner", "title": "Someone is watching", "text": "A named person has the dashboard open.", "chips": ["Dashboard", "Pager"] }
    ]
  }
}
```

### Image

A headline over one large image. `frame` is `card` (a white card with a shadow, the default) or `none`. `position` is `below` (the default) or `right`. With `right` the image stands at full height and the headline must wrap to at most 3 lines in a 760 px column, so a headline of 70 characters can already be refused. `link` makes the image open an address; a YouTube or Vimeo address plays on the slide with the image as its poster. The image id is the one `upload_image` returned.

```json
{
  "layout": "image",
  "slots": {
    "headline": "The release board on a quiet day",
    "image": { "image": "img_4Kq8Zr2mTb", "alt": "A dashboard with every release marked green." },
    "position": "right",
    "points": [
      { "icon": "check", "lead": "Green", "text": "Nothing is waiting." },
      "Each card is one change."
    ],
    "caption": "Northwind release board"
  }
}
```

### Stack

2 to 8 labelled segments, listed top to bottom. Give every segment a `value` or none. With values, a segment's height follows its value. `unit` needs values. `tone` highlights a segment. `points` stand beside the stack. `side` puts the stack on the `right` (the default) or the `left`.

```json
{
  "layout": "stack",
  "slots": {
    "headline": "Where a release week goes",
    "segments": [
      { "label": "Waiting for review", "value": 18, "tone": "negative", "tag": "Biggest" },
      { "label": "Building", "value": 6 },
      { "label": "Testing", "value": 9 },
      { "label": "Writing code", "value": 7 }
    ],
    "unit": "h",
    "total": "40 hours in all",
    "points": [{ "lead": "Review", "text": "Nearly half of the week is waiting." }],
    "reveal": "one-by-one"
  }
}
```

### Bars

See the example under Steps. A bar has a `label` and a `value`, and optionally a `sub`, a `was` (a dashed outline at that length), a `note`, a `tone`, and a `tag`. `values: "hidden"` leaves the numbers off. For a quantity over time use `chart`.

### Levels

A ladder of 2 to 7 levels, top rung first, with any number marked `current`, beside one main column: `items`, `text` or `image` (exactly one). Optional `facts` (2 to 4 tiles) stand under the main column. `side` puts the ladder on the `right` (the default) or the `left`.

```json
{
  "layout": "levels",
  "slots": {
    "headline": "Where Northwind teams are on releases",
    "levels": {
      "title": "Release maturity",
      "items": [
        { "text": "Releases on demand", "mark": "4" },
        { "text": "Daily releases", "mark": "3", "state": "current" },
        { "text": "Weekly releases", "mark": "2" },
        { "text": "Quarterly releases", "mark": "1" }
      ]
    },
    "main": {
      "title": "What daily releases ask for",
      "icon": "bulb",
      "items": [
        { "lead": "Small", "text": "One change at a time." },
        { "lead": "Safe", "text": "A way back for each one." }
      ]
    },
    "facts": [
      { "label": "Teams on daily", "value": "7 of 12", "tone": "positive" },
      { "label": "Median release", "value": "1 day" }
    ]
  }
}
```

### Quote

See the example under Emphasis. A quote has the `quote` (the words only; the layout adds the quotation marks), an `attribution` and an optional `role`, `headline`, `stat` (a `value` of up to 12 characters and a `caption`), `follow_up`, `source` and a portrait `image`.

### Statement

See the example under Light and dark slides.

## Where the limits are

Every slot limit, with its unit, is in the generated file for the layout. If this page and a generated file ever differ, the generated file is right.
