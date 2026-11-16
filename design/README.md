# Design

Mockups and design decisions, with one folder per Feature.

| Folder | Feature | Status |
|---|---|---|
| [`sw-1-foundation/`](sw-1-foundation/) | SW-1 Foundation: agent-built web decks over MCP | Mockups reviewed, decisions D3–D8 settled |

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

Still open: whether to port the existing thin HTML deck engine or wrap an open-source slide library such as reveal.js or Slidev. A spike will decide.
