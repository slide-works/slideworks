# Design

Mockups and design decisions, with one folder per Feature.

| Folder | Feature | Status |
|---|---|---|
| [`sw-1-foundation/`](sw-1-foundation/) | SW-1 Foundation: agent-built web decks over MCP | Mockups reviewed, decisions D3–D9 settled, built on the SW-1 branch |
| [`sw-2-templates-palettes/`](sw-2-templates-palettes/) | SW-2 Templates and palettes (also covers SW-15 Wada palettes and SW-50 uploaded templates) | Built (SW-2's sections; Wada palettes and uploaded templates are still to come) |

## SW-1 Foundation

Open [`sw-1-foundation/sw-1-mockups.html`](sw-1-foundation/sw-1-mockups.html) in a browser to see everything on one page. The page is interactive: the player's previous and next buttons work, and clicking a slide on the deck page shows its speaker notes. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what SW-1 ships, and where later Features plug in | [01-scope](sw-1-foundation/png/sw-1-01-scope.png) |
| Decks library | [02-decks-library](sw-1-foundation/png/sw-1-02-decks-library.png) |
| Deck page: slide grid, notes, activity log | [03-deck-page](sw-1-foundation/png/sw-1-03-deck-page.png) |
| Sharing dialog | [04-sharing](sw-1-foundation/png/sw-1-04-sharing.png) |
| Player, laptop and phone | [05-player](sw-1-foundation/png/sw-1-05-player.png) |
| The 17 building blocks, shown as a 16-slide sample deck | [06-building-blocks](sw-1-foundation/png/sw-1-06-building-blocks.png) |
| The agent's side: an MCP session and the tools | [07-agent-mcp](sw-1-foundation/png/sw-1-07-agent-mcp.png) |
| Design questions and the answers given | [08-questions-answered](sw-1-foundation/png/sw-1-08-questions-answered.png) |

### Decisions (2026-11-16)

- **D3. Slide model:** a slide is a layout plus typed slots. For example, `practice` has `do`, `comes_back`, `avoid` and `callout`. The server checks every slot against the layout's schema and rejects a slide it can't render, naming the field that broke.
- **D4. Default look:** light content slides with cards. The title, divider and closing slides are dark. Cobalt accent, Plus Jakarta Sans headings and Inter body text, with no org branding.
- **D5. Talk structure:** the part tracker, footer and progress bar are part of the default theme. `create_deck` takes `parts` and each slide sets `part`.
- **D6. Sharing:** agents have full sharing control through `set_sharing`, the same as the owner in the UI. Every change shows in the deck's activity log with the agent's name.
- **D7. Activity log:** the deck page shows who changed what, when, and through which tool. Restoring old versions comes later.
- **D8. Blocks:** all 17 ship in SW-1: title, divider with journey, bullets, two-column, practice, compare, big-number, quote, image, table, chart, gantt, numbered-cards, rules, cards with chips and badges, callout, and close.

- **D9. Engine:** the deck engine is ported as React: a `Player` component (scale, steps, `#slide.step` links, keys, click and swipe) and one React component per layout, rendered from the slot JSON. No slide library is wrapped.

The built screens' screenshot baselines live next to their component tests (`src/SlideWorks.Client/src/**/*.spec.tsx-snapshots/`) and come only from CI's Playwright container. Compare them with these mockups when a baseline changes.

## SW-2 Templates and palettes

Open [`sw-2-templates-palettes/sw-2-mockups.html`](sw-2-templates-palettes/sw-2-mockups.html) in a browser. The page is interactive:

- The palette picker re-renders the sample deck.
- The Sanzo Wada picker lists all 348 combinations and shows how each is mapped to roles.
- The chrome toggles, the New deck dialog and the Look panel all work.

The PNGs are screenshots of each section at 1300 px wide.

The page also covers two Features that build on SW-2: SW-15 (Sanzo Wada palettes, section 03) and SW-50 (uploaded templates and deck import, section 07). The import example uses a made-up brand, Northwind. Brand templates are user content and never ship in this repo.

| Screen | PNG |
|---|---|
| Scope: a template and a palette, and what ships where | [01-scope](sw-2-templates-palettes/png/sw-2-01-scope.png) |
| Built-in templates (talk, workshop, update) and an uploaded one | [02-templates](sw-2-templates-palettes/png/sw-2-02-templates.png) |
| Built-in palettes and their colour tokens | [03-palettes](sw-2-templates-palettes/png/sw-2-03-palettes.png) |
| Sanzo Wada combinations as palettes (SW-15) | [04-wada-palettes](sw-2-templates-palettes/png/sw-2-04-wada-palettes.png) |
| Template chrome: tracker, footer, logo, draft stamp, progress | [05-chrome](sw-2-templates-palettes/png/sw-2-05-chrome.png) |
| Deep-dive slides | [06-deep-dives](sw-2-templates-palettes/png/sw-2-06-deep-dives.png) |
| Colour roles, tone panels and the contrast check | [07-colour-roles](sw-2-templates-palettes/png/sw-2-07-colour-roles.png) |
| Uploading a template and importing one from a deck (SW-50) | [08-your-own-template](sw-2-templates-palettes/png/sw-2-08-your-own-template.png) |
| New deck dialog and the deck page's Look panel | [09-in-the-app](sw-2-templates-palettes/png/sw-2-09-in-the-app.png) |
| Share card | [10-share-card](sw-2-templates-palettes/png/sw-2-10-share-card.png) |
| The agent's side: MCP session and tools | [11-agent-mcp](sw-2-templates-palettes/png/sw-2-11-agent-mcp.png) |
| Design questions and the answers given | [12-questions-answered](sw-2-templates-palettes/png/sw-2-12-questions-answered.png) |

### Decisions (2026-11-16)

- **D1. No themes:** a deck's look is a **template** plus a **palette**, and nothing else.
  - The template holds layouts allowed, chrome defaults, outline, house style, fonts, radius and shadow, title art, ornament and logo. It can also hold its own palette, which it can lock.
  - The palette holds every colour: surfaces, ink, the accent family, roles (positive, negative, caution, deep dive), dark slides, and panel surface and ink pairs.
  - A template holds no colours, and a palette holds no fonts.
- **D2. Built-in palettes:** Cobalt (the default, SW-1's D4 colours), Ember, Plum and Midnight. Each passes a contrast test that fails the build if any pair drops below its bar.
- **D3. Changing colours:** a deck changes colour only by picking a palette. There is no free colour picker, so custom colours come through an uploaded template (SW-50).
- **D4. Deep dives:** a per-slide `deep_dive` flag. Dividers list a part's deep dives automatically, and the notes start with "Deep dive. Skip if short of time."
- **D5. Renderer:** the headless renderer moves from SW-3 into SW-2, and SW-3 keeps lint. Changed later by D21: an agent renders its own slide previews from the per-slide URLs and instructions `preview_deck` returns, and the server renders only images it serves itself, which today is the share card.
- **D6. Measure:** the share of new decks that use a listed template and palette with no other override, counted from the activity log.
- **D7. Three Features:** SW-2 ships the built-in templates and palettes. SW-15 adds the Sanzo Wada palettes, and SW-50 adds uploaded templates and deck import.

Still open:

- **D8:** what a template is. The recommendation is the structure, type, shape and art plus an optional palette, with no pre-made placeholder slides.
- **D9:** switching after creation. The recommendation is that both can change at any time; slides with a layout outside the template still render, with a warning.

Settled on the other Features:

- **SW-15:** automatic role mapping with contrast fixes, and the ornament keeps the combination exactly as printed (D1). The data comes from mattdesl/dictionary-of-colour-combinations, MIT licence (D3).
- **SW-50:**
  - Uploaded templates may include custom layouts as HTML with typed slots, reviewed before publishing (D1).
  - The routes in are an HTML importer plus `create_template` for agents (D2).
  - Templates are shared the same way as decks (D3).

