| Landscape: the slide full screen with the translation over it; a demo (iPhone web, Android web, Android full screen, the app; captions over, below or off; three sizes) and 14 states | [05-landscape](sw-885-listen/png/sw-885-05-landscape.png) |
| Speaker side: the microphone tip, the Microphone row, a lapel mic, headphone counts in the Subtitles sheet and the presenter window | [06-speaker](sw-885-listen/png/sw-885-06-speaker.png) |
| How a line gets from the speaker to a guest's ears, the guest's speech queue and the hub messages | [07-flow](sw-885-listen/png/sw-885-07-flow.png) |
| The decisions | [08-decisions](sw-885-listen/png/sw-885-08-decisions.png) |

# Design

Mockups and design decisions, with one folder per Feature.

| Folder | Feature | Status |
|---|---|---|
| [`sw-1-foundation/`](sw-1-foundation/) | SW-1 Foundation: agent-built web decks over MCP | Mockups reviewed, decisions D3–D9 settled, built on the SW-1 branch |
| [`sw-2-templates-palettes/`](sw-2-templates-palettes/) | SW-2 Templates and palettes (also covers SW-15 Wada palettes; its SW-50 sketch is replaced by `sw-50-custom-templates/`) | Built (SW-2's sections; Wada palettes and uploaded templates are still to come) |
| [`sw-4-presenter/`](sw-4-presenter/) | SW-4 Presenter view and phone remote (SW-7 merged in) | Built (decisions D1–D6 settled, plan decisions D7–D17 as built; the user guide is [`docs/presenting.md`](../docs/presenting.md)) |
| [`sw-5-audience-join/`](sw-5-audience-join/) | SW-5 Audience join: QR code, join code and guest mode | Built (decisions D1–D8 settled) |
| [`sw-6-polls/`](sw-6-polls/) | SW-6 Live multiple-choice polls | Built (decisions D1–D8 settled; the mockups differ in a few places, listed under "As built" in the SW-6 section) |
| [`sw-9-qa-reactions/`](sw-9-qa-reactions/) | SW-9 Audience Q&A and reactions | Built (decisions D1–D22 settled) |
| [`sw-50-custom-templates/`](sw-50-custom-templates/) | SW-50 Custom templates, and the layout gaps a real deck found | Partly built: the template editor and the new layouts are on main; template bundles (upload and download) and custom layouts are not built yet. Decisions D4–D17 settled; D18–D48 recorded as recommended, not reviewed |
| [`sw-108-profile-sharing/`](sw-108-profile-sharing/) | SW-108 Profile page and sharing decks with people | Mockups reviewed 22 Nov; decisions D12 and D24–D32 settled, D33 open; not built |
| [`sw-883-translations/`](sw-883-translations/) | SW-883 Slide translations, and the guest phone's full-screen landscape | Mockups reviewed 28 Nov; decisions D1–D9 settled; not built |
| [`sw-884-live-speech/`](sw-884-live-speech/) | SW-884 Live speech V1: a speaker app with on-device subtitles, translated on the server for each guest's phone | Mockups reviewed 28 Nov; decisions D1–D24 settled; not built |
| [`sw-885-listen/`](sw-885-listen/) | SW-885 Live speech V2: guests listen to the translated talk (a Listen button on mobile web, and an audience mode in the app) | Mockups reviewed 28 Nov, landscape added in round 2; decisions D1–D27 settled; not built |
| [`sw-943-translation-engine/`](sw-943-translation-engine/) | SW-943 Translation engine: self-hosted models, chosen per language pair in the admin panel | Mockups reviewed 28 Nov; decisions D1–D13 settled; not built |

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

## SW-6 Live polls

Open [`sw-6-polls/sw-6-mockups.html`](sw-6-polls/sw-6-mockups.html) in a browser. A poll is a slide: the presenter reaches it, the phones that joined in SW-5 swap the slide for the question, and the bars on the big screen grow as answers arrive. The live demo is interactive. Press ▶ in the presenter window to reach the poll slide, tap an option on the phone to answer, then close the poll, reveal hidden results, show the correct answer or start a new round. The settings under the demo switch between pick one, pick up to 2 and a poll with a correct answer, and between the three results settings. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what SW-6 ships, and what SW-8 to SW-11 add | [01-scope](sw-6-polls/png/sw-6-01-scope.png) |
| Live demo: the presenter window runs a poll and a guest's phone answers it | [02-live-demo](sw-6-polls/png/sw-6-02-live-demo.png) |
| Every phone state: choosing, pick several, sent, closed, correct answer, a changed poll, a dropped connection, not open yet, a named poll | [03-phone-states](sw-6-polls/png/sw-6-03-phone-states.png) |
| On the big screen: live, hidden, closed, correct answer, pick up to 2, before it opens | [04-big-screen](sw-6-polls/png/sw-6-04-big-screen.png) |
| Presenter controls: the lifecycle, the controls and the phone remote strip | [05-presenter-controls](sw-6-polls/png/sw-6-05-presenter-controls.png) |
| The poll model: methods, the results setting, rules, privacy and the measure | [06-poll-model](sw-6-polls/png/sw-6-06-poll-model.png) |
| The agent's side: an MCP session and the tools | [07-agent-mcp](sw-6-polls/png/sw-6-07-agent-mcp.png) |
| Realtime and load | [08-realtime-load](sw-6-polls/png/sw-6-08-realtime-load.png) |
| Design questions and the answers given | [09-questions-answered](sw-6-polls/png/sw-6-09-questions-answered.png) |

### Decisions (2026-11-17)

- **D1. Where a poll lives:** a `poll` slide layout, with one poll per slide. The question and the options are slots, so an agent adds a poll with `add_slide`. Settings and answers live in poll tables keyed by the slide.
- **D2. Opening and closing:** a poll opens when the presenter reaches its slide. It closes when the presenter presses Close or moves to another slide. A per-poll setting, `opens: manual`, makes it open by hand.
- **D3. Sending:** for pick-one, a tap sends the answer and tapping another option changes it. A Send button appears only where several picks are needed.
- **D4. Results setting:** every poll has one of three settings: live, after you've answered, or after the poll closes. Live is the default. The presenter always sees the counts, and a phone never shows results before its owner has answered.
- **D5. Privacy:** SW-6 ships private polls only. An answer is stored against the guest ID from SW-5, which has no name, and nobody sees who picked what. The model keeps a privacy field so named polls can arrive with nicknames in the quiz Feature (SW-11).
- **D6. Correct answer:** optional on any poll. The presenter shows it with a separate button after closing. The slide marks the correct bar and each phone says whether its owner got it. There are no scores.
- **D7. Reset:** New round asks the same question again from zero. Earlier rounds are kept, and the presenter panel and `get_poll_results` list them. The big screen shows only the current round.
- **D8. Load and measure:** a run of 1,000 voters must pass before SW-6 ships, with sending an answer under 1 s at p95, results on screen under 2 s at p95 and errors under 1%. It runs on demand, not on every pull request. Runs at 3,000 and 10,000 voters are measured and written down, not gates. The measure is the response rate: people who answered ÷ guests joined when the poll closed.

### As built

The mockups are not edited. The product differs from them in these places, on purpose:

- **Keys:** the presenter window uses `P` to open or close the poll, `R` to reveal results and `A` to show the answer. The mockup's `O` and `N` clash with SW-4's overview and notes keys, and New round has no key.
- **Errors on the phone remote:** the mockup has no error state for the poll strip. A refused call shows its message over the slide preview, above the strip, so the strip keeps its height on a short screen. A dismiss button clears it.
- **The answer count on a phone:** once a guest has answered, the phone shows "N answers so far" while the poll is open, also when the bars are hidden. The mockup shows it only when the bars show. A guest who hasn't answered yet sees no count.
- **A changed poll:** the phone's card says "<presenter> changed the poll. Send this pick again?" The mockup names a new round.
- **The join pill and the hidden-results chip on the big screen:** both use the theme's ink colour with paper-coloured text, from the look's tokens. The mockup draws them in its night colour with white text.

## SW-9 Audience Q&A and reactions

Open [`sw-9-qa-reactions/sw-9-mockups.html`](sw-9-qa-reactions/sw-9-mockups.html) in a browser. The guest page from SW-5 gets a second tab, Questions, where anyone can ask and upvote for the whole session, and a row of five reactions under the slide. The presenter gets a Questions panel in the SW-4 presenter window. The live demo is interactive. The room asks, upvotes and reacts on its own. On the phone you can ask, upvote and react. In the presenter panel you can show a question on screen, mark it answered, hide it and approve it. The settings under the demo switch review-first, names, reactions, where a shown question appears, and a slide with questions and reactions off. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what SW-9 ships, and what it leaves to SW-8, SW-11 and SW-12 | [01-scope](sw-9-qa-reactions/png/sw-9-01-scope.png) |
| Live demo: the presenter window runs a Q&A and a guest's phone asks, upvotes and reacts | [02-demo](sw-9-qa-reactions/png/sw-9-02-demo.png) |
| Every phone state: reactions, the list, empty, asking, with a name, sent, waiting for approval, on screen and answered, a poll opening, hidden, a dropped connection, closed | [03-phone](sw-9-qa-reactions/png/sw-9-03-phone.png) |
| On the big screen: a question over any slide, the `qa` slide, one question up, empty, reactions, not live | [04-screen](sw-9-qa-reactions/png/sw-9-04-screen.png) |
| Presenter controls: the question lifecycle, the controls and the phone remote strip | [05-controls](sw-9-qa-reactions/png/sw-9-05-controls.png) |
| The Q&A model: how it maps onto SW-6's poll model, the settings, rules, privacy and the measure | [06-model](sw-9-qa-reactions/png/sw-9-06-model.png) |
| The agent's side: an MCP session and the tools | [07-mcp](sw-9-qa-reactions/png/sw-9-07-mcp.png) |
| Realtime and limits | [08-realtime](sw-9-qa-reactions/png/sw-9-08-realtime.png) |
| Design questions and the answers given | [09-questions](sw-9-qa-reactions/png/sw-9-09-questions.png) |

### Decisions (2026-11-17)

- **D1. Where Q&A lives on the phone:** the guest page gets two tabs at the bottom. Slide is the SW-5 page with SW-6's poll card. Questions is the list, with the number of open questions on the tab.
- **D2. On by default:** questions and reactions are both on for every live session. Each can be turned off for the whole deck or for one slide.
- **D3. Names:** a question is anonymous by default. The ask sheet has a "Show my name" switch and a free-text name, shown with that question only. A deck setting can turn names off.
- **D4. Review:** a question shows on phones at once, and the presenter can hide it. A per-deck "review first" setting makes new questions wait for approval. Nothing reaches the big screen unless the presenter shows it.
- **D5. The big screen:** Show on screen puts one question over any slide as a card, and a `qa` slide layout lists the top five. A deck setting says where a shown question appears.
- **D6. Reactions:** five fixed emoji: thumbs up, heart, laugh, wow and clap. They float up the edge of the big screen and add to a counter in the presenter panel. Other phones don't receive them.
- **D7. A poll opening while a guest is on Questions:** the phone switches to the Slide tab by itself, unless the ask sheet is open.
- **D8. The model (changed: SW-6 was built first, then generalised):** Q&A is a real poll on SW-6's model. It is one poll per deck with a round per live session, where a question is an option the audience appended and a guest's upvotes are one answer. SW-6 was built as planned and SW-9 then generalised its tables with a migration (`Sw9PollKinds`): a poll has its own id and a kind (`Slide` or `Qa`) with `SlideId` nullable, a round has the kind and there is one open round per session per kind, and option rows exist only for the Q&A poll, one per question, up to 200 characters. Approve, hide, answered and on screen are a separate `Question` row that the poll doesn't know about.
- **D9. Measure and load:** the measure is questions asked per session, and the share of joined guests who asked, upvoted or reacted at least once. SW-6's load harness gets one more script: 1,000 guests who each upvote three questions and tap ten reactions within 30 seconds, held to SW-6's gates and run by hand.
- **D10. Detail of D2 and D5 (confirmed):** a slide with questions or reactions off hides the reaction row and replaces asking with "Questions are off on this slide" while it is on screen; the list and upvotes stay. The deck setting `show_on` has two values: `anySlide`, the default, where the card lays over whatever slide is up, and `qaSlide`, where a shown question appears only while a Q&A slide is up.
- **D11. Gates:** SW-9's branch started after SW-6's PR #14 merged, and the briefs were re-checked against SW-6's files first.
- **D12. How general the poll tables are:** see D8. A slide poll's options stay in its slide's slots, so SW-6's behaviour didn't change.
- **D13. Question states:** a question is waiting, open or answered. Hidden and on screen are flags beside that. Hide works on a waiting or open question and takes it off screen, not on an answered one. Restore returns the state it had, so a question hidden while waiting waits again. Reopen makes an answered question open, and its votes can change again.
- **D14. What phones receive about votes:** a `questionTally` with the votes of only the questions another guest may see (not hidden, not waiting), at most twice a second, plus a list revision that tells the phone to re-read its list. Presenter screens get the whole list in `questionControl`.
- **D15. When a guest can't ask:** 409 with `questions_closed`, `questions_off` or `questions_off_here`. Upvotes still count while questions are closed or off on a slide. With questions off for the deck there is no round and no Questions tab.
- **D16. Where the settings live:** a settings row per deck (absent means the defaults) and a switch row per slide. Anyone who can present changes them, live or not, from the presenter panel's settings strip, the "Q&A and reactions" card on the deck page, and `update_qa`.
- **D17. Who sees questions and reactions on the big screen:** only the screens of a person who can present the deck. Other viewers see the Q&A slide's "not live" state and no card or emoji.
- **D18. The key:** `Q` opens and closes the Questions panel in the presenter window.
- **D19. Reactions are stored** as totals per session, slide and emoji, written every 5 seconds and when the session ends, plus one row per guest who reacted at all in the session. Never a row per tap.
- **D20. Limits:** 200 characters a question, 30 a name, 3 questions a minute per guest, 30 vote changes a minute per guest on Q&A's own limiter, 10 reaction taps in 5 seconds per guest, at most 30 emoji on the big screen and then "+N", five rows on the Q&A slide.
- **D21. Upvotes on your own or an answered question:** a guest can take back the upvote on its own question. An upvote on an answered question can't be added or removed.
- **D22. Question ages:** the server sends an age label with each question ("now", "2 min", "1 h"), made when the list is read. A phone re-reads its list once a minute while the Questions tab is showing.

## SW-50 Custom templates

Open [`sw-50-custom-templates/sw-50-mockups.html`](sw-50-custom-templates/sw-50-mockups.html) in a browser. A person or an agent makes their own template: fonts, logos, title art, ornament, a palette and custom layouts. It is checked, published as a version and shared like a deck. The template editor is interactive: the tabs switch, the colour pickers re-skin the sample slides and re-run the contrast check, and the publish checklist follows. The example brand, Northwind, is made up. Brand templates are user content and never ship in this repo. The PNGs are screenshots of each section at 1300 px wide.

SW-50 was first sketched in the SW-2 mockups (section 07) as "uploaded templates and deck import". It was split on 2026-11-17: importing a look from an existing deck is now SW-354. The same day, the built-in layout gaps found by rebuilding a real 44-slide deck were merged into SW-50. Their mockups are three more pages, listed under "Layout gaps" below.

| Screen | PNG |
|---|---|
| Scope: the split from SW-354, what SW-2 already built, the merged-in layout gaps, a template's life | [01-scope](sw-50-custom-templates/png/sw-50-01-scope.png) |
| The Templates page, the New template dialog, the bundle file | [02-templates-page](sw-50-custom-templates/png/sw-50-02-templates-page.png) |
| The template editor: basics, fonts, logo and art, palette, custom layouts, live preview, publish checklist | [03-editor](sw-50-custom-templates/png/sw-50-03-editor.png) |
| Checks and what each failure looks like; versions and the update offer; archive; limits | [04-checks](sw-50-custom-templates/png/sw-50-04-checks.png) |
| Custom layouts: slots, markup, the renders to review, the bindings, what is allowed and refused | [05-custom-layouts](sw-50-custom-templates/png/sw-50-05-custom-layouts.png) |
| Sharing a template, the New deck dialog, who sees what | [06-sharing-and-use](sw-50-custom-templates/png/sw-50-06-sharing-and-use.png) |
| The agent's side: an MCP session and the tools | [07-agent-mcp](sw-50-custom-templates/png/sw-50-07-agent-mcp.png) |
| How we build it: what changes where, the order of work, risks | [08-how-we-build-it](sw-50-custom-templates/png/sw-50-08-how-we-build-it.png) |
| Design questions and the answers given | [09-questions-answered](sw-50-custom-templates/png/sw-50-09-questions-answered.png) |

### Decisions (2026-11-17)

D1–D3 were settled with SW-2 on 2026-11-16 and are listed in its section. D2's importer half moved to SW-354.

- **D4. Making a template:** an editor in the app that starts from a built-in template, with a live preview and a publish checklist. A bundle (`.zip` with `template.json` and the files) can be uploaded to fill it and downloaded from it.
- **D5. Custom layouts come last:** templates with fonts, logos, art and a palette ship first. Custom layouts are the last wave of the same Feature.
- **D6. Logo and art files:** SVG, PNG and WebP. An SVG is checked on upload (no script, no external references, no `foreignObject`) and only ever served as an image with a sandbox Content-Security-Policy.
- **D7. Versions:** publishing a draft adds a version, and a published version never changes. A deck stays on the version it was made with and is offered the update in its Look panel.
- **D8. Text fit:** the server measures each uploaded font's average character width in headless Chromium at upload. `SlideFit` uses the template's widths, so `add_slide` refuses text that won't fit in that font. A font outside 0.45–0.70 em is refused.
- **D9. Layout review:** the template's owner approves each custom layout after seeing it with sample content and with maximum content. The sanitiser, not the review, is the security boundary.
- **D10. Measure:** the share of new decks that use an uploaded template, counted from the activity log.
- **D11. Colours beyond the token list:** a new token, `accent-2`, in every palette. The built-in palettes set it to their accent, so they look the same. The progress bar's far end, the current journey stop and title art use it. A custom palette can also hold up to four extra named colours, which only ornament and custom layouts can use.
- **D12. Logo on dark slides:** title, divider and close slides draw the dark logo in the bottom-right corner, with no footer text.
- **D13. Top accent bar:** a chrome setting drawn by SlideWorks, none or accent (8 px), which a deck can override.
- **D14. Update my decks:** after publishing a version, the template's owner can move their own decks to it in one go. Other people's decks are updated by their owners.
- **D15. Custom layouts react to values:** a number slot can set a CSS variable (a bar's length, a needle's angle), an enum slot can pick a variant class, and `data-step` puts an element on a later click.
- **D16. Light or dark:** a custom layout declares its surface: light, dark, or either, where the slide picks.
- **D17. Motion:** none in SW-50 beyond step reveals. Named effects are a later story.

### Layout gaps (2026-11-17)

The built-in layout gaps (SW-356 to SW-373) were designed in three groups, each with a mockup page and a build spec. The decisions were recorded on SW-50 as recommended and have not been reviewed in a design round; the work item holds each one's question, answer and alternatives.

| Group | Page | Build spec | Decisions |
|---|---|---|---|
| A: slots on existing layouts, chrome and parts, and the small items (SW-356, SW-357, SW-360, SW-361, SW-364, SW-367, SW-370 to SW-373) | [`sw-50-gaps-a.html`](sw-50-custom-templates/sw-50-gaps-a.html) | [`gaps-a.md`](sw-50-custom-templates/gaps-a.md) | D18–D28 |
| B: new or wider layouts (SW-358, SW-359, SW-362, SW-363, SW-365, SW-366, SW-368) | [`sw-50-gaps-b.html`](sw-50-custom-templates/sw-50-gaps-b.html) | [`gaps-b.md`](sw-50-custom-templates/gaps-b.md) | D39–D48 |
| C: diagram building blocks (SW-369) | [`sw-50-gaps-c.html`](sw-50-custom-templates/sw-50-gaps-c.html) | [`gaps-c.md`](sw-50-custom-templates/gaps-c.md) | D29–D38 |

The pages draw slides in system fonts, so letter widths differ a little from the built-in fonts, and the fit figures in the specs are estimates to confirm when the real fixtures are made.

| Group | Section | PNG |
|---|---|---|
| A | callout | [01-callout](sw-50-custom-templates/png/sw-50-gaps-a-01-callout.png) |
| A | emphasis | [02-emphasis](sw-50-custom-templates/png/sw-50-gaps-a-02-emphasis.png) |
| A | icons | [03-icons](sw-50-custom-templates/png/sw-50-gaps-a-03-icons.png) |
| A | steps | [04-steps](sw-50-custom-templates/png/sw-50-gaps-a-04-steps.png) |
| A | limits | [05-limits](sw-50-custom-templates/png/sw-50-gaps-a-05-limits.png) |
| A | parts | [06-parts](sw-50-custom-templates/png/sw-50-gaps-a-06-parts.png) |
| A | notes | [07-notes](sw-50-custom-templates/png/sw-50-gaps-a-07-notes.png) |
| A | preview deck | [08-preview-deck](sw-50-custom-templates/png/sw-50-gaps-a-08-preview-deck.png) |
| A | talk template | [09-talk-template](sw-50-custom-templates/png/sw-50-gaps-a-09-talk-template.png) |
| A | shared | [10-shared](sw-50-custom-templates/png/sw-50-gaps-a-10-shared.png) |
| B | scope | [00-scope](sw-50-custom-templates/png/sw-50-gaps-b-00-scope.png) |
| B | surface | [01-surface](sw-50-custom-templates/png/sw-50-gaps-b-01-surface.png) |
| B | statement | [02-statement](sw-50-custom-templates/png/sw-50-gaps-b-02-statement.png) |
| B | cards | [03-cards](sw-50-custom-templates/png/sw-50-gaps-b-03-cards.png) |
| B | compare | [04-compare](sw-50-custom-templates/png/sw-50-gaps-b-04-compare.png) |
| B | quote | [05-quote](sw-50-custom-templates/png/sw-50-gaps-b-05-quote.png) |
| B | links | [06-links](sw-50-custom-templates/png/sw-50-gaps-b-06-links.png) |
| B | image | [07-image](sw-50-custom-templates/png/sw-50-gaps-b-07-image.png) |
| B | decisions | [08-decisions](sw-50-custom-templates/png/sw-50-gaps-b-08-decisions.png) |
| C | verdicts | [01-verdicts](sw-50-custom-templates/png/sw-50-gaps-c-01-verdicts.png) |
| C | stack | [02-stack](sw-50-custom-templates/png/sw-50-gaps-c-02-stack.png) |
| C | bars | [03-bars](sw-50-custom-templates/png/sw-50-gaps-c-03-bars.png) |
| C | flow | [04-flow](sw-50-custom-templates/png/sw-50-gaps-c-04-flow.png) |
| C | levels and facts | [05-levels-and-facts](sw-50-custom-templates/png/sw-50-gaps-c-05-levels-and-facts.png) |
| C | custom layouts | [06-custom-layouts](sw-50-custom-templates/png/sw-50-gaps-c-06-custom-layouts.png) |
| C | findings and hooks | [07-findings-and-hooks](sw-50-custom-templates/png/sw-50-gaps-c-07-findings-and-hooks.png) |

## SW-108 Profile page and sharing decks with people

Open [`sw-108-profile-sharing/sw-108-mockups.html`](sw-108-profile-sharing/sw-108-mockups.html) in a browser. It shows every screen SW-108 adds or changes, drawn with the app's own chrome tokens. The share dialog in section 4 works: type a full email to add someone, change their access, remove them and save. A partial email or name suggests nobody. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what changes for people | [01-scope](sw-108-profile-sharing/png/sw-108-01-scope.png) |
| User menu with a Profile link | [02-user-menu](sw-108-profile-sharing/png/sw-108-02-user-menu.png) |
| /profile: Identity, Account and Connected agents | [03-profile](sw-108-profile-sharing/png/sw-108-03-profile.png) |
| Profile states: avatar precedence, refused name and photo, disconnecting an agent, no agents | [04-profile-states](sw-108-profile-sharing/png/sw-108-04-profile-states.png) |
| Share dialog: today's (SW-31) beside SW-108's people list with Can view and Can edit | [05-share-dialog](sw-108-profile-sharing/png/sw-108-05-share-dialog.png) |
| Share states: over the limit, a partial email, Private with people on the list, pending people | [06-share-states](sw-108-profile-sharing/png/sw-108-06-share-states.png) |
| The deck as a Can edit and a Can view person, Shared with you, and activity | [07-collaborators](sw-108-profile-sharing/png/sw-108-07-collaborators.png) |
| The share email and the unsubscribe page | [08-email-unsubscribe](sw-108-profile-sharing/png/sw-108-08-email-unsubscribe.png) |
| Design questions and the answers given | [09-questions](sw-108-profile-sharing/png/sw-108-09-questions.png) |

`sharing-dialog-sw31.png` is the SW-31 dialog's CI baseline on main (6e353cd), shown in section 4 for comparison.

### Decisions (2026-11-22)

D1–D11 and D13–D23 are on SW-108. These are the ones the mockups show:

- **D12. Private:** at Private only the owner gets in, people with Can edit included. The people list is kept and shown as paused until the level changes back.
- **D24. Names on the people list:** once a person has an account, a deck's people list shows their display name and avatar. That tells the owner the email has an account, which the limit of 10 new people an hour bounds.
- **D25. Labels:** the two levels say "Can view" and "Can edit". The wire stays `read` and `write`.
- **D26. People list:** its own People section under the three levels, because Can edit still works at "Anyone with the link" (D8). The third level is renamed "Only people below".
- **D27. Disconnect:** asks first, inline, because revoking an agent can't be undone.
- **D28. Photo:** upload and remove take effect straight away. Name and colour wait for Save.
- **D29. Limit warning:** none before a share is refused. The refusal says when the limit resets.
- **D30. Share email:** doesn't say view or edit. It has the sharer's name and email, the deck title, one link and the unsubscribe link (C8).
- **D31. User menu:** Profile, People (admin) for admins, and Sign out. The top-bar badge for people waiting stays.
- **D32. Per deck, full email every time:** a new deck is shared with nobody. Each deck is shared on its own, by typing each person's full email, every time. There is no favourites list, autocomplete, search or "People you share with" panel. This replaces D5 and D9.
- **D33 (open, recommended). What the limit counts:** each distinct email the sharer newly added to any of their decks in the last hour or day. Adding one person to five decks counts once.

## SW-883 Slide translations

Open [`sw-883-translations/sw-883-mockups.html`](sw-883-translations/sw-883-mockups.html) in a browser. A deck can carry translations of its slides. Each guest picks a language on the phone, while the big screen, the presenter window, the phone remote and the notes stay in the deck's primary language.

The live demo works. Step through the deck on the laptop, switch the phone between English, ไทย, 日本語 and Español with its language chip, and turn the phone sideways. A dashed outline marks a line that falls back to English; the outline is for review only and is not in the app. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what is translated and what stays in the primary language | [01-scope](sw-883-translations/png/sw-883-01-scope.png) |
| Live demo: the presenter in English and a guest's phone in four languages | [02-live-demo](sw-883-translations/png/sw-883-02-live-demo.png) |
| The new phone states: joined in the phone's language, the language sheet, a line in English, a translated poll, the landscape hint, a deck without translations, landscape on Android | [03-phone-states](sw-883-translations/png/sw-883-03-phone-states.png) |
| Landscape: SW-5's CI baseline beside what SW-883 adds | [04-landscape](sw-883-translations/png/sw-883-04-landscape.png) |
| The deck page's Translations card, off and on | [05-deck-page](sw-883-translations/png/sw-883-05-deck-page.png) |
| Three ways to store translations | [06-storage](sw-883-translations/png/sw-883-06-storage.png) |
| The agent's side: an MCP session and the tools | [07-agent-mcp](sw-883-translations/png/sw-883-07-agent-mcp.png) |
| How each phone gets its language through `DeckHub` | [08-realtime](sw-883-translations/png/sw-883-08-realtime.png) |
| Design questions and the answers given | [09-questions-answered](sw-883-translations/png/sw-883-09-questions-answered.png) |

`as-built-landscape.png` is the `guest-following-landscape` CI baseline on main. Section 04 shows it for comparison. Landscape full screen was built in SW-5 (`GuestFollowing.tsx`), so SW-883 only adds to it.

### Decisions (2026-11-28)

- **D1. Storage:** translations sit beside the slots, keyed by slot path (JSON pointer). Each entry holds the text and the primary text it was translated from (`from`). The slots stay plain primary-language strings, and one merge applies a language before rendering, so the renderer, fit check, lint, PDF export and share card don't change. When a slide's slots are replaced, the server re-keys the entries it can match, and the others go stale.
- **D2. Picker:** a language chip in the guest header next to Live. It opens a sheet listing the deck's languages in their own names, with how complete each one is. The choice changes only that phone.
- **D3. Starting language:** the phone's own language when the deck has it, with a one-line banner offering to change it. Otherwise the primary language. The choice is kept for the session.
- **D4. App text:** SW-883 translates slides only. The guest app's own text (tabs, the waiting card, the poll card's hints, the privacy sheet) stays English, and translating it is a separate Feature.
- **D5. Turning it on:** off by default for every deck. `set_translations` over MCP turns it on, as does a switch on the deck page's Translations card, which also lists coverage for each language.
- **D6. Landscape:** SW-5's full-screen slide, plus a tap overlay for 3 seconds (slide number, Live chip, language chip, progress), a one-time portrait hint ("Turn sideways to read the slide full screen"), and a Full screen button in the overlay where the browser supports it (Android Chrome, not iPhone Safari). Pinch to zoom is out.
- **D7. Presenter:** a small "Reading in" counter next to the live chip, shown only when translations are on.
- **D8. Translating:** the agent writes the translations over MCP. SlideWorks only stores them, fit-checks them in the language's font, and tracks which are stale. There is no machine-translation provider.
- **D9. Stale lines and right-to-left:** a stale line shows the primary text until it is translated again. Right-to-left languages are a later Feature.

Proposed in the mockups for the plan to confirm:

- **MCP.** New tools: `set_translations` (on or off, primary language, language list) and `translate_slides` (one language, many slides, each slide's revision; `null` removes a line). `get_deck` and `preview_deck` take a `language`. `check_deck` checks every language. `update_slide` warns about the lines it made stale.
- **Realtime.** A guest calls `setLanguage` on `DeckHub` and joins `audience:{session}:{lang}`. On each move the hub sends each language group the current slide with its language already merged, so the guest renderer doesn't change. Polls count by option index, so every language adds to the same bars.
- **Fonts.** Each language gets a Noto fallback (Noto Sans Thai, Noto Sans JP and so on), and the fit check measures with that font, using SW-50 D8's font widths.

## SW-884 Live speech V1

Open [`sw-884-live-speech/sw-884-mockups.html`](sw-884-live-speech/sw-884-mockups.html) in a browser. A native speaker app does everything speaker mode does on mobile web, and adds live subtitles. The phone turns the speaker's voice into text on the device. The server translates each finished line once for every language a guest is reading in (through SW-943's translation engine). Guests see two lines of subtitles under the slide in their SW-883 language.

The live demo works. Sam's phone talks and the guest phone follows in English, ไทย or 日本語. You can pause subtitles, drop and restore Sam's connection, hide the guest's subtitles, and watch the per-line server log (model ids and latency). One Japanese line fails on purpose to show the "not translated" marker. Model sizes and latency figures are examples, not measurements. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: V1, what V2 (SW-885) adds, and what is elsewhere | [01-scope](sw-884-live-speech/png/sw-884-01-scope.png) |
| Live demo: the speaker app, a guest phone and the server's per-line log | [02-demo](sw-884-live-speech/png/sw-884-02-demo.png) |
| Speaker app: sign-in, Presenting now, the Subtitles button and sheet, model download, mic permission, a mixed Thai and English talk, paused, offline, back after leaving the app | [03-app](sw-884-live-speech/png/sw-884-03-app.png) |
| Guest phone: the subtitle strip in English, ไทย and 日本語, a failed translation, hidden, paused, reconnecting, landscape captions | [04-guest](sw-884-live-speech/png/sw-884-04-guest.png) |
| The presenter window (CC chip, Captions next to Join bar, Phone pairing QR), big-screen captions and the admin Speech to text tab | [05-laptop](sw-884-live-speech/png/sw-884-05-laptop.png) |
| How a line gets from the speaker to a guest, the hub messages and the model manifest | [06-flow](sw-884-live-speech/png/sw-884-06-flow.png) |
| The decisions | [07-decisions](sw-884-live-speech/png/sw-884-07-decisions.png) |

### Decisions (2026-11-28)

Architecture, settled before the mockups:

- **D1. Speech to text:** on the speaker's phone, streaming, behind a `SpeechEngine` seam. V1 engines run on sherpa-onnx: Parakeet TDT 0.6B for English and Typhoon ASR 115M (NeMo exported to ONNX) for Thai. A cloud engine was planned for V2 and is now its own Feature, SW-981.
- **D2. Translation:** on the SlideWorks server, self-hosted, through `ITranslator` (SW-943). The model is chosen per language pair in admin.
- **D3. No spike:** V1 ships with swappable models, and models are compared in production with friends.
- **D4. Privacy:** no audio is stored or uploaded. The transcript is kept with the deck's activity only when the speaker turns it on (off by default).
- **D5. Connection drops:** speech to text keeps running on the phone and lines queue there. On reconnect only the last 30 seconds are sent.
- **D6. Model swaps:** a server-side manifest (id, engine, languages, URL, size, sha256). The app downloads a model on first use, so a swap needs no app release.
- **D7.** Withdrawn (moved to SW-943).
- **D8. Partials:** words still being heard go only to guests reading the language the line was heard in. Everyone else gets each final line once it is translated.
- **D9. Failed translation:** guests get the line as heard, marked "not translated".
- **D10. Languages:** guests get subtitles in the deck's translation languages, picked with SW-883's language chip. There is no second picker.
- **D11. Routing:** `IAudienceRoster.ReadingAsync` (SW-886) decides which languages to translate into, and subtitles go to SW-883's per-language slide-state groups.

Screens, from the mockup review:

- **D12. Speaker control:** a fifth button, Subtitles, at the end of speaker mode's action row. It shows the state (purple live, amber paused, orange offline) and opens the Subtitles sheet: the lines just heard, Listening for, the big-screen captions switch, who is reading, and Pause.
- **D13. What the speaker sees:** the button state, the heard lines in the sheet, and a one-line ticker of the latest words above Next while subtitles are live.
- **D14. Model download:** the first time subtitles start in a language, from a setup sheet, on Wi-Fi unless the speaker allows mobile data. A Speech models page in the app can get them ahead of time.
- **D15. Mixed English and Thai:** both models listen at once, and each line goes to the one that is surer. Every line carries the language it was heard in, and is translated from that language.
- **D16. Leaving the app:** leaving the app or locking the phone pauses subtitles and releases the mic. Guests see "paused", and the app offers Resume on return. The screen stays awake while speaker mode is open, as on web.
- **D17. Guests:** subtitles are on whenever the speaker has them on, and Hide lasts for the session. In landscape, two caption lines sit over the bottom of the slide.
- **D18. Big screen:** an optional captions bar above the join bar, with final lines in the language heard. Off by default for each deck, toggled with a Captions button next to Join bar.
- **D19. Admin:** a Speech to text tab on SW-943's `/admin/translations` page. It holds the manifest, with one active model per language, and a latency table by model pair, built from each line's record.
- **D20. Presenter window:** a CC chip next to "Reading in" shows live, paused, "left the app" or phone offline.
- **D21. Sign-in:** the server address, then Sign in with Google in the system browser, or scan a pairing QR code from the laptop as a shortcut.
- **D22. Which models run:** "Listening for" English and ไทย chips in the Subtitles sheet, both on by default when both models are on the phone. Turning one off runs a single model and saves battery.
- **D23. Captions switch:** on the laptop, Captions sits next to Join bar in the live chip. On the phone, it is a switch in the Subtitles sheet, and the action row stays at five buttons.
- **D24. Pairing QR code:** a Phone button in the presenter window's toolbar shows a QR code. It signs the app in on this server and opens the talk, and works once, for 2 minutes.

Proposed in the mockups for the plan to confirm:

- **Hub messages.** The app sends `SendCaption` (seq, partial or final, lang, slide, heard time, speech-to-text time, model id, confidence). Guests get `Caption` in their language group, with `from` and `translated`. `CaptionsState` (live, paused, reconnecting or off, who paused, languages listened for, big screen on or off) reaches every group at once.
- **Manifest.** `GET /api/speech/models`. `languages` is a list, so a future bilingual model is just another entry.

## SW-885 Live speech V2: listening

Open [`sw-885-listen/sw-885-mockups.html`](sw-885-listen/sw-885-mockups.html) in a browser. V1 (SW-884) puts translated subtitles on guests' phones. V2 lets a guest put headphones in and hear each translated line, read by the phone's own voice. Mobile web gets a Listen button. The app gets an audience mode that keeps playing with the screen locked. The speaker gets a microphone tip and a count of who is listening. The guest page, the subtitle strip and the speaker app are SW-884's mockups; only the listening parts are new.

The live demo works. Sam talks in the speaker app, a guest listens on mobile web in ไทย, 日本語, or Traditional Chinese read in Cantonese or Mandarin, and the guest's speech queue shows each line as waiting, reading, read or skipped. "Talk fast" makes Sam outrun the voice so the D9 catch-up shows. "Hear it in this browser" reads the lines with the browser's own `speechSynthesis` voice, and says when the browser has no voice for the language. Without it, durations are estimated from the text. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: what V2 ships, what V1 already has, and what is elsewhere | [01-scope](sw-885-listen/png/sw-885-01-scope.png) |
| Live demo: the speaker app, a listening guest and the guest's speech queue | [02-demo](sw-885-listen/png/sw-885-02-demo.png) |
| Mobile web: the Listen button, the first-tap sheet, listening, catching up, no voice for the language, Cantonese or Mandarin, back after the screen locked, a guest in the speaker's language, paused | [03-web](sw-885-listen/png/sw-885-03-web.png) |
| The app's audience mode: first open, scan the code, following the talk, the Listen sheet, getting a better voice, the mini player, the lock screen, headphones out, signed in with both modes | [04-app](sw-885-listen/png/sw-885-04-app.png) |
| Speaker side: the microphone tip, the Microphone row, a lapel mic, headphone counts in the Subtitles sheet and the presenter window | [05-speaker](sw-885-listen/png/sw-885-05-speaker.png) |
| How a line gets from the speaker to a guest's ears, the guest's speech queue and the hub messages | [06-flow](sw-885-listen/png/sw-885-06-flow.png) |
| The decisions | [07-decisions](sw-885-listen/png/sw-885-07-decisions.png) |

### Decisions (2026-11-28)

Settled before the mockups:

- **D1. Translation:** on the server, once per language (SW-884 D2), not on each guest's device.
- **D2. Mobile web:** gets subtitles in V1 and a Listen button in V2. The app's reasons to exist are audio with the screen locked, headphones and voice choice.
- **D3. Voice:** the device's system voice for the guest's language (`speechSynthesis`, `AVSpeechSynthesizer`, Android `TextToSpeech`), with an explicit locale. Server neural voices may come later as a premium.
- **D4. One app:** a speaker mode and an audience mode in one app, with one store listing.
- **D5. Languages:** the 15-language guest catalogue for subtitles and voice.
- **D6.** Cloud speech to text was planned for V2; D20 moves it out.
- **D7. Microphone:** a tip, not a test. A phone mic on a lectern is poor; a lapel mic is much better.
- **D8. V3** is multilingual typed Q&A (SW-944). There are no spoken questions.
- **D9. Falling behind:** with more than 3 lines waiting, finish the current line and jump to the newest.

From the mockup review:

- **D10. Listen on mobile web:** a Listen button in the subtitle strip's header, next to Hide. The first tap opens a sheet (voice, speed, a headphone reminder), and its Start button is the user gesture iOS needs before it will speak.
- **D11. What is read:** only lines translated into the guest's language. A guest reading in the speaker's language gets no Listen button. In a mixed talk, lines spoken in the guest's language are skipped.
- **D12. Screen lock on web:** the page holds the screen on with the Wake Lock API while listening. If it locks anyway, speech stops, and the page offers Resume and one line about the app.
- **D13. No voice for the language:** the Listen sheet says so, keeps Start off, shows how to add the voice, and subtitles carry on. A local text-to-speech model for these languages is SW-982.
- **D14. App home:** Join a talk comes first, by code or QR code, with no sign-in. Sign in to present sits below it; once signed in, Presenting now appears under Join.
- **D15. Join links:** slideworks.live links and QR codes open the app when it is installed (app links). Mobile web mentions the app only inside the Listen sheet.
- **D16. Headphones out:** the app pauses, like podcast apps, with one tap to play on the speaker. It is a switch in the app's Listen sheet. Mobile web can't tell, so its sheet asks for headphones.
- **D17. Lock screen:** Now Playing with the slide as artwork, the deck and language as title, Pause, and Jump to live, which clears the queue.
- **D18. Who is listening:** a headphone count per language in the Reading in counter, on the laptop and in the Subtitles sheet. Counts only, never who.
- **D19. Microphone tip:** once per phone, as the first screen of the Subtitles setup sheet. After that, a Microphone row in the sheet names the mic in use, orange for the phone's own, and links back to the tip.
- **D20. Cloud speech to text:** not in V2. It is its own Feature, SW-981, which records how the phone reaches the provider and what happens when the connection drops.
- **D21. The subtitle strip keeps a fixed height** while lines arrive and are read. The line being read is clamped to 3 lines, the next one to 1, and the skip note has a reserved row. It grows only in extreme cases, so the page doesn't jump.

From the landscape round (section 05). They build on SW-5's full-screen landscape, SW-883's tap overlay and Android Full screen button (SW-883 D6), and SW-884's film captions (SW-884 D17):

- **D22. Captions in landscape:** over the bottom of the slide, film style. The newest line is at the bottom; while listening, the line being read has a purple edge and the next one waits under it. The guest can move them into a fixed-height band below the slide, which shrinks the slide about 18%.
- **D23. The Captions menu:** the overlay's CC button opens show or hide, size S/M/L, and over or below the slide. The choice is remembered on the phone; upright, the portrait strip comes back.
- **D24. The listening rail:** in the black bar beside the 16:9 slide, on the side away from the island: headphones, level bars, the language, and the skipped count for 6 seconds. It turns amber when paused. A phone with a 16:9 screen has no bar, so it becomes a corner pill over the slide.
- **D25. Captions off keeps listening on** in landscape.
- **D26. The app goes immersive when sideways:** only the guest screen unlocks rotation. Status and system bars are hidden, the home indicator auto-hides, and the screen stays awake. A swipe from the edge brings the bars back. The mini player folds into the rail.
- **D27. A poll opening in landscape:** a chip over the top of the slide, "Poll open · turn upright to vote", until the guest turns upright or the poll closes.

[`sw-885-listen/design-notes.md`](sw-885-listen/design-notes.md) has the same decisions plus the landscape implementation notes and what to check during planning (safe areas, black-bar width, caption contrast, iPad full screen, the poll chip's state).

Proposed in the mockups for the plan to confirm:

- **The guest's speech queue.** Only final lines translated into the guest's language are queued. Each is spoken with `lang`, `voice` and `rate` set, and the next one starts on `onend`. With more than 3 waiting, the older ones are dropped and the strip says how many.
- **Hub messages.** The guest sends `SetListening({ listening })`. The presenter and speaker app get `AudienceRoster` with `reading` and `listening` counts per language. Captions, groups and translation are V1's.

## SW-943 Translation engine

Open [`sw-943-translation-engine/sw-943-mockups.html`](sw-943-translation-engine/sw-943-mockups.html) in a browser. SW-943 is server side only. It is one translation engine that runs open models on the server's CPU, with no cloud model. Live subtitles (SW-884) and multilingual Q&A (SW-944) both call it. Which models exist is set in server config. A new admin page at `/admin/translations` picks the default model, overrides it per language pair, and has a Try it box for playing with the models.

The admin page in the mockup works. You can change the default, change, add or remove a pair, and translate a sample in Try it, one model at a time or both side by side. Switches above the frame fake a model that is down, a pair whose model a deploy removed, and no overrides. Latency figures and translations are made up. The PNGs are screenshots of each section at 1300 px wide.

| Screen | PNG |
|---|---|
| Scope: server config, the admin page, the engine and the two model containers | [01-scope](sw-943-translation-engine/png/sw-943-01-scope.png) |
| `/admin/translations`, Models and pairs: model cards with health and last-hour latency, the default model, pair overrides and the every-pair map | [02-models](sw-943-translation-engine/png/sw-943-02-models.png) |
| Try it: both models side by side, with time, tokens and what was sent | [03-try](sw-943-translation-engine/png/sw-943-03-try.png) |
| The same page with TranslateGemma down and a pair whose model was removed by a deploy | [04-gone](sw-943-translation-engine/png/sw-943-04-gone.png) |
| Other states: a model that can't do a pair, a duplicate pair, a default that misses a language, a timeout, not an admin, no models configured | [05-states](sw-943-translation-engine/png/sw-943-05-states.png) |
| How the engine picks a model, with worked examples | [06-resolve](sw-943-translation-engine/png/sw-943-06-resolve.png) |
| What runs on the host: the llama-server containers, the config and compose sketches, and each model's prompt | [07-server](sw-943-translation-engine/png/sw-943-07-server.png) |
| The API, tables and metrics the page needs (proposed) | [08-contracts](sw-943-translation-engine/png/sw-943-08-contracts.png) |
| The questions and the answers given | [09-questions](sw-943-translation-engine/png/sw-943-09-questions.png) |

### Decisions (2026-11-28)

Settled before the mockups:

- **D1. Models:** Tencent Hy-MT2 1.8B (Apache 2.0) is the default. It covers all 15 catalogue languages plus Cantonese, about 1 GB at Q4. Google TranslateGemma 4B (Gemma terms) is the second. Only these two at first.
- **D2. Pairs:** an override applies in both directions. One row "Thai ↔ English → model" covers th → en and en → th.
- **D3. Running the models:** one llama-server (llama.cpp) container per model, internal network only, OpenAI-compatible API, both loaded at start (about 1.1 GB + 2.5 GB of RAM).
- **D4. Where config lives:** server config (`Translation:Models`: id, display name, endpoint, adapter, languages, timeout, parallel) says which models exist. The database holds the default model and the pair overrides, which apply to the next sentence without a restart.
- **D5. Glossary:** models that can't take a glossary are still used. The adapter leaves the glossary and context out for TranslateGemma, whose template takes only text and language codes. Hy-MT2 gets them.
- **D6. Try it:** the admin page has a Try it box: type text, pick a pair and a model, and see the result and its time.

From the mockup review:

- **D7. Getting there:** an Admin sub-nav (People · Translations) at the top of every admin page. The top bar's Admin link keeps its badge and still opens People.
- **D8. Saving:** each change saves at once, as on People, with a Saved note and a toast.
- **D9. A model that is down or slow:** if the health check (every 30 s) says a model is down, its pairs go to the default, when the default is up and supports the pair. A single timeout gives "not translated" for that sentence, so a subtitle never waits on two models.
- **D10. Cantonese:** `yue` (Cantonese, 粵語, traditional-chinese script) is added to `languages.json` in SW-943. Only Hy-MT2 supports it, so only Hy-MT2 can be picked for its pairs. It also becomes a slide translation language (SW-883).
- **D11. Try it side by side:** Try it offers "As live" (the model a real sentence would get), each model on its own, or both side by side. Its calls are recorded in metrics with `source=try`.
- **D12. A model removed by a deploy:** a pair override that still names it stays, flagged "no longer in the server config", and the default is used until the admin changes or removes the row.
- **D13. Every-pair map:** a 16 × 16 map below the overrides table shows which model translates each pair after overrides and the default, and where nothing does.

Proposed in the mockups for the plan to confirm:

- **API.** `GET /api/admin/translations` returns the whole view (models with health and last-hour p50, p95 and calls; the default; the pairs). `PUT …/default`, `PUT …/pairs/{a}/{b}`, `DELETE …/pairs/{a}/{b}` and `POST …/try` each answer with the refreshed view or the result, the same pattern as `/api/admin/users`.
- **Tables.** `TranslationSettings` (default model, seeded from `Translation:DefaultModel`) and `TranslationPairOverrides` (the pair sorted, model, changed at and by). A removed row is soft-deleted with `RemovedAt` and a partial unique index on the live pair.
- **Metrics.** Every call records the model id, pair, tokens in and out, latency and outcome (translated, not supported, timed out, unreachable, not configured) to VictoriaMetrics, plus one Seq event.
