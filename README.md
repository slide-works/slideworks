# SlideWorks

Web slide decks that agents build over MCP and anyone presents from a URL.

An agent builds a deck from ready-made building blocks (title, divider, bullets, compare, table, chart, timeline and more) without writing any HTML. A person opens the deck by link, shares it, and presents it in the browser on a laptop or a phone. A presenter can give the talk from a laptop with a presenter window (notes, timer, overview) while the audience window follows, and drives the talk from a phone remote. An audience joins a live talk from their phones with a QR code or a six-digit code and follows the slide on screen. The audience can answer live polls, ask and upvote questions, and send reactions.

Designs are in [`design/`](design/). The plan lives in the ShipLean project **SW**: SW-1 is the foundation, SW-2 to SW-15 the increments after it.

## Stack

ASP.NET Core (.NET 10), React SPA (Vite + TypeScript + Tailwind in `src/SlideWorks.Client`). One container, published to GHCR on every push to `main`.

Data lives in SQLite through EF Core; migrations are committed and applied on startup.

## Run locally

Two processes side by side; the Vite dev server proxies `/api`, the `/hubs` WebSockets, `/mcp`, `/oauth`, `/.well-known` and the server-side sign-in paths (`/signin/*`, `/signin-google`) to Kestrel, so the browser and agents only ever talk to the Vite origin (no CORS setup):

```bash
dotnet run --project src/SlideWorks.WebApi   # Kestrel on :5510, settings from appsettings.Development.json
cd src/SlideWorks.Client && pnpm install && pnpm run dev   # Vite with HMR on :3000, proxies to :5510
```

Open http://localhost:3000, not :5510. `appsettings.Development.json` sets `SLIDEWORKS_PUBLIC_URL=http://localhost:3000`, so the server treats the Vite origin as its own: links, the sign-in callback and the OAuth metadata name it, and the cross-site write guard accepts writes from it and refuses them from anywhere else. It also points the database and images at local relative paths (`slide-works-dev.db`, `slide-works-dev-data/`).

To sign in locally, create a Google OAuth client with the redirect URI `http://localhost:3000/signin-google` and give the server its ID, its secret and your email as environment variables (keep the secret out of committed files):

```bash
SLIDEWORKS_GOOGLE_CLIENT_ID=<client id> SLIDEWORKS_GOOGLE_CLIENT_SECRET=<client secret> \
SLIDEWORKS_ADMIN_EMAIL=you@example.com dotnet run --project src/SlideWorks.WebApi
```

Connect a local agent to `http://localhost:3000/mcp`, for example `claude mcp add --transport http slideworks http://localhost:3000/mcp`.

Tests:

```bash
dotnet test src/SlideWorks.slnx                 # the full suite, for people (build the client first, see below); agents run filtered tests with tools/agent, and CI runs everything
cd src/SlideWorks.Client && pnpm test           # frontend unit tests (also: lint, format:check, build)
```

`pnpm run build` compiles the SPA straight into `src/SlideWorks.WebApi/wwwroot` (gitignored), which the API serves in production — same wiring as the Docker image.

## Acceptance tests (Playwright)

`src/SlideWorks.AcceptanceTests` starts the real app on a Kestrel port (production `Program.cs`, temp database) and drives the SPA bundle in a browser. Build the client first, and install the browser once:

```bash
cd src/SlideWorks.Client && pnpm install && pnpm run build   # emits the bundle into src/SlideWorks.WebApi/wwwroot
dotnet build src/SlideWorks.AcceptanceTests
pwsh src/SlideWorks.AcceptanceTests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
dotnet test src/SlideWorks.AcceptanceTests
```

Static analysis (SonarAnalyzer.CSharp and Agoda.Analyzers) applies to every project through `src/Directory.Build.props`, with warnings as errors.

## Presenting

The owner of a deck presents it from the Present menu: a presenter window (notes, timer and overview) opens beside an audience window that follows the talk, and a phone signed in as the owner works as a remote. Everything goes through `DeckHub`, one shared state per talk. See [`docs/presenting.md`](docs/presenting.md) for the keys, deep dives and what happens when the Wi-Fi drops. A deck can also have a talk length, set through the API or the `set_talk_length` MCP tool (there is no web UI for it); the timer shows the time left. A deck's parts (names, short names, captions, marks) can be replaced after creation with the `set_parts` MCP tool; there is no web UI for it.

## Realtime

Live updates go over one SignalR hub (WebSockets, with Server-Sent Events and long polling as fallbacks):

```
publisher → IDeckEvents (in-process bus) → DeckEventsRelay → DeckHub group "deck:<id>" → subscribeToDeck → screen
```

- Server: `src/SlideWorks.WebApi/Realtime/`. `DeckHub` at `/hubs/deck` (WebSockets, with Server-Sent Events and long polling as fallbacks) has `JoinDeck(deckId)` and `LeaveDeck(deckId)` and sends `deckEvent` messages. For the audience it has `JoinAudience(code)` and `LeaveAudience(code)` for guests (group `audience:{deckId}`) and `WatchAudience(deckId)` for the owner (group `audience-watch:{deckId}`), and sends `audienceEvent` messages. Deck events are records deriving from `DeckEvent`, serialised with a `type` discriminator. Publish with `IDeckEvents.PublishAsync`; delivery is off the request and in order. Audience events derive from `AudienceEvent` and go through `IAudienceEvents` and `AudienceEventsRelay`.
- Client: `src/SlideWorks.Client/src/realtime/`. `subscribeToDeck` connects, joins, rejoins after a reconnect and retries the first connect with back-off; `useDeckRealtime(deckId, onEvent)` wraps it for a screen. `deckEvents.ts` mirrors the event types.
- Auth: anyone may connect, so a viewer of a deck shared by link can follow it without signing in. The session cookie identifies a signed-in person, and `JoinDeck` refuses a deck the caller can't view. The cross-site write guard refuses a negotiate or connect that carries another site's `Origin`.
- One process: the bus is in memory and SignalR keeps groups per process. A second instance needs a cross-process bridge and a SignalR backplane first.

## Audience join

- Every deck has a standing six-digit join code. **Go live** (the live chip in the player) opens a session behind it, and **End** closes it. Guests who arrive before Go live wait in a lobby.
- A guest opens `/join` and types the code, or scans the QR code, which opens `/j/<code>`. There is no name and no account. The phone mirrors the slide on screen, with no notes, timer or other slides sent to it.
- The `join` layout shows the code, the QR code and, if the layout allows it, how many have joined. A deck setting puts a small QR code and the code on every slide while live.
- The joined count, on the live chip and the `join` layout, counts each guest who joined the session once. It doesn't drop when a guest's phone locks or switches app, which closes the connection. A poll's response rate still divides by the guests connected now.
- Set `SLIDEWORKS_JOIN_URL` for a short join domain such as `https://slideworks.live`. See [`docs/self-hosting.md`](docs/self-hosting.md#live-audience-join) for DNS, TLS and what the proxy must keep.
- Peak joined per session and the median time from the join slide to each join are SW-5's Measures, two queries in [`docs/measures.md`](docs/measures.md).

## Live polls

- A poll is a slide with the `poll` layout: a question of up to 120 characters and 2 to 8 options of up to 60 characters each, every option with its own text. An agent adds one with `add_slide`. The other settings are not slots: how many picks (one, or up to a number), when the counts show (live; after the poll closes; or after the guest has answered, which shows each guest the counts on their own phone once they have answered, and the room only when the presenter reveals them or the poll closes), the correct answer and whether it opens when the presenter reaches the slide or by hand. An agent sets them with `update_poll`.
- While the deck is live, a poll that opens on arrival opens when the presenter reaches its slide. A poll set to open by hand waits for Open. Either kind closes when the presenter moves to another slide. A phone that joined keeps showing the slide, and the poll card, with the question and the options, takes the place of the waiting card below it. A guest answers with a tap. Pick several has a Send button. A guest can change an answer while the poll is open, and each answer counts once.
- The presenter window has a poll panel and the phone remote has a poll strip. Both open and close the poll, reveal the counts, show the correct answer and start a new round, and earlier rounds are kept. In the presenter window the keys are `P` (open or close), `R` (reveal) and `A` (show answer). New round has no key.
- Only a person who can present the deck can run a poll, and the server checks it on every call. An agent with edit access can open and close a poll on a live deck (`open_poll`, `close_poll`) and read its results (`get_poll_results`), but can't reveal, show the answer or start a new round.
- Counts a guest may not see are never sent to that guest, and nobody can list who picked what: results are totals only.
- A guest can send 30 answers a minute. The server publishes new counts at most twice a second for each deck.
- The response rate of each closed round (the guests who answered, out of the guests who had joined when it closed) is SW-6's Measure, a query in [`docs/measures.md`](docs/measures.md). The 1,000-voter load run is in [`docs/load.md`](docs/load.md).

## Audience Q&A and reactions

- While a deck is live, its guests get a second tab on their phone, Questions, beside the slide. A guest asks a question of up to 200 characters and upvotes the ones they want answered. Questions and upvotes belong to the live session and start fresh on the next Go live. A guest can ask 3 questions a minute and change their upvotes 30 times a minute, counted apart from poll answers. A guest can take back an upvote on their own question, as on any other, but an upvote on an answered question can't be added or removed.
- A question is anonymous unless the guest turns on "Show my name" and gives a name of up to 30 characters, which shows with that question only. A question is waiting, open or answered. Hidden and on screen are flags beside that: hiding works on a waiting or open question, and restoring brings back the state it had.
- Five reactions, under the slide on the phone: thumbs up, heart, laugh, wow and clap. They float up the edge of the big screen (30 at once, the rest fold into a "+N" chip) and add to a counter in the presenter panel. Other phones don't receive them. A guest can tap 10 times in 5 seconds. The server keeps totals for each session, slide and emoji, written every 5 seconds and when the session ends, and never a row per tap, so a restart loses up to 5 seconds of counts.
- Only a person who can present the deck sees questions and reactions on the big screen. In the presenter window `Q` opens and closes the Questions panel, where the presenter shows a question on screen, marks it answered, hides it or approves it. The phone remote has a Questions strip.
- Settings are per deck: questions `open`, `closed` (the list and upvotes stay) or `off` (no round and no Questions tab), review first (new questions wait for approval), names, reactions, and `show_on`. With `anySlide`, the default, Show on screen lays a card over whatever slide is up. With `qaSlide` a shown question appears only while a `qa` slide is up. A slide can also turn questions, reactions or both off while it is on screen: the reaction row hides and asking says "Questions are off on this slide", while the list and upvotes stay. Anyone who can present the deck changes them, live or not, from the presenter panel, the "Q&A and reactions" card on the deck page, or `update_qa`.
- The `qa` layout is the Q&A slide: its title over the room's five most upvoted open questions, or the one question shown on screen. An agent adds it with `add_slide`.
- A guest who can't ask gets a 409 with `questions_closed`, `questions_off` or `questions_off_here`. Phones receive the votes of only the questions another guest may see, at most twice a second. A guest's id is never sent with a question, an upvote or a reaction, and no one can list who asked or upvoted.
- An agent with edit access reads the room's questions with `get_questions` (text, name shown, state, votes and when asked, for each session that ran Q&A, newest first) and changes the settings with `update_qa`. It can't approve, hide, answer or show a question. `get_deck` returns the deck's `qa` settings and each session's reaction totals for each slide.
- The Measure is the questions asked in each session, and the share of the guests who joined who asked, upvoted or reacted at least once, both queries in [`docs/measures.md`](docs/measures.md). The 1,000-guest run is in [`docs/load.md`](docs/load.md).

## Templates and palettes

A deck's look is a **template** plus a **palette**, and nothing else. There are no themes, and a slide never stores a
colour or a font.

- **Templates** hold structure and house style: the layouts allowed, the chrome defaults (part tracker, footer, progress
  bar), fonts, radius and shadow, title art and ornament. Built in: `talk`, `workshop` and `update`. A template can
  carry its own palette and lock it, so picking that template also picks its palette.
- **Palettes** hold every colour: surfaces, ink, the accent family, the roles (positive, negative, caution, deep dive),
  dark slides and the tone-panel pairs. Built in: Cobalt (the default), Ember, Plum and Midnight. Each one passes a
  contrast check over its colour pairs, and the build fails if a pair drops below its bar.
- The built-in catalogue is data in `src/contracts/templates.json` and `src/contracts/palettes.json`. A person can also
  make templates of their own: fonts, logos, title art, colours and layouts, published as versions and shared like a
  deck. `GET /api/looks` is per person: it lists the built-ins and the templates that person owns or was given.
  Agents read the same list with `list_templates` and `list_palettes`. How to make and use a template is in
  [`docs/templates.md`](docs/templates.md), and the layouts added with them are in [`docs/layouts.md`](docs/layouts.md).
- A deck picks its look when it's made (the New deck dialog, or `create_deck`) and can change it at any time (the deck
  page's Look panel, `PUT /api/decks/{id}/look`, or `set_look`). A deck can also switch the template's chrome on or
  off and add a logo. Colours change only by picking another palette: there is no free colour picker.
- A slide flagged `deep_dive` gets the deep-dive bar and pill, is listed on its part's divider (unless the template
  turns that off), and its speaker notes start with "Deep dive. Skip if short of time." The prefix is added when the
  notes are read, so clearing the flag removes it.
- How many new decks keep a built-in look unchanged is SW-2's Measure, a query in [`docs/measures.md`](docs/measures.md). The uploaded-templates Measure (SW-50) is in [`docs/measures.md#uploaded-templates-sw-50`](docs/measures.md#uploaded-templates-sw-50).

## Slide previews and share cards

- **Previews are rendered by the agent.** `preview_deck` returns one render URL template (with `{slide}` in it), the player URL, the share
  card URL, the rendered images, the findings for those slides and instructions. `check_deck` returns the same findings without the images. What each finding means and how to fix it: [`docs/agent-checks.md`](docs/agent-checks.md). The agent screenshots each URL at 1920×1080 in its own headless Chromium. The
  instructions pin Playwright 1.62.0 and install only what's needed (`npx -y playwright@1.62.0 install --only-shell
  chromium`). The server renders no slides.
- **The share card is rendered by the server.** `GET /api/cards/{id}.png` is anonymous and returns a 1200×630 PNG for
  link previews. Only a deck shared by link gets its own card, with its title. A private deck, a named-viewer deck or an
  unknown id gets the generic card and never starts a deck render. If the renderer is unavailable or times out, the
  route answers 503 with `Retry-After`. If a deck's card fails to render (for example a broken logo), the route serves
  the generic card. `GET /d/{id}/{slug}` serves the player with Open Graph tags that point at the card.
- **The image carries Chromium for the share card only.** The Dockerfile installs it with the Playwright driver that
  ships with `Microsoft.Playwright`, into `/ms-playwright`, and installs only the headless shell, not the full
  browser. It makes the runtime image much larger: about 0.7 GB without it, and roughly 1.7 GB with it.
- Render URLs carry a short-lived signed token in the URL fragment, never in a query string. Pages send it as the
  `X-Render-Token` header. A token reads only the one deck it names, for one purpose: a preview token lasts 10
  minutes, and a card token 60 seconds.

## Configuration

Every setting is an environment variable. [`docs/self-hosting.md`](docs/self-hosting.md) explains each one, the TLS-proxy setup and how agents connect.

| Variable | What |
|---|---|
| `SLIDEWORKS_PUBLIC_URL` | Required outside Development. The URL people and agents reach SlideWorks at. It is the only source of the app's scheme and host; `X-Forwarded-*` is never trusted, and `Host` only chooses between this and the join URL. Development uses `http://localhost:3000` |
| `SLIDEWORKS_JOIN_URL` | Optional. A short domain for the join links an audience scans, such as `https://slideworks.live`: an absolute `http` or `https` origin with no path, or the server refuses to start. Unset, join links are `<public URL>/j/<code>` |
| `SLIDEWORKS_GOOGLE_CLIENT_ID`, `SLIDEWORKS_GOOGLE_CLIENT_SECRET` | Sign in with Google, on only when both are set. Redirect URI `<public URL>/signin-google` |
| `SLIDEWORKS_ADMIN_EMAIL` | The first admin's Google email, honoured only while no admin exists. New people wait on `/pending` until an admin approves them on `/admin/users` |
| `SLIDEWORKS_DB_PATH` | SQLite file, default `/data/slide-works.db`. The cookie keys live in `slide-works-keys/` next to it |
| `SLIDEWORKS_DATA_PATH` | Data folder for uploaded images (under `images/<deck id>/`) and for template files (under `templates/<template id>/`), default `/data` |
| `RenderTokens__Key` | Signs the render tokens (preview and share card). Required outside Development: the app refuses to start without it. Use a random secret of at least 32 characters, such as `openssl rand -base64 32`. `src/docker-compose.yml` reads it from `SLIDEWORKS_RENDER_TOKEN_KEY`. In Development with no key, each process makes a random one |
| `SLIDEWORKS_RENDER_URL` | The origin the share-card browser opens. Unset means the app's own loopback address (a wildcard listen address such as `http://+:8080` becomes `127.0.0.1`). Set it only when the app can't reach itself that way |
| `SLIDEWORKS_RENDER_TIMEOUT` | How long one share-card render may take, from launching the browser to the PNG, as a TimeSpan such as `00:00:20` (the default) |
| `SLIDEWORKS_SEQ_URL`, `SLIDEWORKS_SEQ_API_KEY` | Also log to Seq when set; console only otherwise |
| `SLIDEWORKS_TRUSTED_PROXIES` | Unset by default. Comma-separated CIDRs (or single addresses) of the proxy in front, such as `172.18.0.0/16`. A request from one of them takes its client IP from the last `X-Forwarded-For` entry, for the per-caller OAuth rate limits only; `X-Forwarded-Proto` and `X-Forwarded-Host` are never read. A bad entry stops startup |

`GET /health` is anonymous and reports the commit the image was built from.

## Connecting an agent

Agents use the MCP server at `<public URL>/mcp`, behind SlideWorks' own OAuth server, so a client needs only the URL: it registers itself, and you sign in and approve it in the browser. Claude Code, Claude Desktop and claude.ai, VS Code and Cursor are supported:

```bash
claude mcp add --transport http slideworks https://slides.example.com/mcp
```

See [`docs/self-hosting.md`](docs/self-hosting.md#5-connect-an-agent) for the other clients.

## Component tests (Playwright)

```bash
cd src/SlideWorks.Client && pnpm run test-ct   # functional assertions; screenshots may false-fail locally
```

Screenshot assertions (`toHaveScreenshot()`) will likely fail on your machine even on unchanged code: the fonts installed there don't match the container the baselines were generated in. Never commit a baseline made outside that container. CI's `playwright` job runs in it, and a missing baseline makes the job write the image and attach it to the `playwright-report` artifact as `<name>-actual.png`; check it, then commit it as `<spec>.spec.tsx-snapshots/<name>-linux.png`. Or regenerate inside the same image locally:

```bash
cd src
docker run --rm -v "$PWD":/work -w /work/SlideWorks.Client mcr.microsoft.com/playwright:v1.62.0-resolute \
  bash -c "corepack enable && pnpm install --frozen-lockfile && pnpm exec playwright test -c playwright-ct.config.ts --update-snapshots=all"
```

It mounts all of `src/`, not just the client, because the component tests import `@contracts` from `src/contracts`. The container runs as root, so the files it writes in `node_modules` and the snapshot folders end up owned by root.

## Deploy

`docker-publish.yml` pushes `ghcr.io/<owner>/<repo>:latest` and `:<sha>` on every push to `main`. `src/docker-compose.yml` is the shape to run it in, and [`docs/self-hosting.md`](docs/self-hosting.md) walks a fresh server from `docker compose up` to an agent building a deck.
The database, cookie keys and images sit on a named volume at `/data`, never a bind mount onto a network share.

## License

[MIT](LICENSE)
