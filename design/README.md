# Design

Mockups and design decisions, with one folder per Feature.

| Folder | Feature | Status |
|---|---|---|
| [`sw-1-foundation/`](sw-1-foundation/) | SW-1 Foundation: agent-built web decks over MCP | Mockups reviewed, decisions D3–D9 settled, built on the SW-1 branch |
| [`sw-2-templates-palettes/`](sw-2-templates-palettes/) | SW-2 Templates and palettes (also covers SW-15 Wada palettes and SW-50 uploaded templates) | Built (SW-2's sections; Wada palettes and uploaded templates are still to come) |
| [`sw-4-presenter/`](sw-4-presenter/) | SW-4 Presenter view and phone remote (SW-7 merged in) | Built (decisions D1–D6 settled, plan decisions D7–D17 as built; the user guide is [`docs/presenting.md`](../docs/presenting.md)) |
| [`sw-5-audience-join/`](sw-5-audience-join/) | SW-5 Audience join: QR code, join code and guest mode | Mockups reviewed, decisions D1–D8 settled, not built |

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

## SW-4 Presenter view and phone remote

Open [`sw-4-presenter/sw-4-mockups.html`](sw-4-presenter/sw-4-mockups.html) in a browser. Every mock on the page shares one presentation state, as the real windows will. Move a slide, black out the screen or turn on deep-dive skipping in the presenter window, and the audience window, the overview, the deep-dive strip and the phone all follow. The presenter window takes the keys ← → N O B D T once you click into it. The PNGs are screenshots of each section at 1300 px wide.

The phone remote was planned as its own Feature, SW-7. It is merged into SW-4 by D1.

| Screen | PNG |
|---|---|
| Scope: what SW-4 covers and what it leaves to SW-5, SW-6 and SW-173 | [01-scope](sw-4-presenter/png/sw-4-01-scope.png) |
| How the presenter window, audience window and phone share state through `DeckHub` | [02-how-it-connects](sw-4-presenter/png/sw-4-02-how-it-connects.png) |
| Starting: the deck page's Present menu, and the QR code for the phone | [03-starting](sw-4-presenter/png/sw-4-03-starting.png) |
| Presenter window and the audience window it drives | [04-presenter-window](sw-4-presenter/png/sw-4-04-presenter-window.png) |
| Overview grid, grouped by part | [05-overview](sw-4-presenter/png/sw-4-05-overview.png) |
| Skipping deep dives when running late | [06-deep-dives](sw-4-presenter/png/sw-4-06-deep-dives.png) |
| Phone: speaker mode, the slide list, picking the talk, connection lost | [07-phone-speaker-mode](sw-4-presenter/png/sw-4-07-phone-speaker-mode.png) |
| Controls on the laptop, in the presenter window and on the phone | [08-controls](sw-4-presenter/png/sw-4-08-controls.png) |
| When things go wrong | [09-when-things-go-wrong](sw-4-presenter/png/sw-4-09-when-things-go-wrong.png) |
| Design questions and the answers given | [10-questions-answered](sw-4-presenter/png/sw-4-10-questions-answered.png) |

### Decisions (2026-11-17)

- **D1. Phone remote:** SW-7 is merged into SW-4, so one Feature builds the shared state once. `DeckHub` already exists, so nothing waits for SW-5.
- **D2. PDF export:** moved out to its own Feature, SW-173, at the back of the backlog and not planned.
- **D3. Who drives:** only the deck owner's signed-in sessions can join a deck's `present:{deckId}` hub group. A link viewer or an agent can't. The QR code holds no secret: it opens `/remote?deck={id}`, and signing in is what grants control.
- **D4. Sync:** the presenter window, the audience window and every phone share one state through `DeckHub`. There is no local link between the windows. Each move sends the whole state, `{slide, step, black, skipDeep, timer}`, with a sequence number, and the newest wins. The hub group keeps the last state so a late joiner starts in the right place. The cost is that a Wi-Fi drop freezes the audience window until it reconnects. Its own keys still move it in the meantime.
- **D5. Timer:** the presenter can set an optional talk length per deck. The timer then shows the time left, turns amber at 5 minutes left and red when over.
- **D6. Phone layout:** the current slide is shown first and fills the width. The next slide sits beside its label, and the notes scroll underneath. Next is the big button.

## SW-5 Audience join

Open [`sw-5-audience-join/sw-5-mockups.html`](sw-5-audience-join/sw-5-mockups.html) in a browser. Nearly every attendee joins by scanning the QR code with a phone, so the phone page comes first and the presenter's side second. The live demo is interactive. Press Go live and step through the deck on the laptop, and the phone follows: it joins, mirrors the slide, shows the activity card on the interactive slide, and ends with the session. The toggles try the join bar, a dropped connection and joining before Go live. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what SW-5 ships, and what SW-6 onwards adds | [01-scope](sw-5-audience-join/png/sw-5-01-scope.png) |
| The 10-second journey: camera, joined, following, an interactive slide | [02-journey](sw-5-audience-join/png/sw-5-02-journey.png) |
| Live demo: the presenter's laptop drives a guest's phone | [03-live-demo](sw-5-audience-join/png/sw-5-03-live-demo.png) |
| Every phone state: typing the code, a wrong code, the lobby, following, reconnecting, privacy, ended, offline, landscape | [04-phone-states](sw-5-audience-join/png/sw-5-04-phone-states.png) |
| On the big screen: the join slide, the join bar, the join slide before Go live | [05-big-screen](sw-5-audience-join/png/sw-5-05-big-screen.png) |
| The code and the join link | [06-code-and-link](sw-5-audience-join/png/sw-5-06-code-and-link.png) |
| Realtime and privacy | [07-realtime-privacy](sw-5-audience-join/png/sw-5-07-realtime-privacy.png) |
| Design questions and the answers given | [08-questions-answered](sw-5-audience-join/png/sw-5-08-questions-answered.png) |

### Decisions (2026-11-17)

- **D1. Join code:** 6 digits, from 100000 to 999999, shown as `482 913`. It is typed on a numeric keypad and read out as two triples.
- **D2. Guest identity:** guests give no name. A guest is a random ID kept in the browser for the session, so a reload doesn't count twice. A later activity (Q&A, a quiz) may ask for a nickname when it starts, if it needs one.
- **D3. Between interactive slides:** the phone shows the current slide with a "nothing to answer yet" card below it. An interactive slide (SW-6 onwards) takes over that card. Guests can't browse back through past slides.
- **D4. Code lifetime:** each deck has one standing code. Every Go live opens a new session behind it, so the QR never changes and answers stay separate per run. A guest who joins before Go live waits in a lobby.
- **D5. Join bar:** a small QR code and the code in the corner of every slide. It is off by default, as a deck setting with a toggle in the presenter's live chip.
- **D6. Joined count:** shown on the join slide (the layout can hide it) and always in the presenter's live chip.
- **D7. Realtime:** SignalR on the existing `DeckHub`, with SSE and long-polling fallbacks for guests on networks that block WebSockets. There is no cap on guests per session.
- **D8. Join domain:** joining has its own short domain. The QR holds `https://slideworks.live/482913`, and the bare domain is the page where you type the code. The domain is an instance setting (`SLIDEWORKS_JOIN_URL`), so a self-hosted instance can use its own or none. With none, links fall back to `{instance}/j/{code}` and `{instance}/join`.
