# SW-50 part 2, group B: new or wider layouts (build spec)

Stories: SW-358, SW-359, SW-362, SW-363, SW-365, SW-366, SW-368. Mock-ups: [`sw-50-gaps-b.html`](sw-50-gaps-b.html), PNGs `png/sw-50-gaps-b-NN-*.png`. Checked against main at d3f0048.

Every size is canvas px on the 1920×1080 slide. "Lines(text, width, size)" is `SlideFit.Lines` at 0.56 em. The body budget is `SlideFit.BODY_HEIGHT` (620 px) unless a rule says otherwise. All new error texts go through the existing `validation.Fail(path, message)` and reach the agent as `path: message`.

## What group B takes from group A

Group B places these and does not define them. If A's design differs, only the marked lines here change.

| From A | Where B uses it | What B assumes |
|---|---|---|
| Inline emphasis (SW-360) | `statement.statement`, `quote.quote`, a compare panel's `line` | A slot type for display text that marks a run as emphasised. It costs no extra width in `SlideFit`. On a dark slide the emphasis colour is `accent-glow`. |
| Icons (SW-361) | `cards[].icon`, `numbered-cards` is left without, a compare panel's `icon` | An optional icon slot type. In the fit rules an icon on a card is `ICON_BLOCK` px tall (B's numbers use 78: 56 px icon plus 22 px under it). |
| Callout slot (SW-357) | `image.callout`; also `statement`, `quote`, `links`, `cards`, `compare` if A adds it there | `Opt("callout", new BlockSlot(Callout))` as on `bullets`. B's fit rules add `CalloutHeight(callout, width)` for it. |
| Step rules (SW-364) | `statement.sub`, `quote.stat`, `quote.follow_up`, the compare stamps | The existing `reveal` slot (`all`, `one-by-one`). A widens `LayoutDefinition.StepsFrom` from one list to an ordered list of **step targets** (a list field, or an optional slot), and `SlideSteps.Count` counts the targets that are present. B only names the targets and their order. |

If A does not deliver step targets, B's three layouts ship with `reveal` accepted and `SlideSteps.Count` extended by B for these targets only (about 40 lines in `SlideSteps.cs`).

---

## SW-359 Dark variant of content layouts, per slide

### Model

- **Slide field, not a slot:** `surface`, `"light"` or `"dark"`, optional. It sits next to `part`, `notes` and `deep_dive` on `add_slide` and `update_slide`. Left out (or null) the slide takes its layout's own surface.
- **Layout surface:** `LayoutDefinition` replaces `bool Dark` with `LayoutSurface Surface` (`Light`, `Dark`, `Either`). Same word and meaning as D16 for custom layouts.
  - `Dark`: `title`, `divider`, `close`, `join` (unchanged).
  - `Either`: `statement` (new), `quote`, `bullets`, `big-number`.
  - `Light`: every other layout.
- `LayoutDefinition.Dark` stays as a computed property (`Surface == LayoutSurface.Dark`) so nothing else breaks.
- New `SlideSurface` (Core, static): `Resolve(LayoutDefinition, string? requested)` returns `light` or `dark`; `Check(...)` adds the errors below.
- Storage: `Slide.Surface` (`string?`, max 5), one EF migration. Null for every existing slide.
- `update_slide`: `surface` given sets it; `surface: "default"` clears it; left out keeps it. When the layout changes and the stored surface is one the new layout cannot take, the stored value is cleared, with no error.

### Validation and errors

| Case | Error |
|---|---|
| Not `light`, `dark` (or `default` on update) | `surface: must be 'light' or 'dark'` |
| `dark` on a light-only layout | `surface: the 'cards' layout is always light; statement, quote, bullets and big-number can be dark` (the list comes from the catalogue) |
| `light` on an always-dark layout | `surface: the 'title' layout is always dark` |

Giving a fixed layout its own surface is accepted and stored as null.

### How the tokens switch

One CSS block, keyed on the frame, re-points the light tokens to the palette's night set. Layout components do not change.

```css
/* src/SlideWorks.Client/src/slides/chrome/surface.css, imported by SlideFrame */
.sw-frame[data-dark='true'] {
  --sw-ink: var(--sw-night-ink);
  --sw-muted: var(--sw-night-2);
  --sw-grid: var(--sw-night-line);
  --sw-card: color-mix(in srgb, var(--sw-night-ink) 7%, var(--sw-night));
  --sw-chip: color-mix(in srgb, var(--sw-night-ink) 12%, var(--sw-night));
  --sw-accent-dark: var(--sw-accent-glow);
  --sw-accent-light: color-mix(in srgb, var(--sw-accent) 24%, var(--sw-night));
  --sw-good-soft: color-mix(in srgb, var(--sw-good) 22%, var(--sw-night));
  --sw-good-ink: var(--sw-night-ink);
  --sw-bad-soft: color-mix(in srgb, var(--sw-bad) 22%, var(--sw-night));
  --sw-bad-ink: var(--sw-night-ink);
  --sw-inverse-surface: color-mix(in srgb, var(--sw-night-ink) 10%, var(--sw-night));
  --sw-lift: none;
}
```

- `title`, `divider`, `close`, `join` and the journey use none of the re-pointed tokens today (checked with grep), so the block is safe on every dark slide.
- `--sw-inverse-surface` is a new derived token in `theme/tokens.css` (default `var(--sw-night)`); `tone.ts` uses it for the soft inverse surface, so a soft inverse panel is visible on a dark slide. It is derived, not a palette key.
- The kicker is text in `--sw-accent`, which is too dim on night: `Kicker.module.css` adds `.sw-frame[data-dark='true'] .kicker { color: var(--sw-accent-glow) }`. The deep-dive kicker rule keeps priority and uses `--sw-dd-on-night` on dark.
- Solid panels keep `panel-*` and `panel-*-ink`.

### Chrome

- `SlideFrame` gets `dark` from the resolved surface. Tracker and footer are hidden by layout, no longer by darkness: a new `CHROMELESS_LAYOUTS = ['title','divider','close','join']` in `slots.ts` replaces the `!dark` tests. `DARK_LAYOUTS` goes; `LAYOUT_SURFACE: Record<LayoutName, 'light'|'dark'|'either'>` replaces it.
- `chrome.css`: on dark, tracker text and footer text are `--sw-night-muted`, the past tracker mark is `color-mix(in srgb, var(--sw-accent) 45%, var(--sw-night))`, the current one keeps the accent with `--sw-accent-glow` text. The progress bar's dark rule already exists.
- Footer logo on dark: the template's dark logo when part 1 gives one (D12), else the deck logo as today. `SlideFooter` takes the URL from the frame; the choice is one line in `SlideFrame`.

### Contrast

`PaletteContrast` gains five pairs (all `TEXT`, 4.5). `Contrast` gains `Mix(fg, bg, share)` in sRGB to build the mixed backgrounds.

| Pair | Foreground | Background |
|---|---|---|
| `dark-kicker` | `accent-glow` | `night` |
| `dark-chrome` | `night-muted` | `night` |
| `dark-soft-accent` | `night-ink` | `accent` 24% over `night` |
| `dark-soft-positive` | `night-ink` | `good` 22% over `night` |
| `dark-soft-negative` | `night-ink` | `bad` 22% over `night` |

The four built-in palettes must pass (Cobalt does by calculation: 6.4 for `dark-chrome`). Custom palettes (part 1) are held to the same list.

### MCP surface

- `add_slide`, `update_slide`: new argument `surface` with the description "light or dark, for a layout whose surface is 'either' (list_layouts says which). Leave it out for the layout's own surface."
- `list_layouts`: each layout gains `surface` (`light`, `dark`, `either`). `dark` stays (true for always-dark only). `layouts.md` table gets a Surface column in place of Dark.
- `get_deck` slides, the play view, the deck page and the presenter view carry the resolved `surface` per slide (`"light"` or `"dark"`, never null).
- Activity summary: "made slide 27 dark" / "made slide 27 light", joined to the usual update summary.

### Files

- Core: `Slides/LayoutDefinition.cs`, `Slides/LayoutCatalogue.cs`, new `Slides/LayoutSurface.cs`, new `Slides/SlideSurface.cs`, `Look/PaletteContrast.cs`, `Look/Contrast.cs`.
- WebApi: `Features/Slides/Slide.cs`, `AddSlideRequest.cs`, `UpdateSlideRequest.cs`, `SlideEdits.cs`, `SlideChecks.cs`, `SlideCommands.cs`, `Mcp/AddSlideTool.cs`, `Mcp/UpdateSlideTool.cs`, `Features/Decks/Views/DeckPlaySlide.cs`, `DeckPageSlide.cs`, `Features/Decks/Mcp/GetDeckSlide.cs`, `LayoutInfo.cs`, `Features/Presenting/Views/PresenterSlide.cs`, a migration, `Data/AppDbContext` mapping.
- Contracts: `screens/deck-play.json`, `deck-page.json`, `presenter.json`, `sample-deck.json` (one dark quote), `layouts.md` (generated).
- Client: `slides/slots.ts`, `slides/SlideRenderer.tsx` (`RenderableSlide.surface`), `slides/SlideFrame.tsx`, `slides/chrome/chrome.css`, new `slides/chrome/surface.css`, `theme/tokens.css`, `slides/blocks/Kicker.module.css`, `slides/blocks/tone.ts`, `api/types.ts`.

### Tests

- UnitTest: `SlideSurfaceTests` (resolve and the three errors), `BuiltInPaletteContrastTests` (five new pairs), `LayoutCatalogueTests` (surface per layout), `LayoutsDocTests`.
- WebApi.Tests: new `Slides/SlideSurfaceTests.cs`: add dark bullets, read it back from play view and `get_deck`; refuse dark cards; update to `default`; layout change clears it; wire test for the `surface` argument (`McpWire`). `ScreenRecordShapeTests` for the three screens.
- CT: `SlideFrame.spec.tsx` gains "content slide, dark" (tracker and footer shown) for Cobalt and Ember; `ContentLayouts.spec.tsx` renders `bulletsCallout` and a `big-number` dark; `StatementLayouts.spec.tsx` renders `quote` dark. `slidesUseTokens.test.ts` must still pass (`surface.css` is under `slides/`).
- Acceptance journey: an agent adds a dark quote; the player shows the night background with the tracker and footer.
- Baselines: new dark ones only. No existing baseline changes.

### Backwards compatibility

Existing slides have no surface and draw as before. `list_layouts.dark` keeps its meaning.

### Size

M. About 350 lines of product code, 250 of tests.

---

## SW-358 Statement layout

### Model

New layout `statement`, surface `Either`, description: "One big sentence that carries the slide, light or dark, with an optional sub line. Can reveal the sub line on a step."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | |
| `statement` | text (A's emphasis type) | yes | 70, line breaks allowed | "Set at 210 px: use \n for a line break, at most 3 written and 3 shown lines; it shrinks to 110 px to keep them" |
| `sub` | text | no | 100 | "One or two lines under the sentence" |
| `reveal` | `all` or `one-by-one` | no | | Step target: `sub` |

### Rules (`LayoutRules.Statement`)

- Written lines (split on `\n`) at most 3: `statement.statement: at most 3 lines (got 4)`.
- Shown lines at the 110 px floor in 1680 px (27 characters a line), summed over the written lines, at most 3: `statement.statement: too long to fit: it wraps to about 4 lines at the statement's smallest size, and at most 3 fit`.
- `sub` at 56 px in 1680 px (53 characters a line) at most 2 lines: `statement.sub: too long to fit: it wraps to about 3 lines, and at most 2 fit`.

### Client

`layouts/StatementLayout.tsx` and `.module.css`, modelled on `CloseLayout`: `.sw-layout` with `justify-content: safe center`; the `Kicker` block; an `h2` of one `span` per written line, `font: 800 210px/1 var(--sw-font-head)`, letter-spacing −0.035em, max-width 1680; the sub at `600 56px/1.25 var(--sw-font-head)`, 44 px under the sentence, colour `--sw-accent-dark` (which is `accent-glow` on dark). `useFitText` budget `{ start: 210, floor: 110, lines: 3, lineHeight: 1, maxHeight }` where `maxHeight` is 846 less the kicker (42), the sub (44 + its height) and nothing else. The sub is wrapped in `Reveal` at step 1 when `reveal` is `one-by-one`. Tracker and footer follow the template's chrome, as on any content slide.

### Fit fixtures

`statementMax`: 40-character kicker, three written lines of about 20 characters, a 100-character sub. `refusedBy`: none (display text); the CT case and its wide-letter twin assert the box x 120–1800, y 150–996.

### MCP surface

`list_layouts` shows `statement` with `surface: "either"` and steps "one for `sub` when `reveal` is `one-by-one`". `templates.json`: add `statement` to `talk` and `workshop`.

### Files

Core `LayoutCatalogue.cs`, `LayoutRules.cs`. Contracts `fit-fixtures.json`, `templates.json`, `sample-deck.json`, `layouts.md`. Client `slots.ts` (`StatementSlots`, `LAYOUT_NAMES`, `SLOT_KEYS`), `layouts/index.ts`, new `layouts/StatementLayout.tsx`, `StatementLayout.module.css`.

### Tests

- UnitTest: `SlideValidatorTests` (three rules), `FitFixturesTests`, `SlideStepsTests` (0 and 1 step), `LayoutsDocTests`.
- WebApi.Tests: `AddSlideTests` adds a statement; `LayoutOutsideTemplateWarnsTests` for the `update` template.
- CT: `StatementLayouts.spec.tsx`: light, dark, max, wide twin, sub hidden at step 0 and shown at step 1 (still rendered when hidden). Three new baselines.
- Acceptance: in the SW-359 journey the agent adds a dark statement and steps to the sub.

### Backwards compatibility

A new layout. Tests that count layouts (19) change to 21 with `links`.

### Size

S. About 200 lines of product code, 150 of tests.

---

## SW-362 Cards and numbered-cards use the slide's height

### Model

| Slot | Change |
|---|---|
| `cards.cards` | 1–4 items (was 1–3) |
| `cards.cards[].fill` | `soft`, `solid`, `plain` (new value). Description: "'plain' keeps the card white and draws a top rule in the tone's colour" |
| `cards.cards[].pin` | new, text, optional, 24: "A short label in the card's corner, such as 'Hold on to this one'" |
| `cards.cards[].icon` | A's icon type, optional |
| `numbered-cards.cards[]` | gains `tone`, `fill` (with `plain`) and `pin`, defined by the same helper as `cards` |

`fill` without `tone` is ignored, as today. `plain` is added to the one shared `Fill()` helper, so the callout accepts it too; on a callout `plain` draws as `soft` (noted in the slot description).

### Arrangement

- `cards`: 1–3 cards in three columns 32 px apart (539 px each), as today. 4 cards: two columns (824 px) and two rows, 32 px and 28 px apart. No slot chooses this.
- `numbered-cards`: 2 or 3 cards share three columns (541 px); 4 cards keep four columns (399 px). Today 2 or 3 cards sit in a four-column grid and leave a hole.
- In both, the list is the body's flex child and fills it (`flex: 1; grid-auto-rows: minmax(0, 1fr)`), so every card is the body's height (or half of it, less the gap, in the 2×2).

### Scale (`CardScale`)

The type is set **large** when every card on the slide has a title of at most 32 characters, a text of at most 70, and no chips. Otherwise **regular** (today's sizes). The rule is on character counts only, so the server and the client can both run it and nothing is measured.

| | Regular | Large |
|---|---|---|
| `cards` row | title 34/1.15, text 24/1.4, padding 34/36 | title 800 50/1.12, text 30/1.4, padding 40 |
| `cards` 2×2 | same type, padding 28/36 | title 800 44/1.12, text 28/1.4, padding 32/40 |
| `numbered-cards` | numeral 64, title 30/1.2, text 23/1.4 | numeral 96, title 800 40/1.15, text 28/1.4 |

- Core: new `Slides/CardScale.cs`: `Of(JsonElement cards)` returns `Large` or `Regular`, with the constants `TITLE_MAX = 32`, `TEXT_MAX = 70`.
- Client: new `slides/layouts/cardScale.ts` with the same rule; the list gets `data-scale`.
- New shared file `src/contracts/card-scale-fixtures.json`: an array of `{ cards, scale }` cases (at the limits, one over each limit, with a chip). `CardScaleTests` (UnitTest) and `cardScale.test.ts` (vitest) both read it, so the two rules cannot drift.
- `layouts.md` states the rule in the `cards` and `numbered-cards` descriptions.

### Fit rules (new `LayoutRules.Cards` and `LayoutRules.NumberedCards`)

Neither layout has a fit rule today. Each card's height is estimated at the slide's scale and checked against the height it gets. `w` is the card's text width.

- `cards`, row (budget 620): regular, `w` 467: `68 + badge(48) + icon(ICON_BLOCK) + Lines(title, w, 34)×39.1 + 12 + Lines(text, w, 24)×33.6 + chips + pin(44) + rule(8)`. Large, `w` 459: `80 + badge(48) + icon + Lines(title, w, 50)×56 + 16 + Lines(text, w, 30)×42 + pin(44) + rule(8)`.
- `cards`, 2×2 (budget 296 = (620 − 28) / 2): regular, `w` 752: `56 + …` with the regular type. Large, `w` 744: `64 + badge + icon + Lines(title, w, 44)×49.3 + 12 + Lines(text, w, 28)×39.2 + pin + rule`.
- `numbered-cards` (budget 620; `w` 477 in three columns, 335 in four): regular `72 + 64 + 22 + Lines(title, w, 30)×36 + 14 + Lines(text, w, 23)×32.2 + pin + rule`. Large `80 + 96 + 26 + Lines(title, w, 40)×46 + 14 + Lines(text, w, 28)×39.2 + pin + rule`.
- `chips`: `22 + rows×38 + (rows − 1)×10`, where chips pack greedily into `w` at `32 + 10.1×length` px each, 10 px apart.
- Parts in brackets count only when present; `rule` counts for `fill: plain`.
- Error, on the first card that is too tall: `cards.cards[1]: too much text to fit on the slide (about 331 px of 296 px); use fewer or shorter items`. This is `SlideFit.Check` with the path and budget.

What this accepts: a row card with a badge, a 50-character title, a 140-character text and two chips (about 525 px). In the 2×2 a full title and text (about 259 px) fit; a badge or chips on top of both do not.

### Fit fixtures

Replace or add: `cardsRowMax` (regular, as above), `cardsLargeRow` (three cards at the large limits with badge and icon), `cardsGridMax` (four cards, 50/140, no badge), `cardsGridLarge` (four at the large limits, one solid with a pin), `numberedCardsFourMax` (regular, last card solid with a pin), `numberedCardsLargeThree`. Each has an `estimate`, `grow` pointers and `refusedBy: "fit"`. The existing `cardsProse`, `cardsTitleCase`, `cardsWide`, `numberedCards*` stay and must still pass the new rules; if one fails, the fixture was over the real limit and is shortened in this story, with the reason in the commit.

### Client

- `layouts/CardsLayout.tsx`, `.module.css`: `data-count`, `data-scale`, the 2×2 grid, the fill.
- `layouts/Card.tsx`, `Card.module.css`: large type, `pin`, `fill: plain` (`border-top: 8px solid` in the tone's colour: `--sw-accent`, `--sw-good`, `--sw-bad`, `--sw-night`), icon slot (A).
- `layouts/NumberedCardsLayout.tsx`, `.module.css`: column count, scale, tone and fill through `toneStyle`, pin. Extract the pin as `blocks/Pin.tsx` and `Pin.module.css`, used by both.
- `blocks/tone.ts`: `Fill` gains `plain`; `tonePair` returns the card pair (`card`, `ink`) for it and a new `toneRule(tone)` gives the rule's token.
- Pin: bottom right, 28 px in, `700 15px/1`, letter-spacing 0.14em, uppercase, pill, `accent-light` on `accent-dark`; on a solid card the ink at 22% over the surface with the ink as text.

### MCP surface

`list_layouts` shows the new limits and slots. No new error kinds beyond the fit error above.

### Files

Core `LayoutCatalogue.cs`, `LayoutRules.cs`, new `CardScale.cs`. Contracts `fit-fixtures.json`, new `card-scale-fixtures.json`, `layouts.md`. Client as above plus `slots.ts` (`Card`, `NumberedCard`, `Fill`), `layouts/CardLayouts.fixtures.ts`.

### Tests

- UnitTest: `CardScaleTests`, `FitFixturesTests`, `SlideValidatorTests` (four cards accepted, five refused, pin over 24, 2×2 with badge and long text refused with the px message).
- vitest: `cardScale.test.ts`, `tone.test.ts` (plain).
- CT: `CardLayouts.spec.tsx` and `CardsLayout.spec.tsx`: every card's bottom edge equals the body's (row) or the row's (2×2); the new fixtures and wide twins stay inside the box; reveal still works in the 2×2 (hidden cards rendered).
- Baselines affected: **every existing `cards` and `numbered-cards` baseline changes** (cards are taller; short ones go large): `CardLayouts.spec.tsx-snapshots/*`, `CardsLayout.spec.tsx-snapshots/*`. New baselines for the 2×2, the highlighted card and plain rules.
- Acceptance: the agent adds four manifesto cards with one solid and pinned; the player shows two rows and the pin text.

### Backwards compatibility

Existing slides validate unless they break the new fit rules; stored slides are never re-validated on read, so they keep rendering. They look different: cards fill the height, 2–3 numbered cards get wider, and short cards get large type. That is the point of the story, and it changes baselines, not data. An `update_slide` on an old over-full cards slide can now be refused with the fit message.

### Size

L. About 500 lines of product code, 350 of tests, plus baselines.

---

## SW-363 Compare panels: rows, a stamp and a large line

### Model

`ComparePanel(defaultTone)` in `LayoutCatalogue` becomes (changes marked):

| Panel slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `label` | text | yes | 24 | unchanged |
| `title` | text | yes | 50 | unchanged |
| `sub` | text | no | 100 | unchanged |
| `points` | list of text | **no** | 1–4, each 90 | was required |
| `rows` | list of `{label, value}` | no, new | 1–4; `label` 16, `value` 40 | "Label and value rows, such as 'Starts with' and 'A spec'" |
| `line` | text (A's emphasis type) | no, new | 100 | "One large line, such as a quoted sentence. Write the quotation marks yourself" |
| `stamp` | block `stamp` | no, new | 16 | Step target |
| `footer` | text | no, new | 90 | "A closing line at the panel's bottom edge" |
| `tone` | tone | no | | unchanged; defaults `negative` / `positive` |
| `fill` | `soft`, `solid`, `plain` | no | default `soft` | `plain` new: the card surface, the tone on the top edge and the label |
| `icon` | A's icon type | no | | beside the label |

- `ObjectSlot` `exactlyOneOf: ["points", "rows", "line"]` (the mechanism `two-column` uses). Error as today's: `compare.failure: exactly one of points, rows, line`.
- New slot on the layout: `reveal` (`all`, `one-by-one`). Step targets: the stamps, as **one** step for both panels. No stamp, no step.
- The `stamp` block's description changes to "A tilted stamp, such as 'Draft'" (it is no longer only on the title slide).

### Fit rule (`LayoutRules.Compare`, extended)

Per panel, budget 620, text width 724:

`160 + Lines(title, 724, 44)×48.4 + sub + body + stamp + footer`

- `sub`: `16 + Lines×37.8` (as today).
- body, `points`: as today. `rows`: `Σ (25 + Lines(value, 524, 32)×38.4)`; the label column is 180 px and never wraps (16 characters at 16 px uppercase). `line`: `Lines(line, 724, 40)×50`.
- `stamp`: 78. `footer`: `35 + Lines(footer, 724, 24)×33.6`.
- Error path `compare.failure` or `compare.success`, the usual fit message.

Accepted maximum with rows: a one-line title, four one-line values, a stamp and a one-line footer (about 610 px).

### Client

- `layouts/ComparePanel.tsx`, `ComparePanel.module.css`: the panel becomes a flex column. Rows: a grid `180px 1fr`, each row 12 px padding and a 1 px top line at 14% of the ink; label `700 16px/1.2` uppercase, letter-spacing 0.14em, at 62% of the ink; value `700 32px/1.2 var(--sw-font-head)`. Line: `700 40px/1.25 var(--sw-font-head)`, 26 px above. Footer: `margin-top: auto`, a 1 px line (20 px above, 14 px below), `400 24px/1.4` at 80% of the ink.
- Stamp in a panel: `blocks/Stamp.tsx` gains a `placement="inline"` variant (`Stamp.module.css`): in flow, 24 px above, 4 px border, radius 10, `800 26px/1`, letter-spacing 0.16em, uppercase, rotated −4°, in the panel's edge colour (the ink on a solid panel). Wrapped in `Reveal` at step 1 when `reveal` is `one-by-one`.
- `fill: plain`: surface `--sw-card`, ink `--sw-ink`, top edge the tone's colour, label in the tone's soft ink (`accent-dark`, `good-ink`, `bad-ink`, `ink` for inverse).
- Panels keep their content height (the taller panel's); they are not stretched to the body.
- `layouts/CompareLayout.tsx`: passes `step` and `reveal`.

### Fit fixtures

`compareRowsMax` (as above, both panels), `compareLineMax` (title 50, line 100, footer 90), `comparePointsStampFooter`. The existing `compare` fixture stays.

### MCP surface

`list_layouts`: `compare` gains steps "one for the stamps when `reveal` is `one-by-one`". The description becomes "Two panels side by side. Each holds points, label and value rows, or one large line, with an optional stamp and footer."

### Files

Core `LayoutCatalogue.cs`, `LayoutRules.cs`. Contracts `fit-fixtures.json`, `layouts.md`. Client `slots.ts` (`CompareSide`, `CompareSlots`), `layouts/ComparePanel.tsx`, `ComparePanel.module.css`, `layouts/CompareLayout.tsx`, `blocks/Stamp.tsx`, `Stamp.module.css`, `blocks/tone.ts` (shared with SW-362: `plain`), `layouts/ContentLayouts.fixtures.ts`.

### Tests

- UnitTest: `SlideValidatorTests` (exactly one of; rows over the budget), `FitFixturesTests`, `SlideStepsTests`.
- CT: `CompareLayout.spec.tsx`: rows, line with footer at the panel's bottom when the other panel is taller, stamp hidden at step 0 but rendered, plain and solid. Three new baselines. Existing compare baselines must not change.
- Acceptance: covered by one step in the SW-362 journey (a compare slide with stamps, step forward, stamp visible).

### Backwards compatibility

Existing slides have `points` and no new slot: same validation result, same pixels.

### Size

M. About 350 lines of product code, 250 of tests.

---

## SW-365 Quote layout: headline, stat, follow-up and source

### Model

Surface `Either`. Description: "A quotation with its attribution, and optionally a headline, a supporting stat, a follow-up line and a source."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | unchanged |
| `headline` | text | no, new | 80 | |
| `quote` | text (A's emphasis type) | yes | 280 | unchanged otherwise |
| `attribution` | text | yes | 80 | "Who said it. With a role, this is the name" |
| `role` | text | no, new | 160 | "The speaker's role, and where and when it was said" |
| `stat` | object | no, new | | Step target 1 |
| `stat.value` | text | yes | 12 | "Such as '4 months'" |
| `stat.caption` | text | yes | 90 | |
| `follow_up` | text | no, new | 160 | "A line under the quote". Step target 2 |
| `source` | text | no, new | 200 | "Where the quote and the stat come from, without a 'Source:' prefix" |
| `reveal` | `all` or `one-by-one` | no, new | | |

The two-point timeline on source slide 20 is not part of `stat`. It belongs to the diagram blocks (group C).

### Drawing

- With a headline the slide opens with `SlideHeading` and the figure sits in `.sw-body`; without one it is as today (kicker, then the figure centred).
- Figure: the 8 px accent rule and 48 px indent stay (text width 1432).
- Attribution row, 34 px under the quote: name and role on the left (`400 28px/1.3`, the name `700` in `--sw-ink` when a role is set, the role after a comma in `--sw-muted`); the stat on the right, 420 px wide, 48 px from the text: a soft negative panel (24/30 px padding, radius 16, the 6 px left strip), value `800 72px/1 var(--sw-font-head)`, caption `400 22px/1.3`.
- Follow-up: inside the figure, a 1 px `--sw-grid` line 34 px above and below, then `600 32px/1.35 var(--sw-font-body)`.
- Source: under the figure, 24 px above, `400 20px/1.35`, `--sw-muted` (night-muted on dark), max-width 1480.
- The quote is display text: `QUOTE_FIT` keeps `{ start: 62, floor: 36, lines: 7, lineHeight: 1.22 }` and gains `maxHeight`: the layout's height less everything else.

### Fit rule (new `LayoutRules.Quote`)

The server checks that the quote fits at its 36 px floor. Height available to the slide: 846 (y 150 to 996).

`fixed = 42 (kicker) + headline + attribution + follow_up + source`

- `headline`: `Lines(headline, 1560, 64)×69.1 + 46`.
- `attribution`: `34 + max(Lines(name + role, 1432 or 964 with a stat, 28)×36.4, stat)`, where `stat` is `58 + 72 + Lines(caption, 360, 22)×28.6`.
- `follow_up`: `69 + Lines(follow_up, 1432, 32)×43.2`. `source`: `24 + Lines(source, 1480, 20)×27`.
- Check: `Lines("“" + quote + "”", 1432, 36)×43.9 ≤ 846 − fixed`, reported with `SlideFit.Check(height, "quote.quote", validation, budget)`: `quote.quote: too much text to fit on the slide (about 220 px of 203 px); use fewer or shorter items`.
- The rule runs only when one of the new slots is present, so every stored quote still validates.

Accepted maximum: a two-line headline, a 200-character quote, name and role, a stat, a one-line follow-up and a two-line source.

### Fit fixtures

`quoteFull` (as above, `refusedBy: "fit"`, grow `/quote`), `quoteStatOnly`, `quoteFollowUpDark` (with `"surface": "dark"`; the fixture format gains an optional `surface`).

### MCP surface

`list_layouts`: steps "one each for `stat` and `follow_up` when `reveal` is `one-by-one`".

### Files

Core `LayoutCatalogue.cs`, `LayoutRules.cs`. Contracts `fit-fixtures.json`, `layouts.md`, `sample-deck.json`. Client `slots.ts` (`QuoteSlots`), `layouts/QuoteLayout.tsx`, `QuoteLayout.module.css`, `layouts/ContentLayouts.fixtures.ts`.

### Tests

- UnitTest: `SlideValidatorTests` (each new slot; over-full refused), `FitFixturesTests`, `SlideStepsTests` (0, 1, 2 steps).
- CT: `StatementLayouts.spec.tsx` (it holds the quote today): full light, full dark, stat and follow-up hidden then shown, the quote's font size between 36 and 62 and its box above y 996, wide twin. Three new baselines. The existing quote baseline must not change.
- Acceptance: in the SW-359 journey, the dark quote has a stat on step 1.

### Backwards compatibility

A quote with only today's slots is drawn by the same CSS at the same sizes.

### Size

M. About 300 lines of product code, 250 of tests.

---

## SW-366 Links slide

### Model

New slot type `LinkSlot` (Core, `Slides/LinkSlot.cs`), kind name `link`:

- A JSON string, at most `max` characters (500 by default), no leading or trailing space.
- `Uri.TryCreate(value, UriKind.Absolute)` succeeds; scheme is `http` or `https` (compared ordinally, lower-cased); host is not empty; `UserInfo` is empty; no character below U+0020, no U+007F and no space anywhere.
- JSON schema: `{ "type": "string", "format": "uri", "pattern": "^https?://", "maxLength": 500 }`. In `layouts.md`: kind "link", limits "an http or https address, up to 500 chars".

| Error | Text |
|---|---|
| Not a string | `links.links[0].url: must be text` (as `TextSlot`) |
| Too long | `links.links[0].url: at most 500 characters (got 512)` |
| Not an http(s) address | `links.links[0].url: must be an http or https address, such as https://example.com/page` |
| Has user info | `links.links[0].url: must not hold a user name or password` |

New layout `links`, surface `Light`, description: "A list of 1–6 links, each a kind, a title and a source, with an optional QR code for one address. Titles open the link in the player."

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `kicker` | text | no | 40 | |
| `headline` | text | yes | 80 | |
| `links` | list of object | yes | 1–6 | |
| `links[].kind` | text | no | 16 | "Such as 'Talk' or 'Article'" |
| `links[].title` | text | yes | 90 | "Drawn as the link" |
| `links[].source` | text | no | 100 | "Who and where, such as 'Sam Carter, example.com'" |
| `links[].url` | link | yes | 500 | |
| `qr` | object | no | | |
| `qr.url` | link | yes | 200 | "The address the QR code opens. Short addresses scan best" |
| `qr.caption` | text | no | 60 | "Under the code; defaults to the address without its scheme" |

No steps.

### Fit rule (`LayoutRules.Links`)

List width `W` is 1680, or 1216 with a QR code (400 px code, 64 px gap). Row: `28 + Lines(title, W − 226, 28)×35 + (source ? 4 + Lines(source, W − 226, 20)×26 : 0)`; rows 12 px apart; sum at most 620. Path `links.links`. Accepted maximum: six rows with one-line titles and one-line sources beside a QR code (about 594 px).

### Client

- New `layouts/LinksLayout.tsx`, `LinksLayout.module.css`. Row: a card (`--sw-card`, radius 14, lift), grid `150px 1fr`, kind `700 15px/1.2` uppercase in `--sw-accent`, title `700 28px/1.25 var(--sw-font-head)` in `--sw-accent-dark` underlined at 40% accent, source `400 20px/1.3` muted.
- The title is `<a href={url} target="_blank" rel="noopener noreferrer">` only when new `slides/safeHref.ts` accepts the address (parsed with `new URL`, protocol `http:` or `https:`); otherwise it is plain text. This repeats the server's check for slides stored before a rule changes.
- The QR code: `src/SlideWorks.Client/src/qr/QrCode.tsx` (the join slide's, `qrcode` 1.5.4, error correction M, 4-module quiet zone, black on white). It already sits outside `slides/**`, so the token guard is not touched. 400×400 px on a white card, radius 16; label `QR code for {caption}`. A 200-character address is a version 10 code (57 modules), about 6 px a module on the canvas.
- Caption: `500 20px/1.3 var(--sw-font-mono)`, centred, two lines at most.
- The player must not move on a link click: `player/PlayerStage.tsx` `onClick` returns early when `event.target.closest('a[href]')` is set. Touch: a tap on a link is not a swipe, so nothing changes there.
- Deck page thumbnails and the presenter's small views draw slides under a click target; there the anchors get `tabIndex={-1}` and `pointer-events: none` through a new `interactive: boolean` on `SlideDeck` (true in the player, the audience window and the guest's phone; false elsewhere).

### MCP surface

`list_layouts` shows `links` and the `link` kind. `templates.json`: add `links` to `talk` and `workshop`. `preview_deck` and the share card need nothing.

### Files

Core new `Slides/LinkSlot.cs`, `LayoutCatalogue.cs`, `LayoutRules.cs`, `Slides/LayoutsDoc` support for the new kind (it lives in `SlideWorks.UnitTest/Slides/LayoutsDoc.cs`). Contracts `fit-fixtures.json`, `templates.json`, `layouts.md`. Client `slots.ts`, `layouts/index.ts`, new `layouts/LinksLayout.tsx`, `LinksLayout.module.css`, new `slides/safeHref.ts`, `player/PlayerStage.tsx`, the callers that build `SlideDeck` (`interactive`).

### Tests

- UnitTest: `LinkSlotTests` (accepted: http, https, upper-case scheme, a port, a query, an IDN host; refused: `javascript:`, `data:`, `file:`, `mailto:`, `//host`, no scheme, user info, a space, a tab, a newline, 501 characters), `SlideValidatorTests`, `FitFixturesTests` (`linksSixQr`, `linksWrapped`).
- vitest: `safeHref.test.ts` with the same refused list.
- WebApi.Tests: `AddSlideTests`: the `javascript:` address is refused with the text above over the MCP wire.
- CT: new `LinksLayout.spec.tsx`: anchors carry `href`, `target`, `rel`; an unsafe address renders no anchor; the QR code's label; fixtures inside the box; one baseline with the QR code and one without. `Player.spec.tsx`: a click on a link in the right third does not advance.
- Acceptance: an agent adds a links slide; in the player a click on the first title opens a new page with that address and the slide number stays; the shared (link) viewer sees the same.

### Backwards compatibility

A new layout and a new slot type. Nothing stored changes.

### Size

M. About 400 lines of product code, 350 of tests.

---

## SW-368 Image layout: frame, position, lead points

### Model

| Slot | Kind | Required | Limits | Notes |
|---|---|---|---|---|
| `frame` | `card` or `none` | no, new | default `card` | "'card' (the default) draws the image on a white card; 'none' draws it straight on the slide, for a transparent image" |
| `position` | `below` or `right` | no, new | default `below` | "'below' (the default) puts the image under the headline; 'right' gives it the right half at full height, with the headline, points and callout on the left" |
| `points` | list of text or `{lead, text}` | no | 1–4; text 80; `lead` 30, `text` 80 | was list of text |
| `caption` | text | no | 140 | unchanged |
| `callout` | A's callout slot | no | | placed by B |

The image knows nothing about its own transparency: the author says `frame: "none"`. The server does not read pixels.

### Drawing

- `frame: none`: no background, radius or shadow on the `<img>`.
- `position: below`: as today. The callout, when present, is the body's last row, full width, 22 px under the image and points.
- `position: right`: the layout does not use `SlideHeading`'s full width. `.sw-layout` holds one grid, `760px 1fr`, 64 px apart, running to y 996. Left column, top to bottom: kicker, headline (same type, wrapping in 760 px), points 46 px under it, the callout at the foot (`margin-top: auto`). Right column: the image scaled whole into 856×846 (less the caption), centred.
- Points with a lead draw the lead bold in `--sw-accent-dark` before the text, as `bullets` does. `position: below` keeps its 680 px points column.

### Fit rule (new `LayoutRules.Image`)

- `below`: points `Σ Lines(words, 636, 26)×35.1 + 22 a gap` at most `620 − callout`, path `image.points`; and `620 − CalloutHeight(callout, 1680) − caption(42)` at least 240, so the image keeps room: `image.callout: leaves too little room for the image; shorten the callout or drop the caption`.
- `right`: the headline may wrap to at most 3 lines in 760 px (21 characters a line): `image.headline: too long beside the image: it wraps to about 4 lines in the left column, and at most 3 fit`. Then `42 + Lines(headline, 760, 64)×69.1 + (points ? 46 + Σ Lines(words, 716, 26)×35.1 + 22 a gap : 0) + CalloutHeight(callout, 760)` at most 846, path `image.points`.

Accepted maximum on the right: a three-line headline, four two-line points, a two-line callout (about 730 px).

### Client

`layouts/ImageLayout.tsx`, `ImageLayout.module.css`: `data-frame`, `data-position`; the right arrangement as its own branch so the `below` markup and CSS stay byte-for-byte. Points render through the existing lead/text block (`blocks/LeadText.tsx`).

### Fit fixtures

`imageRightMax` (as above, default frame), `imageRightNoFrame`, `imageBelowLeadPoints`. The existing `imageTitle` and `imageProse` stay.

### MCP surface

`list_layouts` shows the new slots. The description becomes "A headline with one large image under it or to its right, with optional points, a caption and a callout."

### Files

Core `LayoutCatalogue.cs`, `LayoutRules.cs`. Contracts `fit-fixtures.json`, `layouts.md`. Client `slots.ts` (`ImageSlots`), `layouts/ImageLayout.tsx`, `ImageLayout.module.css`, `layouts/DataLayouts.fixtures.ts`.

### Tests

- UnitTest: `SlideValidatorTests` (enums, lead points, the three-line headline rule), `FitFixturesTests`.
- CT: `DataLayouts.spec.tsx`: with `frame: none` the image's computed background is transparent and it has no shadow; with `position: right` a 792×882 image is at least 700 px wide and its right edge is at x 1800; the headline's right edge is at most x 880; fixtures inside the box. Three new baselines. Existing image baselines must not change.
- Acceptance: an agent uploads a transparent PNG, adds it with `frame: none` and `position: right`; the player shows it beside the headline.

### Backwards compatibility

Slides without the new slots take the defaults and draw as before. String points still validate.

### Size

M. About 300 lines of product code, 250 of tests.

---

## Shared mechanisms

1. **`surface`** (SW-359): the slide field, `LayoutSurface`, the frame's token block. SW-358 and SW-365 need it to show their dark variants; they can be built light-only first and gain dark when SW-359 lands. Custom layouts (D16) reuse `LayoutSurface` and the same slide field.
2. **`fill: plain` and `toneRule`** in `blocks/tone.ts` and the shared `Fill()` helper in `LayoutCatalogue`: used by SW-362 and SW-363. Build it once, in SW-362, or as a small task before both.
3. **Step targets** (A, SW-364): SW-358, SW-363 and SW-365 each add `reveal` and name targets. All three touch `SlideSteps` only through A's mechanism.
4. **Emphasis, icons, callout** (A): slot types B places. B's catalogue lines for them are one line each and can land after A.
5. **Inline stamp** (`blocks/Stamp.tsx`): SW-363 only, but the block's description in `LayoutCatalogue` changes for everyone.
6. **`LinkSlot` and `safeHref`** (SW-366): the only place an address becomes an `href`. Custom layouts that want links later must use the same type.
7. **`QrCode`** (`src/qr/`): reused as is by SW-366.
8. **Fit fixtures**: every story adds entries to `src/contracts/fit-fixtures.json`; the format gains an optional `surface` (SW-359 or SW-365, whichever lands first).
9. **`LayoutRules.cs` and `LayoutCatalogue.cs`**: every story edits both. Each story adds its own method to `LayoutRules` and touches only its own layout's entry in the catalogue, so the edits do not overlap line by line, but they must be serialised or merged with care (see the table).

Suggested order: SW-359 (surface) and the `plain` fill first; then SW-358, SW-365, SW-362, SW-363, SW-366, SW-368 in any order, two at a time at most because of the shared files.

## File impact

Paths under `src/`. "all" means all seven stories.

| File | Stories |
|---|---|
| `SlideWorks.Core/Slides/LayoutCatalogue.cs` | all |
| `SlideWorks.Core/Slides/LayoutRules.cs` | 358, 362, 363, 365, 366, 368 |
| `SlideWorks.Core/Slides/LayoutDefinition.cs` | 359 |
| `SlideWorks.Core/Slides/LayoutSurface.cs`, `SlideSurface.cs` (new) | 359 |
| `SlideWorks.Core/Slides/CardScale.cs` (new) | 362 |
| `SlideWorks.Core/Slides/LinkSlot.cs` (new) | 366 |
| `SlideWorks.Core/Slides/SlideSteps.cs` | group A (358, 363, 365 depend on it) |
| `SlideWorks.Core/Look/PaletteContrast.cs`, `Contrast.cs` | 359 |
| `SlideWorks.WebApi/Features/Slides/Slide.cs`, `AddSlideRequest.cs`, `UpdateSlideRequest.cs`, `SlideEdits.cs`, `SlideChecks.cs`, `SlideCommands.cs` | 359 |
| `SlideWorks.WebApi/Features/Slides/Mcp/AddSlideTool.cs`, `UpdateSlideTool.cs` | 359 |
| `SlideWorks.WebApi/Features/Decks/Views/DeckPlaySlide.cs`, `DeckPageSlide.cs`, `Features/Decks/Mcp/GetDeckSlide.cs`, `LayoutInfo.cs`, `Features/Presenting/Views/PresenterSlide.cs` | 359 |
| `SlideWorks.WebApi/Migrations/*` (one new) | 359 |
| `contracts/layouts.md` (generated) | all |
| `contracts/fit-fixtures.json` | 358, 362, 363, 365, 366, 368 (359 adds the `surface` field) |
| `contracts/card-scale-fixtures.json` (new) | 362 |
| `contracts/templates.json` | 358, 366 |
| `contracts/sample-deck.json` | 358, 359, 365 |
| `contracts/screens/deck-play.json`, `deck-page.json`, `presenter.json` | 359 |
| `SlideWorks.Client/src/slides/slots.ts` | all |
| `SlideWorks.Client/src/slides/layouts/index.ts` | 358, 366 |
| `SlideWorks.Client/src/slides/SlideRenderer.tsx`, `SlideFrame.tsx`, `chrome/chrome.css`, `chrome/surface.css` (new) | 359 |
| `SlideWorks.Client/src/theme/tokens.css` | 359 |
| `SlideWorks.Client/src/slides/blocks/Kicker.module.css` | 359 |
| `SlideWorks.Client/src/slides/blocks/tone.ts` | 359 (inverse surface), 362 and 363 (`plain`) |
| `SlideWorks.Client/src/slides/blocks/Stamp.tsx`, `Stamp.module.css` | 363 |
| `SlideWorks.Client/src/slides/blocks/Pin.tsx`, `Pin.module.css` (new) | 362 |
| `SlideWorks.Client/src/slides/layouts/StatementLayout.tsx`, `.module.css` (new) | 358 |
| `SlideWorks.Client/src/slides/layouts/Card.tsx`, `Card.module.css`, `CardsLayout.tsx`, `CardsLayout.module.css`, `NumberedCardsLayout.tsx`, `NumberedCardsLayout.module.css`, `cardScale.ts` (new) | 362 |
| `SlideWorks.Client/src/slides/layouts/ComparePanel.tsx`, `ComparePanel.module.css`, `CompareLayout.tsx` | 363 |
| `SlideWorks.Client/src/slides/layouts/QuoteLayout.tsx`, `QuoteLayout.module.css` | 365 |
| `SlideWorks.Client/src/slides/layouts/LinksLayout.tsx`, `.module.css` (new), `slides/safeHref.ts` (new) | 366 |
| `SlideWorks.Client/src/player/PlayerStage.tsx` | 366 |
| `SlideWorks.Client/src/slides/layouts/ImageLayout.tsx`, `ImageLayout.module.css` | 368 |
| `SlideWorks.Client/src/api/types.ts` | 359 |
| `SlideWorks.Client/src/slides/layouts/CardLayouts.fixtures.ts`, `CardLayouts.spec.tsx`, `CardsLayout.spec.tsx` | 362 |
| `SlideWorks.Client/src/slides/layouts/ContentLayouts.fixtures.ts`, `ContentLayouts.spec.tsx` | 359, 363, 365 |
| `SlideWorks.Client/src/slides/layouts/StatementLayouts.spec.tsx` | 358, 359, 365 |
| `SlideWorks.Client/src/slides/layouts/CompareLayout.spec.tsx` | 363 |
| `SlideWorks.Client/src/slides/layouts/DataLayouts.fixtures.ts`, `DataLayouts.spec.tsx` | 368 |
| `SlideWorks.Client/src/slides/layouts/LinksLayout.spec.tsx` (new) | 366 |
| `SlideWorks.Client/src/slides/SlideFrame.spec.tsx` | 359 |
| `SlideWorks.Client/src/player/Player.spec.tsx` | 366 |
| `SlideWorks.UnitTest/Slides/SlideValidatorTests.cs`, `FitFixturesTests.cs`, `LayoutCatalogueTests.cs`, `LayoutsDocTests.cs` | all |
| `SlideWorks.UnitTest/Slides/SlideStepsTests.cs` | 358, 363, 365 |
| `SlideWorks.UnitTest/Slides/LayoutsDoc.cs` | 359 (Surface column), 366 (link kind) |
| `SlideWorks.UnitTest/Look/BuiltInPaletteContrastTests.cs` | 359 |
| `SlideWorks.WebApi.Tests/Slides/AddSlideTests.cs`, `UpdateSlideTests.cs` | 358, 359, 366 |
| `SlideWorks.WebApi.Tests/Slides/SlideSurfaceTests.cs` (new) | 359 |
| `SlideWorks.WebApi.Tests/Contracts/ScreenRecordShapeTests.cs` | 359 |
| `SlideWorks.AcceptanceTests/Journeys/Sw50LayoutJourneyTests.cs` (new, one file, one test per journey) | 359 with 358 and 365; 362 with 363; 366; 368 |

Conflict hot spots: `LayoutCatalogue.cs`, `LayoutRules.cs`, `slots.ts`, `fit-fixtures.json`, `SlideValidatorTests.cs` and `layouts.md` are touched by every story, and group A touches the same files. Give each of those one owner per wave, or have each story add its own partial-class file (`LayoutRules.Statement.cs`, …) so only the catalogue and `slots.ts` are shared. `layouts.md` is generated: regenerate it once per wave, never merge it by hand.

## Decisions recorded on SW-50 (recommended, 17 Nov)

| No. | Story | Choice |
|---|---|---|
| D39 | SW-359 | A slide field `surface`; layouts declare light, dark or either |
| D40 | SW-359 | statement, quote, bullets and big-number can be dark |
| D41 | SW-359 | The frame re-points light tokens to the night set; dark content slides keep tracker and footer |
| D42 | SW-362 | Card type scale by a fixed character-count rule shared by server and client |
| D43 | SW-362 | `cards` with four cards is always 2×2; `numbered-cards` stays in a row |
| D44 | SW-362, SW-363 | `fill: plain` for a toned top rule or a white panel; no new tone |
| D45 | SW-363 | A panel holds exactly one of points, rows, line; panels keep content height; one step for both stamps |
| D46 | SW-365 | The quote's stat has no timeline |
| D47 | SW-366 | `link` slot kind (http or https only); QR drawn by the join slide's component |
| D48 | SW-368 | `frame` and `position` slots; no sniffing; right only |

## Open points

- The step-target mechanism and the emphasis, icon and callout slot types are group A's. B's catalogue entries wait for their names.
- Whether `statement` should hide the tracker (the source deck does on slides 26 and 28) is left to SW-367's per-slide tracker switch.
- A `caution` tone for a third rule colour (the source deck's orange) needs two palette tokens and is not in this design.
