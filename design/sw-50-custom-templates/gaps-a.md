# SW-50 layout gaps, group A: build spec

Slots on existing layouts, chrome and parts, and the small ones. Ten child items of SW-50, designed on 2026-11-17 against `main` at `21160cc`. Mockups: [`sw-50-gaps-a.html`](sw-50-gaps-a.html), PNGs in [`png/`](png/) named `sw-50-gaps-a-NN-<slug>.png`.

Not built. Group B (SW-358, 359, 362, 363, 365, 366, 368) and group C (SW-369) are specified elsewhere and reuse the mechanisms in [Shared mechanisms](#shared-mechanisms).

## What binds every story

- A slide is a layout plus typed slots. `LayoutCatalogue` is the one definition; `SlideValidator`, `list_layouts`, `list_blocks` and the generated `src/contracts/layouts.md` come from it, and the client's `slots.ts` mirrors it.
- Slot text is plain text. Nothing here parses Markdown or HTML, and nothing uses `dangerouslySetInnerHTML`.
- Colours and fonts in `src/SlideWorks.Client/src/slides/**` are `var(--sw-…)` tokens only.
- Body text fit is checked on the server (`SlideFit`, `LayoutRules`). Each change to what fits adds an entry to `src/contracts/fit-fixtures.json`, which `FitFixturesTests` checks on the server and the CT fit cases render on the client.
- Hidden steps stay rendered (`Reveal`: visibility and opacity, never `display:none`).
- A stored slide is never rewritten. Every new slot is optional, and a slide without the new slots validates, counts steps and renders exactly as before. `Slide.Steps` is stored when a slide is written, so no stored slide gains or loses a step.
- After any catalogue change: regenerate `layouts.md` (`UPDATE_LAYOUTS_DOC=1 dotnet test src/SlideWorks.UnitTest --filter "FullyQualifiedName~LayoutsDoc"`).

Decisions recorded on SW-50 as recommended (they stand unless changed there): D18 emphasis as a parallel slot (SW-360), D19 the icon set and names and D20 where icons go (SW-361), D21 the callout's `reveal` (SW-357, SW-364), D22 step plans and the strike-and-answer table (SW-364), D23 marked parts and no per-slide tracker switch (SW-367), D24 the journey's shrink-then-break rule (SW-356), D25 notes keep the two marks they already have (SW-371), D26 `preview_deck`'s URL template (SW-372), D27 widen `talk` (SW-373), D28 the nine limits (SW-370).

Sizes: S is under about 150 changed lines, M 150–500, L over 500, tests included.

---

## SW-357 Callout slot on every content layout

PNG: `sw-50-gaps-a-01-callout.png`.

### Slide model

Add `Opt("callout", new BlockSlot(Callout))` to: `cards`, `numbered-cards`, `rules`, `two-column`, `compare`, `image`, `table`, `chart`, `gantt`, `big-number`. `bullets` and `practice` keep theirs. Not added to `quote`, `title`, `divider`, `close`, `join`, `poll`, `qa`.

The `callout` block gains one optional field (used by SW-364):

| Field | Kind | Required | Values |
|---|---|---|---|
| `callout.tone` | enum | yes | unchanged |
| `callout.fill` | enum | no | unchanged |
| `callout.text` | text | yes | up to 160 (unchanged) |
| `callout.reveal` | enum | no | `with-slide` (default) or `last` |

### Placement

The callout is the last child of `.sw-body`, full width (1680 px), 30 px under the content. The content above takes the height left:

- `cards`, `numbered-cards`, `compare`, `image`, `chart`, `gantt`: the grid, panels or media get `flex: 1; min-height: 0` in an inner wrapper and shrink. (Group B's SW-362 makes cards fill the height; with a callout "the height" is what is left above it.)
- `rules`, `two-column`, `table`, `bullets`: content at the top, callout directly under it.
- `big-number`: the grid shrinks; the value's fit text already follows its column.

### Validator and fit

`LayoutRules.CalloutHeight(callout, 1680)` already exists (30 px gap + 56 px padding + lines × 37.8 px: 124 px for one line, 162 px for two). Make it `internal` and add it to each layout's body height when a callout is present:

| Layout | Rule |
|---|---|
| `rules`, `two-column`, `compare`, `table` | existing rule's height + `CalloutHeight` ≤ 620 |
| `cards`, `numbered-cards` | new rules `LayoutRules.Cards` and `LayoutRules.NumberedCards`, run **only when a callout is present**: tallest card + `CalloutHeight` ≤ 620. Card height = 68 px padding + badge row (0, or 30 px per badge line + 18 px) + title lines × 39.1 px + 14 px + text lines × 33.6 px + chips (0, or 20 px + 46 px per chip row), measured at the card's inner width ((1680 − 32 × (n − 1)) / n − 72 px). Take the exact figures from `Card.module.css` and `NumberedCardsLayout.module.css` when building |
| `image`, `chart`, `gantt`, `big-number` | no text rule: the media shrinks. The CT fit case asserts the media is at least 300 px tall with a two-line callout |

Error text, in the existing form: `cards.cards: too much text to fit on the slide with the callout (about 512 px of 458 px); shorten the cards or the callout`. For layouts that already have a rule the existing message stands.

New `fit-fixtures.json` entries (each with a `grow` field and `refusedBy: "fit"`): `cardsCallout`, `numberedCardsCallout`, `rulesCallout`, `twoColumnCallout`, `compareCallout`, `tableCallout`. Render-only CT cases (no grow): `imageCallout`, `chartCallout`, `ganttCallout`, `bigNumberCallout`.

### MCP

`list_layouts` shows `callout` in each layout's schema, and `layouts.md` in each table. No new error codes.

### Files

- Server: `src/SlideWorks.Core/Slides/LayoutCatalogue.cs`, `LayoutRules.cs`.
- Client: new `src/SlideWorks.Client/src/slides/blocks/BodyCallout.tsx` (+ `.module.css`): wraps `Callout`, takes `callout`, `step` and `at`, and draws it through `Reveal` when `callout.reveal` is `last`. Used by `BulletsLayout`, `PracticeLayout` (unchanged look), `CardsLayout`, `NumberedCardsLayout`, `RulesLayout`, `TwoColumnLayout`, `CompareLayout`, `ImageLayout`, `TableLayout`, `ChartLayout`, `GanttLayout`, `BigNumberLayout` and their `.module.css`. `slots.ts`: `callout?: CalloutBlock` on ten slot types and in `SLOT_KEYS`; `CalloutBlock.reveal`.
- Contracts: `fit-fixtures.json`, `layouts.md`.

### Tests

- Unit: `FitFixturesTests` picks up the new fixtures; `LayoutCatalogueTests` gains "every content layout has a callout slot".
- WebApi: `AddSlideTests` — `AddSlide_WhenCardsWithCallout_ShouldAccept`, `AddSlide_WhenCardsAtMaxWithCallout_ShouldRefuseWithFitMessage`; `DeckReadToolsTests` — `ListLayouts_ShouldShowCalloutOnEveryContentLayout`.
- CT: `CardLayouts.spec.tsx`, `ContentLayouts.spec.tsx`, `DataLayouts.spec.tsx`, `CompareLayout.spec.tsx` render each `*Callout` fixture and its wide twin inside x 120–1800, y 150–996. New screenshot baselines: one per layout with a callout (10). Existing baselines must not change.
- Acceptance: `Sw50CalloutJourney` — an agent adds a cards slide with a callout and the player shows it under the cards.

### Compatibility and size

No stored slide changes. A slide without a callout skips the new cards rules. **M** (about 450 lines; most of it CSS and fixtures).

---

## SW-360 Inline emphasis in headlines, quotes and statements

PNG: `sw-50-gaps-a-02-emphasis.png`.

### Decision

A parallel `emphasis` slot, not a marker grammar inside the text. The text slot stays exactly the text shown, so limits, `SlideFit`, `useFitText`, search and old readers see the same string. The emphasis names a phrase that must already be in the text.

### Slide model

`LayoutDefinition` gains a constructor argument `emphasis: IReadOnlyList<string>?`: the names of the layout's top-level text slots that may carry emphasis, the first being the default. When it is set, the definition appends one field to the layout:

| Slot | Kind | Required | Limits |
|---|---|---|---|
| `emphasis` | list of object | no | 1–3 items |
| `emphasis[].text` | text | yes | up to 80 chars |
| `emphasis[].tone` | enum | yes | `accent`, `positive`, `negative`; aliases `info`, `good`, `bad` |
| `emphasis[].in` | enum | no | one of the layout's emphasis slots; default the first |

Layouts in this story:

| Layout | Emphasis slots |
|---|---|
| every layout with `headline` under `SlideHeading` (`bullets`, `two-column`, `practice`, `compare`, `image`, `table`, `chart`, `gantt`, `numbered-cards`, `rules`, `cards`) | `headline` |
| `big-number` | `headline`, `caption` |
| `quote` | `quote` |
| `close` | `headline`, `sub` |

Only display text of 28 px or more: tone colours clear 3:1 there in every built-in palette (checked: the lowest is Cobalt's `good` on `paper` at 3.10). Body text, cards, callouts and captions take no emphasis.

### Validator

New `LayoutRules.Emphasis`, run by `LayoutDefinition.Validate` after the schema and before the layout's own rule. For each entry, with `slot` = `in` or the default:

1. `slot` is present, else `quote.emphasis[0].in: quote.sub is not set`.
2. `text` occurs in the slot's value exactly once (ordinal, case-sensitive), else `quote.emphasis[0].text: "mass unemployed" is not in quote.quote` or `quote.emphasis[0].text: occurs 2 times in quote.quote; use a longer phrase that occurs once`.
3. No two entries on the same slot overlap, else `bullets.emphasis[1].text: overlaps emphasis[0]`.

`update_slide` validates the merged slots, so an edit that removes the phrase is refused by rule 2 until the entry is removed too.

Fit: none. The visible characters are the slot's own text; nothing is added or removed. No fit fixture.

### Rendering

New `src/SlideWorks.Client/src/slides/blocks/Emphasised.tsx`: `Emphasised({ text, spans })` finds each phrase with `indexOf`, splits the string and returns text nodes and `<em data-tone="…" className={styles.em}>`. Pure helper `emphasisFor(slots, slotName, defaultSlot)` in `slides/emphasis.ts` picks the entries for one slot. `Emphasised.module.css`:

- light surface: `accent` → `var(--sw-accent)`, `positive` → `var(--sw-good)`, `negative` → `var(--sw-bad)`;
- dark surface (`.sw-frame[data-dark='true']`): `accent` → `var(--sw-accent-glow)`, `positive` → `color-mix(in srgb, var(--sw-good) 55%, var(--sw-night-ink))`, `negative` → `color-mix(in srgb, var(--sw-bad) 55%, var(--sw-night-ink))`. Ember's and Plum's `bad` on `night` is under 3:1 unmixed (2.80, 2.93); the mix clears it. No new palette token.
- `font-style: normal`, weight inherited.

`SlideHeading` takes an optional `emphasis` prop and renders the headline through `Emphasised`. `useFitText` is unaffected: the element's text content is the same.

### MCP

`list_layouts` shows `emphasis` in the schema with `in` as an enum of that layout's slots. `layouts.md` gains the rows and one sentence in the header: "Emphasis is a separate slot that names a phrase in the text; the text itself never carries markup."

### Files

- Server: `LayoutDefinition.cs`, `LayoutCatalogue.cs`, `LayoutRules.cs`.
- Client: `blocks/Emphasised.tsx` (+ css), `emphasis.ts`, `blocks/SlideHeading.tsx`, `layouts/QuoteLayout.tsx`, `BigNumberLayout.tsx`, `CloseLayout.tsx`, and one line in each layout that calls `SlideHeading`; `slots.ts` (`Emphasis` type, the slot on 14 types, `SLOT_KEYS`).

### Tests

- Unit: `SlideValidatorTests` — one case per rule and message, an alias tone, a phrase with regex characters, a phrase with combining marks.
- WebApi: `AddSlideTests.AddSlide_WhenEmphasisPhraseIsInTheQuote_ShouldAccept`; `UpdateSlideTests.UpdateSlide_WhenTextNoLongerHoldsThePhrase_ShouldRefuse`; one wire test (`McpWire`) that `tone: "bad"` binds.
- CT: `Emphasised.spec.tsx` (split, two spans, tone attribute, no HTML: a phrase of `<b>` renders as text); `SlideHeading.spec.tsx`; screenshot baselines `quote-emphasis`, `bullets-headline-emphasis`, `close-emphasis` (dark).
- Guard: `slidesUseTokens.test.ts` already covers the new CSS.
- Acceptance: `Sw50EmphasisJourney` — a quote with an accent phrase shows the phrase in the accent colour in the player.

### Compatibility and size

Optional slot; nothing stored changes. **M** (about 400 lines).

---

## SW-361 Icons on cards, bullets, columns and compare panels

PNG: `sw-50-gaps-a-03-icons.png`.

### Decisions

- One built-in set of 48 line icons with our own names. The names live in `src/contracts/icons.json` (`{ "icons": [{ "name": "coins", "words": ["money", "cost"] }, …] }`), shared by server and client.
- The drawings come from the `lucide-react` package (ISC licence), pinned to an exact version, through one map file from our name to its component. Our names do not depend on the package's names, so the set can be redrawn later.
- An icon has no colour of its own. It is drawn with `currentColor`: accent on a plain surface, the panel's ink on a toned card or panel.
- No icons on `numbered-cards` or `rules` (the numeral is the mark), none in chips (no slide in the evidence has one).

The 48 names: `bolt`, `book`, `bulb`, `calendar`, `chart`, `chat`, `check`, `check-circle`, `clock`, `cloud`, `code`, `coins`, `cycle`, `database`, `document`, `eye`, `factory`, `flag`, `folder`, `gauge`, `gear`, `globe`, `handshake`, `heart`, `key`, `layers`, `link`, `list`, `lock`, `mail`, `map-pin`, `people`, `person`, `phone`, `puzzle`, `robot`, `rocket`, `scale`, `search`, `shield`, `star`, `target`, `terminal`, `ticket`, `trophy`, `warning`, `wrench`, `x-circle`.

### Slide model

New slot type `IconSlot` (`src/SlideWorks.Core/Slides/IconSlot.cs`): a string that must be a name in `IconCatalogue` (`src/SlideWorks.Core/Slides/IconCatalogue.cs`, reads the embedded `icons.json`, as `TemplateCatalogue` reads `templates.json`). Schema: `{type: "string", enum: [...]}`. Error: `cards.cards[0].icon: unknown icon "coin"; did you mean "coins"? list_blocks lists the names` (the suggestion is the nearest name by edit distance ≤ 2, left out when there is none).

New inline block `icon` in `LayoutCatalogue.Blocks`, so `list_blocks` returns the names.

| Slot | Kind | Required | Drawn |
|---|---|---|---|
| `cards.cards[].icon` | icon | no | 44 px at the top of the card, before the badge |
| `bullets.items[]` object form | `{icon?, lead?, text}` | `text` yes | 40 px in the bullet mark's place |
| `two-column.left/right.items[]` object form | `{icon?, lead?, text}` | `text` yes | 34 px in the mark's place |
| `two-column.left/right.icon` | icon | no; needs `title` | 34 px beside the column title |
| `image.points[]` | text, or `{icon, text}` | — | 34 px in the mark's place |
| `compare.failure/success.icon` | icon | no | 34 px beside the label |

The list item object form changes from `{lead, text}` (both required) to `{icon?, lead?, text}`; a string stays valid. `LeadOrText(...)` in the catalogue becomes `ListItem(...)` and is used by `bullets` and `Column()`. Error when `two-column.left.icon` has no title: `two-column.left.icon: needs left.title`.

### Fit

An icon sits in space the mark, label row or badge row already has, so text rules do not change, with two exceptions the rules must count:

- a card with an icon and no badge gains the 62 px top row (the cards rule from SW-357 counts "badge or icon row");
- an item with an icon is indented 68 px, not 52 px (`bullets`: measure at 1432 px, not 1448 px, when any item has an icon).

Fixtures: `bulletsIcons` (8 items with icons at the limit, `refusedBy: "fit"`), `cardsIcons` (render-only with SW-357's rule).

### Rendering

New `src/SlideWorks.Client/src/slides/blocks/Icon.tsx` (+ css): `Icon({ name, size })` looks the name up in `blocks/icons.ts` (our name → `lucide-react` component, named imports so the bundle holds 48 icons), draws it with `stroke: currentColor`, `aria-hidden`. An unknown name draws nothing. `src/SlideWorks.Client/package.json` adds `lucide-react` at an exact version (keep the release-age guard).

### Files

- Server: `IconSlot.cs`, `IconCatalogue.cs`, `LayoutCatalogue.cs`, `LayoutRules.cs`, `SlideFit.cs` (`Words` ignores `icon`), `SlideWorks.Core.csproj` (embed `icons.json`).
- Contracts: `icons.json`, `layouts.md`, `fit-fixtures.json`.
- Client: `blocks/Icon.tsx`, `blocks/icons.ts`, `blocks/LeadText.tsx`, `layouts/Card.tsx`, `BulletsLayout.tsx`, `TwoColumnColumn.tsx`, `ImageLayout.tsx`, `ComparePanel.tsx` and their css; `slots.ts` (`IconName`, `ListItem`).

### Tests

- Unit: `IconCatalogueTests` (48 names, unique, kebab-case); `SlideValidatorTests` (unknown icon message with and without a suggestion; `{text}` alone accepted; icon without title refused).
- Client unit: `icons.test.ts` — every name in `icons.json` has a drawing and the map has no extra name.
- WebApi: `DeckReadToolsTests.ListBlocks_ShouldListTheIconNames`; one wire test for an icon on a card.
- CT: `Icon.spec.tsx`; screenshot baselines `cards-icons`, `bullets-icons`, `compare-icons`, `cards-icons-toned` (icon in the panel's ink).
- Acceptance: none of its own; covered by `Sw50CalloutJourney`'s cards slide carrying an icon.

### Compatibility and size

Optional slots; `{lead, text}` items stay valid. **M** (about 450 lines plus the dependency).

---

## SW-364 Step reveals on more layouts

PNG: `sw-50-gaps-a-04-steps.png`.

### Slide model

`LayoutDefinition`'s `stepsFrom: string?` becomes `steps: StepPlan?`. `StepPlan` (`src/SlideWorks.Core/Slides/StepPlan.cs`) is an ordered list of step sources, each with a count function over the slots and a phrase for the docs:

- `StepPlan.List("items")`: one step per item when `reveal` is `one-by-one` (today's behaviour);
- `StepPlan.Fixed(1, "the success panel")`: that many steps when `reveal` is `one-by-one`;
- `StepPlan.When("detail", 1, "the detail")`: as `Fixed`, but only when the named slot is present;
- `.Then(...)` chains sources.

Every layout with a `callout` slot gets one more source automatically: 1 step when `callout.reveal` is `last`, whatever `reveal` is.

`SlideSteps.Count` becomes `layout.Steps?.Count(slots) ?? 0`. `LayoutDefinition.StepsFrom` stays as a derived property (the first list source's name, else null) so `list_layouts`' `stepsFrom` field keeps its meaning; `LayoutInfo` gains `steps`: the plan's phrase, which is also `layouts.md`'s Steps column.

| Layout | `reveal` values | Steps when not `all` | Step n shows |
|---|---|---|---|
| `bullets`, `cards`, `numbered-cards`, `rules` | `all`, `one-by-one` | one per item | item n (unchanged) |
| `compare` | `all`, `one-by-one` (new slot) | 1 | the success panel |
| `two-column` | `all`, `one-by-one` (new slot) | 1 | the right column |
| `image` | `all`, `one-by-one` (new slot) | one per point; 0 without `points` | point n |
| `big-number` | `all`, `one-by-one` (new slot) | 1 with `detail`, else 0 | the detail |
| `table` | `all`, `one-by-one`, `strike-and-answer` (new slot) | one per row | see below |
| any layout with a callout | — | +1 when `callout.reveal` is `last` | the callout, after every other step |

`table` with `strike-and-answer`: every left cell shows from the start and every right cell is hidden. Step n shows row n's right cell and strikes through its left cell; rows before n dim to 45% opacity. Needs exactly two columns: `table.reveal: 'strike-and-answer' needs exactly 2 columns (got 3)`. With `one-by-one`, step n shows row n whole.

A second big number (slide 14) is not added: that slide is a diagram with two figures and belongs to SW-369 or a custom layout.

### Fit

None: hidden parts keep their space, so the fit is that of the fully revealed slide.

### Rendering

`Reveal` is unchanged. `CompareLayout`, `TwoColumnLayout`, `ImageLayout`, `BigNumberLayout` and `TableLayout` wrap the late part in `Reveal at={n}`. `TableLayout` sets `data-struck` and `data-dim` on rows from `step`; `TableLayout.module.css` styles them with `text-decoration: line-through`, `color: var(--sw-muted)` and `opacity`. `BodyCallout` (SW-357) takes `at` = the layout's other steps + 1; a helper `calloutStep(otherSteps, callout)` in `slides/steps.ts` returns it.

### MCP

`add_slide` and `update_slide` already return `steps`. `list_layouts` shows `reveal` on the five layouts and `steps` text per layout.

### Files

- Server: new `StepPlan.cs`; `LayoutDefinition.cs`, `LayoutCatalogue.cs`, `SlideSteps.cs`, `LayoutRules.cs` (the two-column check in `Table`), `src/SlideWorks.WebApi/Features/Decks/Mcp/LayoutInfo.cs`, `src/SlideWorks.UnitTest/Slides/LayoutsDoc.cs`.
- Client: `slides/steps.ts`, the five layouts above, `TableLayout.module.css`, `slots.ts` (`reveal` on five types; `TableReveal`).

### Tests

- Unit: `SlideStepsTests` — one case per row of the table above, each with and without `callout.reveal: "last"`; a stored bullets slide with a callout and `one-by-one` still counts its items only.
- WebApi: `AddSlideTests.AddSlide_WhenCompareRevealsOneByOneWithLastCallout_ShouldReturnTwoSteps`; `AddSlide_WhenStrikeAndAnswerHasThreeColumns_ShouldRefuse`.
- CT: `CompareLayout.spec.tsx`, `DataLayouts.spec.tsx`, `ContentLayouts.spec.tsx` — at step 0 the late part has `data-revealed="false"` and still has its box; table rows carry `data-struck` and `data-dim` as specified. Screenshot baselines `table-strike-step-2`, `compare-step-0`.
- Acceptance: `Sw50StepsJourney` — in the player, clicking through a strike-and-answer table strikes each claim in turn and the last click shows the callout.

### Compatibility and size

`reveal` and `callout.reveal` are optional and default to today's behaviour. `Slide.Steps` is stored, so old slides keep their counts. Depends on SW-357 for `BodyCallout`. **M** (about 450 lines).

---

## SW-370 Slot length limits a real deck hit

PNG: `sw-50-gaps-a-05-limits.png`.

| Slot | Was | Now | Guard |
|---|---|---|---|
| `divider.sub` | 80 | 110 | 40 px in 1500 px is 47 characters a line at 0.56 em: three lines at most. `DividerLayout` already drops deep-dive pills to keep the stack above y 996; the CT case `divider-max` adds the 110-character sub with eight parts |
| `badge` block (`cards[].badge`) | 20 | 36 | wraps to two lines in a three-card row; the cards rule (SW-357) counts badge lines. `Badge.module.css` line height goes from 1 to 1.25 |
| `compare.failure/success.title` | 50 | 80 | `LayoutRules.Compare` counts title lines |
| `table` cell (plain and `{text, tone}`) | 40 | 80 | `LayoutRules.Table` counts wrapped lines per row |
| `bullets.items[].lead` | 60 | 80 | `LayoutRules.Bullets` counts lead and text together |
| `quote.attribution` | 80 | 140 | 28 px across 1620 px is 103 characters a line: two lines. `QuoteLayout`'s `QUOTE_FIT` takes `lines: 6` when the attribution is over 100 characters |
| `two-column.left/right.title` | 50 | 60 | `LayoutRules.TwoColumn` counts title lines |
| image `alt` (every `ImageSlot`) | 200 | 500 | never drawn |
| `big-number.value` | 6 | 10 | new rule `LayoutRules.BigNumber`: the number part (up to and including the last digit, as `splitValue` in `BigNumberLayout.tsx` splits it) is at most 6 characters: `big-number.value: the number part is at most 6 characters (got 8); put the rest in the caption`. The unit is drawn at 110/300 of the number, so a 6-character number and a 4-character unit fit the 820 px column at the 120 px floor |

Where group B's SW-363 (compare) or SW-365 (quote) redefines one of these slots, B's value wins and this row is dropped.

Fixtures: update `compare` (80-character titles; the existing grow fields still refuse), `tableProse` and `tableTitle` (80-character cells in a two-column table), `bulletsCallout` (80-character lead); add `bigNumberUnit` (`"999999 wks."`-style 10-character value, `refusedBy: "length"`), `quoteAttribution` (render-only CT), `dividerSub` (render-only CT). `layouts.md` regenerates with the new limits; each kept constraint has its reason in the slot's Notes column (the number-part rule, the quote's line budget).

Files: `LayoutCatalogue.cs`, `ImageSlot.cs`, `LayoutRules.cs`, `fit-fixtures.json`, `layouts.md`, `QuoteLayout.tsx`, `Badge.module.css`, the three `*.fixtures.ts`.

Tests: `FitFixturesTests`; `SlideValidatorTests` (the number-part rule; alt of 500 accepted, 501 refused); CT fit cases for the changed fixtures and their wide twins. Screenshot baselines affected: none expected, since no existing fixture text changes on screen except the fit fixtures' own cases.

Compatibility: every limit only grows, so stored slides stay valid. **S** (about 150 lines, mostly fixtures).

---

## SW-367 Parts: short and full names, a caption, an unnumbered section

PNG: `sw-50-gaps-a-06-parts.png`.

### Model

A part is a name (string, as now) or an object:

| Field | Kind | Required | Limits | Used by |
|---|---|---|---|---|
| `name` | text | yes | up to 40 chars (unchanged) | journey stop, `get_deck` |
| `short` | text | no | up to 16 chars | tracker; defaults to `name` |
| `caption` | text | no | up to 24 chars | under the journey stop, in place of "Part n" |
| `mark` | text | no | 1–2 chars | makes the part an unnumbered section |

Rules, in `DeckCommands.Parts` (messages in the existing form):

- at most 8 parts, marked ones included: `parts: at most 8 items`;
- marked parts come after every numbered part: `parts[2]: a part with a mark must come after every numbered part`;
- `parts[1].short: at most 16 characters (got 18)`, `parts[3].mark: at most 2 characters (got 3)`.

A slide's `part` stays a 1-based index into the whole list. `Deck.PartsJson` stores an array whose items are strings or objects; `DeckParts.Read` returns `IReadOnlyList<DeckPart>` (`record DeckPart(string Name, string? Short, string? Caption, string? Mark)`, new file `DeckPart.cs`) and reads a stored string as `{name}`. No migration.

### Chrome

- Tracker (`PartTracker`): one item per numbered part, showing `short ?? name`. Not drawn when the slide has no part, when its part is marked, or when the layout opts out (`LayoutDefinition.Tracker = false`, a new flag for group B's `statement`; no layout in this group sets it).
- Journey (`Journey`): one stop per numbered part, showing `name` and `caption ?? "Part n"`.
- Divider (`DividerLayout`): on a marked part the number is the `mark` and there is no journey and no deep-dive list.
- Footer and progress bar: unchanged.

### No per-slide switch

The unrecorded idea of a per-slide "hide tracker" switch is not built. The three rules above cover every slide of the source deck that hides it (cold-open slides have no part; appendix slides are in a marked part; statement slides use a layout that opts out). A switch would add a column, a migration and an argument to two tools for no slide the rules miss.

### API and MCP

- `create_deck`'s `parts` argument becomes `IReadOnlyList<JsonElement>?` with a schema of `string | {name, short?, caption?, mark?}`; its description: "Optional parts, up to 8. Each is a name of up to 40 characters, or {name, short, caption, mark}: short (16) shows on the tracker, caption (24) under the journey stop, and a mark of 1–2 characters such as 'A' makes an unnumbered section, shown on neither." `POST /api/decks` takes the same.
- `get_deck`, the play view, the deck page and the presenter view return `parts` as objects with all four fields (null when unset). This is a wire change for the SPA, which ships with the server.
- Parts can still be set only at `create_deck`. A tool to change a deck's parts is not in this story (see Open points).

### Files

- Server: `Features/Decks/DeckPart.cs` (new), `DeckParts.cs`, `DeckCommands.cs`, `CreateDeckRequest.cs`, `Mcp/CreateDeckTool.cs`, `Mcp/GetDeckResult.cs`, `Views/DeckPlayView.cs`, `Views/DeckPageView.cs`, `DeckQueries.cs`, `Features/Presenting/Views/PresenterView.cs`, `Features/Slides/SlideEdits.cs` and `SlideCommands.cs` (they only count parts), `Core/Slides/LayoutDefinition.cs` (`Tracker`).
- Contracts: `screens/deck-play.json`, `deck-page.json`, `presenter.json`, `sample-deck.json`.
- Client: `api/types.ts` (`DeckPart`), `slides/slots.ts` (`SlideDeck.parts: DeckPart[]`), `slides/SlideFrame.tsx`, `chrome/PartTracker.tsx`, `blocks/Journey.tsx`, `layouts/DividerLayout.tsx`, and the readers of `parts` that only pass it on or show a name: `SlideRenderer.tsx`, `player/Player.tsx`, `pages/RenderPage.tsx`, `pages/SpeakerPage.tsx`, `pages/deck/SlideGrid.tsx`, `presentation/Overview.tsx`, `PresenterThumb.tsx`, `presenter/PresenterTopBar.tsx`, `remote/SlidesSheet.tsx`, `look/LookPreview.tsx`, `guest/GuestFollowing.tsx`; `test-support/fixtures/*`.

### Tests

- WebApi: `CreateDeckTests` — strings only (unchanged shape in, objects out), a mix, each refusal; one wire test for object parts; `PlayViewTests` and `GetDeckTests` shapes; `DeckPageTests`.
- CT: `SlideFrame.spec.tsx` — tracker shows short names, no tracker without a part, none in a marked part; `DividerLayout.spec.tsx` — captions, the mark, no journey on a marked part. Screenshot baselines changed: `slide-frame-*` only if a fixture gains short names (keep the existing fixtures as strings so they do not change); new `divider-captions`, `divider-mark`.
- Acceptance: `Sw50PartsJourney` — a deck with short names, captions and an appendix: the tracker reads the short names, the divider shows captions, the appendix divider shows "A" and its slides have no tracker.

### Compatibility and size

Stored parts read as names. One visible change to existing decks: a slide with no part, in a deck that has parts, loses its tracker (it highlighted nothing). **L** (about 600 lines; the type change touches many files lightly).

---

## SW-356 (bug) Journey breaks a one-word part name

PNG: `sw-50-gaps-a-06-parts.png` (first, second and last slides).

Cause: `Journey.module.css` `.name` has `overflow-wrap: anywhere`, and with eight stops a column is 166 px, narrower than a 12-letter word at 18 px with 0.14 em tracking.

Fix, client only:

1. `.name`: `overflow-wrap: break-word` (a word breaks only when it cannot fit a line on its own), `hyphens: none`.
2. All names share one font size, from `--journey-name-size`. `Journey.tsx` measures after layout: while any `.name`'s `scrollWidth` exceeds its `clientWidth` and the size is above 13 px, it steps the size down by 1 px (18 → 13) and the tracking from 0.14 em to 0.08 em. At 13 px a single word of 17 capitals fits a 166 px column. A longer word breaks, as the last resort.
3. The two-line clamp and the `title` attribute stay.

Use a `ResizeObserver` as `DividerLayout` does, so hidden thumbnails measure when shown. No server change and no warning at `create_deck`.

Files: `blocks/Journey.tsx`, `blocks/Journey.module.css`. Build after or with SW-367 (same two files).

Tests: `DividerLayout.spec.tsx` — with the bug's eight parts, the "Autocomplete" stop is one line (`getClientRects` of its text node has one rect) and no name's `scrollWidth` exceeds its width; with a 30-letter word the size is 13 px and nothing overflows the slide. Screenshot baseline changed: the existing divider baselines keep 18 px (their names fit); new `divider-eight-parts`.

**S** (about 80 lines).

---

## SW-371 Speaker notes keep bold

PNG: `sw-50-gaps-a-07-notes.png`.

Finding: bold already works. `src/SlideWorks.Client/src/presentation/NotesText.tsx` (SW-4) draws `**bold**` and lines starting `- ` as a list, and both the presenter window (`NotesPanel`) and the phone (`SpeakerNotes`) use it. The rebuild lost the cues because no tool description says so. The deck page shows the raw text.

Decision: keep this two-mark grammar, which is explicit, tiny and already rendered without HTML. Notes are not slot text, so the slot rule is untouched.

Change:

1. Descriptions: `set_notes` (`Features/Slides/Mcp/SetNotesTool.cs`) and the `notes` argument of `add_slide` (`AddSlideTool.cs`; `update_slide` has no notes argument) gain: "Notes are plain text with two marks: **bold** for cues such as **Say:**, and a line that starts with '- ' for a list item. Nothing else is formatted."
2. Deck page: `pages/deck/SelectedSlide.tsx` renders notes through `NotesText`.
3. The server stores notes as given. An unpaired `**` is kept and shown as typed, as today. No validation is added.

Tests: `SetNotesTests.SetNotes_WhenTextHasBoldMarks_ShouldStoreThemAsGiven`; a tool-description assertion in `DeckReadToolsTests` or the tool-schema tests that both descriptions mention `**bold**`; client `NotesText` unit cases exist; add a deck-page CT case `selected-slide-notes-bold`; acceptance: extend `PresentingJourneyTests` with one bold cue visible as `<b>` in the presenter window.

Compatibility: none needed. **S** (about 60 lines).

---

## SW-372 preview_deck returns a short response

PNG: `sw-50-gaps-a-08-preview-deck.png`.

### Result shape

```json
{
  "render_url": "https://host/render/<deck>/{slide}#token=<token>",
  "slides": [1, 2, 3],
  "viewport": { "width": 1920, "height": 1080 },
  "wait_for": "[data-ready=true], [data-error]",
  "expires_in_seconds": 600,
  "player_url": "…",
  "share_card_url": "…",
  "instructions": "…"
}
```

`slides` is the list of slide numbers to render (all, or the ones asked for). `render_url` holds the literal `{slide}` where the number goes. `PreviewSlide` is deleted.

### Instructions (`PreviewInstructions.md`)

Step 3 becomes one loop, with the numbers as `$(seq 1 N)` when they are 1..N and a literal list otherwise:

```bash
for n in $(seq 1 44); do
  npx -y playwright@1.62.0 screenshot --viewport-size=1920,1080 \
    --wait-for-selector="[data-ready=true], [data-error]" --timeout=60000 \
    "https://host/render/<deck>/$n#token=<token>" slide-$n.png
done
```

Then a second option for a long deck: "Faster for many slides: one browser for all of them", a script of about ten lines saved as `shots.mjs` and run after `npm install --no-save playwright@1.62.0`, which opens one Chromium, visits each URL, waits for the selector and writes `slide-N.png`.

A new step: "The page reads the token from the part after `#` when it first loads and then removes it from the address. A reload shows `Render token missing`: open the full URL again."

Budget: the response for 44 slides is under 5 KB (about 3 KB: one URL with the token three times, the instructions at about 2.3 KB).

### Files

`Features/Render/DeckPreviews.cs`, `PreviewInstructions.cs`, `PreviewInstructions.md`, `Mcp/PreviewDeckResult.cs`, `Mcp/PreviewDeckTool.cs` (description), delete `Mcp/PreviewSlide.cs`; `README.md` or `docs/` wherever the result is described.

### Tests

`PreviewDeckToolTests` — `PreviewDeck_WhenDeckHas44Slides_ShouldReturnUnder5Kb` (serialised result length), `…_ShouldGiveOneUrlTemplateAndTheNumbers`, `…_WhenSlidesAreGiven_ShouldLoopOverThoseNumbers`, `…_ShouldSayAReloadLosesTheToken`, and the raw-wire test stays an object. Acceptance: the `Render` journeys that follow the instructions build each URL from the template.

Compatibility: an agent reads the result fresh on every call; nothing stores it. **S** (about 120 lines).

---

## SW-373 A built-in template that allows every layout

PNG: `sw-50-gaps-a-09-talk-template.png`.

Decision: widen `talk`; no "everything" template. In `src/contracts/templates.json`, `talk.layouts` gains `cards`, `numbered-cards`, `rules`, `table`. `practice` and `gantt` stay out (a warning, never a refusal). `workshop` and `update` are unchanged. `join` and `poll` are allowed everywhere already (`Template.Allows`).

Rule for the other groups: a story that adds a layout adds it to `talk` (and to any other template it suits) in the same change.

Files: `src/contracts/templates.json`; `talk`'s `outline` and `houseStyle` texts are unchanged.

Tests: `LayoutOutsideTemplateWarnsTests` — `AddSlide_WhenCardsOnTalk_ShouldNotWarn`, and keep a warning case using `gantt` on `talk`; `ListLayoutsByTemplateTests` and `ListTemplatesToolTests` expectations; `TemplateCatalogueTests`. Check `Sw2AgentJourneyTests` and `TenSlideBrief` for a test that relies on `cards` warning on `talk` and move it to `gantt`.

Compatibility: existing decks on `talk` get fewer warnings; nothing stored changes. **S** (about 40 lines).

---

## Shared mechanisms

What group B's and C's layouts reuse. Each is built once, by the story named.

| Mechanism | Built by | In the catalogue | In the component |
|---|---|---|---|
| Callout in the body | SW-357 | `Opt("callout", new BlockSlot(Callout))`; in the layout's rule add `LayoutRules.CalloutHeight(callout, width)` to the body height, or give the content a shrinking wrapper | `<BodyCallout callout={…} step={step} at={…} />` as the last child of `.sw-body` |
| Emphasis | SW-360 | `emphasis: ["statement", "sub"]` on the `LayoutDefinition`; the `emphasis` slot and its three checks come with it. Only for text of 28 px or more | `<Emphasised text={…} spans={emphasisFor(slots, "statement", "statement")} />` in place of the bare string; works on light and dark frames |
| Icon | SW-361 | `Opt("icon", new IconSlot())`; names from `src/contracts/icons.json` | `<Icon name={…} size={…} />`, coloured by `currentColor` |
| Steps | SW-364 | `steps: StepPlan.List("stamps")`, `StepPlan.Fixed(1, "the follow-up line")`, `StepPlan.When("stat", 1, "the stat")`, chained with `.Then(…)`. The callout's step is added automatically | `Reveal at={n} step={step}`; `calloutStep(otherSteps, callout)` for the callout |
| No tracker on a layout | SW-367 | `tracker: false` on the `LayoutDefinition` (mirrored in `slots.ts` as `NO_TRACKER_LAYOUTS`) | nothing: `SlideFrame` reads it |
| Template membership | SW-373 | add the new layout's name to `talk.layouts` in `templates.json` | — |

Order that avoids rework: SW-373 and SW-372 any time; SW-357 before SW-364 (the callout step); SW-367 before SW-356 (same files); SW-360 and SW-361 are independent of the rest. Group B's layout stories should start after SW-357, SW-360, SW-361 and SW-364 have merged their catalogue types (`StepPlan`, `IconSlot`, the `emphasis` argument), or take those four as a first wave.

## Open points

- **Changing a deck's parts.** Parts are set only at `create_deck`. The rebuilt deck cannot gain short names or captions without being recreated. A `set_parts` tool is a separate, small story; it must decide what happens to slides whose part number no longer exists.
- **Slide 14's two numbers.** Left to SW-369 or a custom layout; `big-number` keeps one value.
- **Emphasis on body text.** Not offered: the 3:1 bar only holds for large text. If asked for, it needs `good-ink` and `bad-ink` on light and new contrast pairs.
- **`qa` in every template.** `Template.Allows` lets `join` and `poll` through but not `qa`. Check whether that is intended when building SW-373.

## File impact

`C` = `src/SlideWorks.Core/Slides/`, `W` = `src/SlideWorks.WebApi/Features/`, `S` = `src/SlideWorks.Client/src/slides/`.

| File | Stories |
|---|---|
| `C/LayoutCatalogue.cs` | SW-357, SW-360, SW-361, SW-364, SW-370 |
| `C/LayoutRules.cs` | SW-357, SW-360, SW-361, SW-364, SW-370 |
| `C/LayoutDefinition.cs` | SW-360, SW-364, SW-367 |
| `C/SlideSteps.cs`, `C/StepPlan.cs` (new) | SW-364 |
| `C/IconSlot.cs`, `C/IconCatalogue.cs` (new), `C/SlideFit.cs` | SW-361 |
| `C/ImageSlot.cs` | SW-370 |
| `src/contracts/layouts.md` (generated) | SW-357, SW-360, SW-361, SW-364, SW-370 |
| `src/contracts/fit-fixtures.json` | SW-357, SW-361, SW-370 |
| `src/contracts/icons.json` (new) | SW-361 |
| `src/contracts/templates.json` | SW-373 |
| `src/contracts/screens/*.json`, `sample-deck.json` | SW-367 |
| `S/slots.ts` | SW-357, SW-360, SW-361, SW-364, SW-367 |
| `S/blocks/BodyCallout.tsx` (new) | SW-357; used by SW-364 |
| `S/blocks/Emphasised.tsx` (new), `S/emphasis.ts` (new), `S/blocks/SlideHeading.tsx` | SW-360 |
| `S/blocks/Icon.tsx`, `S/blocks/icons.ts` (new), `S/blocks/LeadText.tsx` | SW-361 |
| `S/steps.ts` (new) | SW-364 |
| `S/blocks/Badge.module.css` | SW-370 |
| `S/blocks/Journey.tsx`, `Journey.module.css` | SW-367, SW-356 |
| `S/chrome/PartTracker.tsx`, `S/SlideFrame.tsx` | SW-367 |
| `S/layouts/DividerLayout.tsx` | SW-367 |
| `S/layouts/BulletsLayout.tsx` | SW-357, SW-360, SW-361 |
| `S/layouts/CardsLayout.tsx`, `Card.tsx` | SW-357, SW-360, SW-361 |
| `S/layouts/NumberedCardsLayout.tsx`, `RulesLayout.tsx`, `ChartLayout.tsx`, `GanttLayout.tsx`, `PracticeLayout.tsx` | SW-357, SW-360 |
| `S/layouts/CompareLayout.tsx`, `ComparePanel.tsx` | SW-357, SW-360, SW-361, SW-364 |
| `S/layouts/TwoColumnLayout.tsx`, `TwoColumnColumn.tsx` | SW-357, SW-360, SW-361, SW-364 |
| `S/layouts/ImageLayout.tsx` | SW-357, SW-360, SW-361, SW-364 |
| `S/layouts/TableLayout.tsx` | SW-357, SW-360, SW-364 |
| `S/layouts/BigNumberLayout.tsx` | SW-357, SW-360, SW-364 |
| `S/layouts/QuoteLayout.tsx` | SW-360, SW-370 |
| `S/layouts/CloseLayout.tsx` | SW-360 |
| `S/layouts/*.fixtures.ts`, `*.spec.tsx` | SW-357, SW-360, SW-361, SW-364, SW-370 |
| `src/SlideWorks.Client/package.json` | SW-361 |
| `src/SlideWorks.Client/src/api/types.ts` and the `parts` readers listed under SW-367 | SW-367 |
| `src/SlideWorks.Client/src/pages/deck/SelectedSlide.tsx` | SW-371 |
| `W/Decks/DeckParts.cs`, `DeckPart.cs` (new), `DeckCommands.cs`, `CreateDeckRequest.cs`, `Mcp/CreateDeckTool.cs`, `Mcp/GetDeckResult.cs`, `Views/*`, `W/Presenting/Views/PresenterView.cs` | SW-367 |
| `W/Decks/Mcp/LayoutInfo.cs` | SW-364 |
| `W/Slides/Mcp/SetNotesTool.cs`, `AddSlideTool.cs` | SW-371 |
| `W/Render/DeckPreviews.cs`, `PreviewInstructions.cs`, `PreviewInstructions.md`, `Mcp/PreviewDeckResult.cs`, `Mcp/PreviewSlide.cs` | SW-372 |

The five stories that share `LayoutCatalogue.cs`, `LayoutRules.cs`, `slots.ts` and the layout components (SW-357, 360, 361, 364, 370) cannot run in parallel on those files. Either run them in the order SW-370, SW-357, SW-364, SW-360, SW-361 with one owner at a time, or give one task the catalogue and rules for all five and split the client work by layout family (`CardLayouts`, `ContentLayouts`, `DataLayouts`).
