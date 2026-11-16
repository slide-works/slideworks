# SlideWorks

Web slide decks that agents build over MCP and anyone presents from a URL.

An agent builds a deck from ready-made building blocks (title, divider, bullets, compare, table, chart, timeline and more) without writing any HTML. A person opens the deck by link, shares it, and presents it in the browser on a laptop or a phone. Later work adds audience features: QR join, live polls, word clouds, Q&A and a phone remote.

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
dotnet test src/SlideWorks.slnx                 # unit, API and acceptance tests (build the client first, see below)
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

## Realtime

Live updates go over one SignalR hub on WebSockets:

```
publisher → IDeckEvents (in-process bus) → DeckEventsRelay → DeckHub group "deck:<id>" → subscribeToDeck → screen
```

- Server: `src/SlideWorks.WebApi/Realtime/`. `DeckHub` at `/hubs/deck` (WebSockets only) has `JoinDeck(deckId)` and `LeaveDeck(deckId)` and sends `deckEvent` messages. Events are records deriving from `DeckEvent`, serialised with a `type` discriminator. Publish with `IDeckEvents.PublishAsync`; delivery is off the request and in order.
- Client: `src/SlideWorks.Client/src/realtime/`. `subscribeToDeck` connects, joins, rejoins after a reconnect and retries the first connect with back-off; `useDeckRealtime(deckId, onEvent)` wraps it for a screen. `deckEvents.ts` mirrors the event types.
- Auth: anyone may connect, so a viewer of a deck shared by link can follow it without signing in. The session cookie identifies a signed-in person, and `JoinDeck` refuses a deck the caller can't view. The cross-site write guard refuses a negotiate or connect that carries another site's `Origin`.
- One process: the bus is in memory and SignalR keeps groups per process. A second instance needs a cross-process bridge and a SignalR backplane first.

## Configuration

Every setting is an environment variable. [`docs/self-hosting.md`](docs/self-hosting.md) explains each one, the TLS-proxy setup and how agents connect.

| Variable | What |
|---|---|
| `SLIDEWORKS_PUBLIC_URL` | Required outside Development. The URL people and agents reach SlideWorks at. It is the only source of the app's scheme and host; `X-Forwarded-*` and `Host` are never trusted for them. Development uses `http://localhost:3000` |
| `SLIDEWORKS_GOOGLE_CLIENT_ID`, `SLIDEWORKS_GOOGLE_CLIENT_SECRET` | Sign in with Google, on only when both are set. Redirect URI `<public URL>/signin-google` |
| `SLIDEWORKS_ADMIN_EMAIL` | The first admin's Google email, honoured only while no admin exists. New people wait on `/pending` until an admin approves them on `/admin/users` |
| `SLIDEWORKS_DB_PATH` | SQLite file, default `/data/slide-works.db`. The cookie keys live in `slide-works-keys/` next to it |
| `SLIDEWORKS_DATA_PATH` | Data folder for uploaded images (under `images/<deck id>/`), default `/data` |
| `SLIDEWORKS_SEQ_URL`, `SLIDEWORKS_SEQ_API_KEY` | Also log to Seq when set; console only otherwise |
| `SLIDEWORKS_TRUSTED_PROXIES` | Not implemented yet. Planned: proxy CIDRs whose `X-Forwarded-For` gives the client IP for the OAuth rate limits |

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
