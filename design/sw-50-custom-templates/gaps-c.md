# SW-50 layout gaps, group C: diagram building blocks (SW-369)

Build spec. The mockups are `sw-50-gaps-c.html` in this folder, with PNGs in `png/sw-50-gaps-c-*.png`. Design only: nothing here is built.

Checked against `main` as the branch `design/sw-50-gaps-c` was cut. Pixel figures are on the 1920 × 1080 canvas and are the ones the mockup page draws with.

## 1. What was decided

Nine slides of the 44-slide test deck went in as pictures. Four of them become built-in layouts. Five become custom layouts in the deck's own template, and three of those five reuse two new built-in blocks.

This story adds:

- four layouts: `stack`, `bars`, `flow`, `levels`
- two inline blocks: `levels` and `facts`

It changes no existing layout. `chart` and `gantt` stay as they are.

Recorded on SW-50 as D29–D38 (17 Nov 2026), standing as recommended unless changed:

- **D29** the per-diagram verdict (section 2)
- **D30** four layouts and two blocks; `chart` and `gantt` unchanged
- **D31** only `levels` and `facts` can be placed in a custom layout
- **D32** `bars` is a new layout, not a kind of `chart`
- **D33** no built-in gauge; the dial is a custom layout
- **D34** the horizon is a custom layout; `gantt` is not widened
- **D35** a stack reveals from the bottom up
- **D36** the ladder has its own layout, `levels`, and is not a `two-column` choice
- **D37** a monospace font token for custom layouts (finding F2)
- **D38** what the custom-layout wave needs beyond D15's bindings (findings F1, F3, F4)

## 2. Verdicts

The test for each diagram: would a deck about something else want this shape? If yes, it is built in. If the shape only tells this deck's story, it is a custom layout.

| Slide | What it shows | Structure | Verdict | Reason |
|---|---|---|---|---|
| 7 | What a seat costs | A column of six labelled segments | Built-in `stack` | Parts of a whole: budgets, time, cost and layer diagrams all want it |
| 14 | The same stack with one segment added, tagged "New", and two figures beside it | Stack, one toned segment, side points | Built-in `stack` | The same layout with `tone`, `tag` and `points` |
| 16 | Writing the code against checking it | Two horizontal bars, a dashed outline, a marker | Built-in `bars` | A few named things compared by length. `chart` cannot carry long labels, text in a bar or an outline |
| 18 | People, factory, software | Node, box of four items, node, with arrows | Built-in `flow` | Steps joined by arrows are the most common diagram |
| 42 | Prototype to owned product | Three cards with arrows, pills under them | Built-in `flow` | Not a picture, but rebuilt as numbered cards without its arrows. Same layout, with `caption` and `chips` |
| 9 | Code editor with a ghost suggestion, three fact tiles, a six-level ladder | Mock-up + tiles + ladder | Custom layout `code-mock`, holding the built-in `levels` and `facts` blocks | The mock-up is this deck's own prop. The ladder and the tiles are generic |
| 11 | Chat mock-up, tiles, ladder | Same frame | Custom layout `chat-mock`, with `levels` and `facts` | As slide 9 |
| 13 | Agent progress lanes, ladder with two levels highlighted | Meters + ladder | Custom layout `agent-lanes`, with `levels` | As slide 9 |
| 24 | Lights on to lights out | Half-circle dial with a needle | Custom layout `dial` | One number between two named ends. `big-number` already shows one number, and a built-in gauge invites decoration over data |
| 34 | Forecast horizon | Two rows of segments over a time axis | Custom layout `horizon` | Close to `gantt`, but a row holds several segments with their own fills and the axis is uneven. Not worth widening `gantt` for one slide |

Until custom layouts ship (the last wave), slides 9, 11 and 13 use the built-in `levels` layout with the mock-up as an uploaded image, so the ladder and tiles are already real text. Slides 24 and 34 stay pictures until then.

### What `chart` and `gantt` already cover

- `chart` (`kind: bar`): 1–3 series over 2–12 x labels of 12 characters, vertical bars, a marker line. It is for a quantity over time or categories. It stays unchanged and gains no new `kind`.
- `gantt`: one bar per row between whole columns, with a status. It stays unchanged. Slide 34 does not fit it and is a custom layout.
- `big-number`: one value. It is why there is no built-in gauge.

## 3. Shared rules for the four layouts

- Each has `kicker` (text, optional, 40) and `headline` (text, required, 80), drawn by `SlideHeading`, like every content layout.
- `Dark` is `false` in `LayoutDefinition`. **Hook, group B (SW-359):** all four must work on the slide-level dark surface. They use only tokens that have a night counterpart; the mapping the mockups assume is in section 9.
- **Hook, group A (SW-357):** all four take the `callout` slot wherever group A puts it. The fit rules below add `CalloutHeight` to the column the callout sits in. If group A makes the callout a field of every content layout through a shared helper, these four use it and declare nothing themselves.
- **Hook, group A (SW-361):** `flow` nodes and `facts` tiles take an optional `icon` once group A defines the type. It is not in the schemas below.
- **Hook, group A (SW-364):** `stack`, `bars` and `flow` have `reveal` (`all` or `one-by-one`) and a `stepsFrom` list, the same as `bullets`. If group A adds a per-item step, these lists take it.
- **Hook, group A (SW-360):** inline emphasis applies to headlines and captions here as anywhere.
- Tones reuse `LayoutCatalogue.Tone(null)` (`accent`, `positive`, `negative`, `inverse`, with the three aliases).
- Every diagram is real DOM text. Each figure also gets one sentence for screen readers from a `describe…` helper, as `describeChart` does.
- No motion (D17). Bars do not grow and segments do not slide in.

## 4. `stack`

A column of labelled segments that make up one whole, with points beside it.

**Description for agents:** "A stack of 2–8 labelled segments that make up one whole, with optional values, a highlighted segment and points beside it. Can reveal one segment per step, from the bottom up."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | |
| `headline` | text | yes | 80 | |
| `segments` | list of object | yes | 2–8 | Listed top to bottom |
| `segments[].label` | text | yes | 40 | |
| `segments[].value` | number | no | above 0, up to 1,000,000,000 | Sets the segment's share of the height. Give it on every segment or on none |
| `segments[].note` | text | no | 40 | A small line under the label |
| `segments[].tone` | tone enum | no | | Colours the segment and its label; left out, the segment takes its step of the ramp |
| `segments[].tag` | text | no | 12 | A small pill after the label, such as 'New' |
| `unit` | text | no | 8 | Written after each value; only with values |
| `total` | text | no | 30 | A bold line under the stack, such as 'One seat, 2026' |
| `caption` | text | no | 60 | A muted line under `total` |
| `points` | list of text or object | no | 1–4; text 120; `{lead, text}` with lead 8 and text 90 | Beside the stack. A lead is set large, for a figure such as '+5%' |
| `side` | `right` or `left` | no | default `right` | Which side the stack sits on |
| `reveal` | `all` or `one-by-one` | no | | One segment per step, last in the list first |

`stepsFrom: "segments"`.

**Geometry.** The body is a grid: text column, 60 px gap, an 820 px figure (800 px text column). The figure is a 500 px tall stack over the `total` and `caption` lines. Each segment is a row: a 300 px block, a 28 px gap, a 492 px text cell. Rows are 4 px apart and at least 44 px tall. A row's height is its content's height or its share of 500 px, whichever is larger (`flex: <value> 1 0`; equal shares without values). With no `points` and no callout the figure is 900 px wide and centred.

**Colours.** A plain segment is `color-mix(in srgb, var(--sw-accent-dark) N%, var(--sw-card))`, with N running from 22 at the top to 100 at the bottom. A toned segment is `--sw-panel-<tone>`, and its label takes the tone's text colour. The tag is `--sw-accent` on `--sw-on-accent` (the negative tone uses `--sw-panel-negative`).

**Validator rules (`LayoutRules.Stack`).**

1. `value` on every segment or on none: `stack.segments[3].value: required, because other segments have a value`.
2. `unit` without values is refused.
3. Stack fit. Row height = max(44, lines(label, plus the tag's length + 4 characters, 492 px, 26 px) × 33.8 + (note ? 26 : 0) + 10). Sum of rows + 4 × (n − 1) must be at most 500. Failing path: `stack.segments`.
4. Side fit. Each `{lead, text}` point is max(76, lines(text, 546, 30) × 40.5); a plain point is lines(text, 800, 30) × 40.5; 30 px between points; plus the callout's height at 800 px. At most `BODY_HEIGHT` (620). Failing path: `stack.points`.

**Fit fixtures.**

| Name | Holds | `grow` | `refusedBy` |
|---|---|---|---|
| `stackSeven` | 7 segments with 30-character labels, 6 of them with a 40-character note, one with a 10-character tag; `total`, `caption`; 3 `{lead, text}` points of 64 characters; a callout of 96 characters. Estimate 488 of 500 | `/segments/6/note` (add the seventh note), `/points/3` | fit |
| `stackEight` | 8 segments, one-line labels, no notes | `/segments/0/label` to two lines | fit |

Each needs the Title Case, prose and wide-letter twins the other fixtures have.

**`list_layouts` row:** `{ name: "stack", dark: false, stepsFrom: "segments", schema }`.

**Client.** `StackLayout.tsx` + `StackLayout.module.css`; `StackSegment.tsx` (one row); `describeStack.ts`. Reveal: segment `i` of `n` is shown from step `n − i`.

**Size:** M. About 60 lines of C#, 220 of TSX and CSS, 350 of tests and fixtures.

## 5. `bars`

A few named things compared by length.

**Description for agents:** "2–6 horizontal bars that compare named things. A bar can carry a note, a dashed outline for what it was, a tone and a tag. Can reveal one bar per step. For a quantity over time, use chart."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | |
| `headline` | text | yes | 80 | |
| `bars` | list of object | yes | 2–6 | In the order to draw them; the agent sorts |
| `bars[].label` | text | yes | 30 | |
| `bars[].sub` | text | no | 40 | A small line under the label |
| `bars[].value` | number | yes | 0 to 1,000,000,000 | The bar's length |
| `bars[].was` | number | no | 0 to 1,000,000,000 | A dashed outline at this length: what it was, or what was expected |
| `bars[].note` | text | no | 50 | Written inside the bar, or after it when the bar is too short |
| `bars[].tone` | tone enum | no | default `accent` | |
| `bars[].tag` | text | no | 16 | A short marker after the bar's end, such as 'Bottleneck' |
| `unit` | text | no | 8 | Written after each value |
| `values` | `shown` or `hidden` | no | default `shown` | 'hidden' leaves the numbers off, for lengths that are illustrative |
| `wasLabel` | text | no | 24 | What the dashed outline means, such as 'Before'; shown as a small legend |
| `caption` | text | no | 160 | |
| `reveal` | `all` or `one-by-one` | no | | |

`stepsFrom: "bars"`.

**Geometry.** A grid: 400 px label column, 32 px gap, 1248 px track. Bar height by count: 96 px for 2–3 bars, 80 for 4, 64 for 5, 56 for 6. Rows are 28 px apart (20 px for 5 or 6). The legend, when there is one, is a 38 px row above the bars, right-aligned. The caption is 34 px under the last bar, 24 px text.

The scale runs from 0 to the largest `value` or `was`. The track keeps room on the right for the widest end label: value text at 28 px × 0.62 em per character, plus the tag at 18 px × 0.85 em per character + 14, plus 30. The bars share what is left. A bar is at least 6 px long. A note fits inside when its width (characters × 22 px × 0.56 em + 40) is at most the bar's length; otherwise it is written 18 px after the bar or the outline, and the end label follows it. These constants live in one `geometry.ts` and are mirrored in `LayoutRules.Bars`, as the chart's are.

**Colours.** Fill `--sw-panel-<tone>` with `--sw-panel-<tone>-ink` for a note inside it. The outline is 3 px dashed `--sw-muted`. The tag is the tone's text colour (`--sw-accent`, `--sw-bad`, `--sw-good`, `--sw-ink`).

**Validator rules (`LayoutRules.Bars`).**

1. At least one `value` or `was` is above 0.
2. `wasLabel` needs at least one bar with `was`.
3. Note fit: a note that fits neither inside its bar nor after it within the track is refused: `bars.bars[1].note: too long for this bar; shorten it or leave it out`.
4. Height fit. Row = max(bar height, lines(label, 400, 30) × 36 + (sub ? lines(sub, 400, 20) × 26 + 4 : 0)). Legend 38. Caption 34 + lines(caption, 1680, 24) × 32.4. Plus the callout. Sum with the row gaps at most 620. Failing path: `bars.bars`.

**Fit fixtures.**

| Name | Holds | `grow` | `refusedBy` |
|---|---|---|---|
| `barsSix` | 6 bars: 23-character labels, 35-character subs, values in the tens of thousands with an 8-character unit, 16-character tags, four notes, a 24-character `wasLabel`, a 120-character caption. Estimate 600 of 620 | `/bars/5/label` (wraps), `/caption` (second line) | fit |
| `barsNote` | 2 bars, the short one with the longest note that still fits after it | `/bars/0/note` | fit (rule 3) |

**`list_layouts` row:** `{ name: "bars", dark: false, stepsFrom: "bars", schema }`.

**Client.** `BarsLayout.tsx` + CSS; `BarRow.tsx`; `slides/diagrams/barsGeometry.ts` (scale, reserve, note placement; unit-tested); `describeBars.ts`.

**Size:** M. About 70 lines of C#, 260 of TSX and CSS, 400 of tests and fixtures.

## 6. `flow`

Two to four nodes joined by arrows, left to right.

**Description for agents:** "2–4 nodes joined by arrows, left to right. A node is a card with a title; one node can hold 2–4 items and is drawn as a box of tiles. Can reveal one node per step."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | |
| `headline` | text | yes | 80 | |
| `nodes` | list of object | yes | 2–4 | Left to right |
| `nodes[].title` | text | yes | 30 | |
| `nodes[].label` | text | no | 20 | A small tag above the title |
| `nodes[].text` | text | no | 120 | |
| `nodes[].items` | list of text | no | 2–4; each 28 | Draws the node as a wider box of tiles. At most one node has items, and then the flow has at most three nodes |
| `nodes[].tone` | tone enum | no | | `inverse` and `accent` fill the card; `positive` and `negative` colour its rule; left out, the card is plain |
| `caption` | text | no | 140 | Under the flow |
| `chips` | list of block `chip` | no | 1–4 | After the caption |
| `reveal` | `all` or `one-by-one` | no | | An arrow arrives with the node it points to |

`stepsFrom: "nodes"`.

**Geometry.** One grid row: a column per node and a 72 px column per arrow. A plain node is 1 fr and a node with items is 2 fr. All nodes are as tall as the tallest. A card has 30 px padding and an 8 px accent rule on its left; label 16 px, title 32 px/1.2, text 24 px/1.4, 12 px between them. A group node has a 3 px accent border on `--sw-accent-light`, and its tiles are a two-column grid, 14 px apart, 22 px text in 16/18 px padding. The arrow is a 56 × 28 px stroked SVG in `--sw-muted`, `aria-hidden`. The caption and chips row is 34 px under the nodes.

**Validator rules (`LayoutRules.Flow`).**

1. At most one node has `items`: `flow.nodes[2].items: only one node can hold items`.
2. With a node that has items, at most three nodes: `flow.nodes: at most 3 nodes when one holds items`.
3. Fit. Node width w = (1680 − 72 × (n − 1)) ÷ total fr, times its fr; inner width = w − 60. Node height = 60 + (label ? 28 : 0) + lines(title, inner, 32) × 38.4 + (text ? 12 + lines(text, inner, 24) × 33.6 : 0) + (items ? 18 + rows × (32 + tile lines × 27.5) + 14 × (rows − 1) : 0), where a tile is (inner − 14) ÷ 2 − 41 px wide. Under the nodes: 34 + lines(caption, 1680, 24) × 32.4 + (chips ? 58 : 0), plus the callout. Tallest node + the rest at most 620. Failing path: `flow.nodes`.

**Fit fixtures.**

| Name | Holds | `grow` | `refusedBy` |
|---|---|---|---|
| `flowFour` | 4 nodes with a 20-character label, a 30-character title and 80 characters of text; a 140-character caption; four 24-character chips; a one-line callout | `/nodes/0/text` | fit |
| `flowGroup` | 3 nodes, the middle one with four 22-character items and 50 characters of text; caption, chips, a two-line callout | `/nodes/1/items/0` (wraps a tile) | fit |

**`list_layouts` row:** `{ name: "flow", dark: false, stepsFrom: "nodes", schema }`.

**Client.** `FlowLayout.tsx` + CSS; `FlowNode.tsx`; `FlowArrow.tsx`; `describeFlow.ts`.

**Size:** M. About 60 lines of C#, 220 of TSX and CSS, 350 of tests and fixtures.

Not included: labels on arrows, arrows that loop back, a vertical flow. No slide in the test deck needs them.

## 7. `levels` and `facts`

Two inline blocks, and one layout that hosts them.

### Block `levels`

"A ladder of 2–7 levels, top to bottom, with any of them marked as current."

| Field | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `title` | text | no | 30 | A small heading over the ladder |
| `items` | list of object | yes | 2–7 | Top rung first |
| `items[].text` | text | yes | 50 | |
| `items[].mark` | text | no | 3 | In the rung's circle: a number or a letter. Left out, the circle is empty |
| `items[].state` | `plain` or `current` | no | default `plain` | Any number of rungs can be current |

Drawn as a 480 px card with 24 px padding. A rung is a 44 px circle, a 16 px gap and 22 px/1.4 text in 10/12 px padding, at least 64 px tall, 8 px apart. A current rung has an `--sw-accent-light` ground, an `--sw-accent` circle and bold `--sw-accent-dark` text. The ladder is an `<ol>`, and a current rung carries `aria-current="true"`.

### Block `facts`

"A row of 2–4 fact tiles, each a label and a value."

A list of 2–4 objects:

| Field | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `label` | text | yes | 20 | Small capitals |
| `value` | text | yes | 28 | |
| `tone` | tone enum | no | | Colours the tile's top rule |

Tiles share the row's width equally, 20 px apart, at least 120 px tall: 18/22 px padding, a 5 px top rule, a 15 px label and a 24 px/1.25 bold value.

Both blocks join `LayoutCatalogue.Blocks`, so `list_blocks` and the "Inline blocks" table in `layouts.md` show them.

### Layout `levels`

**Description for agents:** "A ladder of levels with the current ones highlighted, beside a column of points, a paragraph or an image, with optional fact tiles under it."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | |
| `headline` | text | yes | 80 | |
| `levels` | block `levels` | yes | | |
| `main` | object | yes | exactly one of `items`, `text`, `image` | The same shape as a `two-column` column: `title` (50), `items` (1–5, text 120 or `{lead 40, text 120}`), `text` (400), `image` |
| `facts` | block `facts` | no | | Under the main column |
| `side` | `right` or `left` | no | default `right` | Which side the ladder sits on |

No `stepsFrom`: the ladder is context.

**Geometry.** A grid: main column 1144 px, 56 px gap, 480 px ladder, which sits at the top. The tiles sit at the bottom of the main column. An image fills what is left above them and scales to fit.

**Validator rules (`LayoutRules.Levels`).**

1. Ladder fit. 48 + (title ? lines(title, 432, 16, 0.85) × 19.2 + 16 : 0) + sum of rungs + 8 × (n − 1), where a rung is max(64, lines(text, 346, 22) × 30.8 + 20). At most 620. Failing path: `levels.levels.items`.
2. Main fit. As `LayoutRules.TwoColumn` at 1144 px (title 34 px, items 30 px with a 44 px indent, text 28 px), plus the tiles: 24 + 66 + (the most lines any value takes at its tile's width, 24 px) × 30, plus the callout. At most 620. An image has no text to fit. Failing path: `levels.main`.

**Fit fixtures.**

| Name | Holds | `grow` | `refusedBy` |
|---|---|---|---|
| `levelsSeven` | 7 rungs with a 2-character mark, two of them 44 characters (two lines), a 30-character ladder title; `main` with a 50-character title and three `{lead 40, text 80}` items; four tiles with 24-character values | `/levels/items/0/text` (to two lines), `/main/items/2/text` | fit |
| `levelsText` | 6 rungs; `main.text` at 400 characters; no tiles | `/main/text` | length |

**`list_layouts` row:** `{ name: "levels", dark: false, stepsFrom: null, schema }`. `list_blocks` gains `levels` and `facts`.

**Client.** `blocks/Levels.tsx` + CSS and `blocks/Facts.tsx` + CSS (take the block's value and nothing else, so a custom layout can host them); `layouts/LevelsLayout.tsx` + CSS, which reuses `TwoColumnColumn` for `main`.

**Size:** M. About 70 lines of C#, 240 of TSX and CSS, 380 of tests and fixtures.

## 8. Custom-layout verdicts: fixtures for the custom-layout wave

These five layouts belong to the deck's own template. They are not built in this story. The custom-layout wave must be able to express each one; the first two are written out in full in section 05 of the mockup page.

### `agent-lanes` (slide 13)

```json
{ "name": "agent-lanes", "surface": "either", "steps": "lanes",
  "slots": {
    "kicker":   { "type": "text", "max": 40, "optional": true },
    "headline": { "type": "text", "max": 80 },
    "lanes":    { "type": "list", "min": 2, "max": 4, "of": {
      "who":      { "type": "text", "max": 8 },
      "task":     { "type": "text", "max": 44 },
      "progress": { "type": "number", "min": 0, "max": 100 },
      "state":    { "type": "enum", "values": ["running", "waiting"] },
      "status":   { "type": "text", "max": 10 } } },
    "lead":     { "type": "text", "max": 160, "optional": true },
    "levels":   { "type": "levels" } } }
```

Bindings: `data-var="--p: progress"` on the meter (`width: calc(var(--p) * 1%)`); `data-variant="state"` gives `.is-waiting`; `data-step` on each lane; `data-slot="levels"` places the built-in block. Needs F1 and F2.

### `dial` (slide 24)

```json
{ "name": "dial", "surface": "either",
  "slots": {
    "kicker":   { "type": "text", "max": 40, "optional": true },
    "headline": { "type": "text", "max": 80 },
    "label":    { "type": "text", "max": 24, "optional": true },
    "value":    { "type": "number", "min": 0, "max": 100 },
    "from":     { "type": "text", "max": 20 },
    "to":       { "type": "text", "max": 20 },
    "text":     { "type": "text", "max": 220, "optional": true } } }
```

Bindings: `data-var="--v: value"` on the needle (`transform: rotate(calc((var(--v) - 50) * 1.8deg))`). The arc is a `conic-gradient` of tokens under a radial `mask`. Needs F4.

### `code-mock` (slide 9)

```json
{ "name": "code-mock", "surface": "either",
  "slots": {
    "kicker":   { "type": "text", "max": 40, "optional": true },
    "headline": { "type": "text", "max": 80 },
    "file":     { "type": "text", "max": 30, "optional": true },
    "lines":    { "type": "list", "min": 1, "max": 6, "of": {
      "code":  { "type": "text", "max": 56 },
      "ghost": { "type": "text", "max": 30, "optional": true } } },
    "hint":     { "type": "text", "max": 80, "optional": true },
    "facts":    { "type": "facts", "optional": true },
    "levels":   { "type": "levels" } } }
```

Bindings: none of the three; `data-if="ghost"` shows the suggestion. Needs F1 and F2. Code text keeps its leading spaces, so the layout sets `white-space: pre` on the line.

### `chat-mock` (slide 11)

```json
{ "name": "chat-mock", "surface": "either", "steps": "messages",
  "slots": {
    "kicker":   { "type": "text", "max": 40, "optional": true },
    "headline": { "type": "text", "max": 80 },
    "messages": { "type": "list", "min": 1, "max": 4, "of": {
      "from": { "type": "enum", "values": ["me", "bot"] },
      "text": { "type": "text", "max": 120 },
      "code": { "type": "text", "max": 80, "optional": true } } },
    "hint":     { "type": "text", "max": 80, "optional": true },
    "facts":    { "type": "facts", "optional": true },
    "levels":   { "type": "levels" } } }
```

Bindings: `data-variant="from"` gives `.is-me` and `.is-bot`; `data-step` on each message. Needs F1 and F2.

### `horizon` (slide 34)

```json
{ "name": "horizon", "surface": "light",
  "slots": {
    "kicker":   { "type": "text", "max": 40, "optional": true },
    "headline": { "type": "text", "max": 80 },
    "rows":     { "type": "list", "min": 1, "max": 3, "of": {
      "label": { "type": "text", "max": 20 },
      "sub":   { "type": "text", "max": 24, "optional": true },
      "segments": { "type": "list", "min": 1, "max": 4, "of": {
        "text":  { "type": "text", "max": 90 },
        "span":  { "type": "number", "min": 1, "max": 100 },
        "fill":  { "type": "enum", "values": ["solid", "hatched", "faint", "none"] } } } } },
    "ticks":    { "type": "list", "min": 2, "max": 6, "of": { "type": "text", "max": 12 } } } }
```

Bindings: `data-var="--w: span"` on each segment (`flex-grow: var(--w)`); `data-variant="fill"` gives `.is-hatched` and the others (the hatch is a `repeating-linear-gradient` of tokens); `data-step` on each row. Needs F3 and F4.

### Findings

D15's three bindings are enough for all five. No fourth binding is needed. Four other abilities are:

- **F1. Built-in blocks as slot types.** The planned slot types name the callout, chip and badge blocks. `levels` and `facts` must be on that list: a `data-slot` element whose slot is a block is filled by SlideWorks' own component.
- **F2. A monospace font token.** There is no `--sw-font-mono`, and font names are refused in a layout's CSS. Add one token, set by the template, with a system monospace stack as the built-in value. SW-50 lists this as noted but not decided.
- **F3. A list inside a list.** `data-each` inside `data-each`, with `data-var` and `data-variant` reading the inner item. The mocked `stage-ladder` has one level.
- **F4. CSS the sanitiser must let through.** `calc()` over a bound variable in `width`, `flex-grow` and `transform: rotate()`; `conic-gradient` and `repeating-linear-gradient` of tokens; `mask` with a gradient and no `url()`; `white-space: pre`.

The five schemas above are the acceptance fixtures: the custom-layout wave is done when each can be uploaded, passes its checks and renders as drawn (the two in the mockup page) or as described.

## 9. Tokens used, and what they resolve to on a dark surface

No new token. The mockups assume this mapping on dark, which group B owns:

| On light | On dark |
|---|---|
| `--sw-paper`, `--sw-card`, `--sw-chip`, `--sw-grid` | `--sw-night`; `--sw-night-ink` mixed 7% and 12% into `--sw-night`; `--sw-night-line` |
| `--sw-ink`, `--sw-muted` | `--sw-night-ink`, `--sw-night-2` |
| `--sw-accent` as text, `--sw-accent-dark` | `--sw-accent-glow` |
| `--sw-accent-light` | `--sw-accent` mixed 30% into `--sw-night` |
| stack ramp: `--sw-accent-dark` into `--sw-card` | `--sw-accent-glow` into `--sw-night` |
| `--sw-panel-inverse` with its ink | `--sw-night-ink` with `--sw-night` |

`color-mix()` of two tokens passes `slidesUseTokens.test.ts` (it bans hex, `rgb()`/`hsl()`, named colours and font names).

## 10. Tests

In the repo's order of preference.

**Acceptance (`SlideWorks.AcceptanceTests`).**

- A new journey, `Sw369DiagramJourneyTests`: an agent adds one `stack`, one `bars`, one `flow` and one `levels` slide over MCP, with the slots of slides 14, 16, 18 and 11 from the mockup page. A person opens the deck: each slide shows its segments, bars, nodes and rungs as text (`data-testid`s `stack-segment`, `bars-bar`, `flow-node`, `levels-rung`, `facts-tile`), and no `<img>`.
- The same journey steps through the `flow` slide: three clicks show three nodes, and the hidden ones kept their space (the first node's box does not move).
- Changing the palette in the Look panel changes the first bar's fill colour.
- `MeasureJourney` gains the four layouts, so each is measured against the safe area in the real app.

**WebApi (`SlideWorks.WebApi.Tests`).**

- `Slides/AddSlideTests`: each layout accepted with its typical slots; each validator rule refused with the path and message in sections 4–7 (values on some segments, `unit` without values, `wasLabel` without `was`, a note too long for its bar, two group nodes, four nodes with a group, a ladder that is too tall).
- One raw JSON-RPC call per layout through `Decks/McpWire.cs`, with an enum argument (`side`, `values`, `state`) on the wire.
- `Decks/DeckReadToolsTests`: `list_layouts` returns the four layouts with their `stepsFrom`; `list_blocks` returns `levels` and `facts`.
- `Slides/ListLayoutsByTemplateTests`: the layouts appear for the templates that allow them.
- `SlideSteps` through the API: a `stack` of five segments with `reveal: one-by-one` reports five steps.

**Unit (`SlideWorks.UnitTest/Slides`).**

- `FitFixturesTests`: every fixture in sections 4–7 is accepted, and growing each `grow` path by one word is refused by the rule `refusedBy` names.
- `LayoutsDocTests`: `layouts.md` regenerated (`UPDATE_LAYOUTS_DOC=1`).
- `LayoutCatalogueTests`, `SlideStepsTests`: the new names and step lists.

**Component tests (`*.spec.tsx`).**

- `DiagramLayouts.spec.tsx` with `DiagramLayouts.fixtures.ts`: each layout through `SlideRenderer`. Asserts: text content; a toned segment's and bar's computed colour; hidden steps have `visibility: hidden` and the same box; stack proportions (a segment with twice the value is at least 1.9 times as tall when neither is at its minimum); a bar's width against `barsGeometry`; a short bar's note sits after it.
- Max-content cases from `fit-fixtures.json`, each with its wide-letter twin: everything drawn stays inside x 120–1800, y 150–996.
- `blocks/Levels.spec.tsx`, `blocks/Facts.spec.tsx`: each block alone, at 2 and at the maximum count.
- `diagrams/barsGeometry.test.ts`, `describeStack.test.ts`, `describeBars.test.ts`, `describeFlow.test.ts` (Vitest).

**Screenshot baselines (made in CI's container only).** `layout-stack`, `layout-bars`, `layout-flow`, `layout-levels`, one max each (`layout-stack-max` and so on), and `diagrams-midnight` (the four on one dark palette, once group B's surface exists). Ten files.

## 11. File impact

| File | What touches it |
|---|---|
| `src/SlideWorks.Core/Slides/LayoutCatalogue.cs` | Blocks `Levels` and `Facts` added to `Blocks`; layouts `stack`, `bars`, `flow`, `levels` added to `Layouts`; the header comment's counts |
| `src/SlideWorks.Core/Slides/LayoutRules.cs` | `Stack`, `Bars`, `Flow`, `Levels`. The file is 220 lines today; move the four into a new `DiagramRules.cs` if it passes about 400 |
| `src/SlideWorks.Core/Slides/NumberSlot.cs` | Only if "above 0" cannot be said with a minimum; otherwise the rule in `LayoutRules.Stack` covers it |
| `src/contracts/layouts.md` | Regenerated |
| `src/contracts/fit-fixtures.json` | `stackSeven`, `stackEight`, `barsSix`, `barsNote`, `flowFour`, `flowGroup`, `levelsSeven`, `levelsText`, each with its Title Case, prose and wide twins |
| `src/contracts/templates.json` | The four layouts added to each built-in template's `layouts` (or left to SW-373's allow-everything template: planner's call) |
| `src/contracts/sample-deck.json` | Unchanged |
| `src/SlideWorks.Client/src/slides/slots.ts` | `LAYOUT_NAMES`, `StackSlots`, `BarsSlots`, `FlowSlots`, `LevelsSlots`, `LevelsBlock`, `FactsBlock` |
| `src/SlideWorks.Client/src/slides/layouts/index.ts` | Four entries. The file says it is frozen; adding a layout is the one reason to edit it |
| `src/SlideWorks.Client/src/slides/layouts/StackLayout.tsx`, `.module.css`, `StackSegment.tsx` | New |
| `src/SlideWorks.Client/src/slides/layouts/BarsLayout.tsx`, `.module.css`, `BarRow.tsx` | New |
| `src/SlideWorks.Client/src/slides/layouts/FlowLayout.tsx`, `.module.css`, `FlowNode.tsx`, `FlowArrow.tsx` | New |
| `src/SlideWorks.Client/src/slides/layouts/LevelsLayout.tsx`, `.module.css` | New; reuses `TwoColumnColumn` |
| `src/SlideWorks.Client/src/slides/blocks/Levels.tsx`, `Facts.tsx`, their CSS and specs | New |
| `src/SlideWorks.Client/src/slides/diagrams/barsGeometry.ts`, `describeStack.ts`, `describeBars.ts`, `describeFlow.ts`, and their tests | New folder |
| `src/SlideWorks.Client/src/slides/layouts/DiagramLayouts.spec.tsx`, `DiagramLayouts.fixtures.ts`, `DiagramLayouts.spec.tsx-snapshots/` | New |
| `src/SlideWorks.Client/src/test-support/fixtures/layoutSlots.ts` | Typical slots for the four layouts |
| `src/SlideWorks.Client/src/look/slidesUseTokens.test.ts` | Unchanged; the new CSS must pass it |
| `src/SlideWorks.UnitTest/Slides/FitFixturesTests.cs`, `LayoutCatalogueTests.cs`, `SlideStepsTests.cs` | New cases |
| `src/SlideWorks.WebApi.Tests/Slides/AddSlideTests.cs`, `ListLayoutsByTemplateTests.cs`, `Decks/DeckReadToolsTests.cs` | New cases |
| `src/SlideWorks.AcceptanceTests/Journeys/Sw369DiagramJourneyTests.cs`, `MeasureJourney.cs`, a page object in `Pages/` | New journey; four more layouts measured |
| `src/SlideWorks.WebApi/Features/Decks/Mcp/*` | Unchanged: `list_layouts` and `list_blocks` read the catalogue |
| `design/README.md` | SW-50 section: add this page and its PNGs when the three gap designs are merged |

The four layouts are independent of each other after one shared task (catalogue entries, `slots.ts`, `index.ts`, `layoutSlots.ts`), so they can be four parallel lanes. `levels` should follow group B's `two-column` or surface changes if those touch `TwoColumnColumn`.

Total: four M items, about 260 lines of C#, 940 of client code and 1,500 of tests and fixtures.
