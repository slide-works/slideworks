# SlideWorks

Web slide decks that agents build over MCP and anyone presents from a URL.

An agent builds a deck from ready-made building blocks (title, divider, bullets, compare, table, chart, timeline and more) without writing any HTML. A person opens the deck by link, shares it, and presents it in the browser on a laptop or a phone. Later work adds audience features: QR join, live polls, word clouds, Q&A and a phone remote.

Designs are in [`design/`](design/). The plan lives in the ShipLean project **SW**: SW-1 is the foundation, SW-2 to SW-15 the increments after it.

## Stack

ASP.NET Core (.NET 10), React SPA (Vite + TypeScript + Tailwind in `src/SlideWorks.Client`). One container, published to GHCR on every push to `main`.

Data lives in SQLite through EF Core; migrations are committed and applied on startup.

## Run locally

Two processes side by side; the Vite dev server proxies `/api` and the `/hubs` WebSockets to Kestrel, so the browser only ever talks to the Vite origin (no CORS setup):

```bash
dotnet run --project src/SlideWorks.WebApi   # Kestrel on :5510, settings from appsettings.Development.json
cd src/SlideWorks.Client && pnpm install && pnpm run dev   # Vite with HMR on :3000, proxies /api and /hubs -> :5510
```

Open http://localhost:3000 and enter the token (`dev-token` in dev).

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

Live updates go over one SignalR hub on WebSockets, the same shape ShipLean and Codex use:

```
publisher → IDeckEvents (in-process bus) → DeckEventsRelay → DeckHub group "deck:<id>" → subscribeToDeck → screen
```

- Server: `src/SlideWorks.WebApi/Realtime/`. `DeckHub` at `/hubs/deck` (WebSockets only) has `JoinDeck(deckId)` and `LeaveDeck(deckId)` and sends `deckEvent` messages. Events are records deriving from `DeckEvent`, serialised with a `type` discriminator. Publish with `IDeckEvents.PublishAsync`; delivery is off the request and in order.
- Client: `src/SlideWorks.Client/src/realtime/`. `subscribeToDeck` connects, joins, rejoins after a reconnect and retries the first connect with back-off; `useDeckRealtime(deckId, onEvent)` wraps it for a screen. `deckEvents.ts` mirrors the event types.
- Auth: the hub sits behind the same bearer token as `/api`. The browser sends it as `?access_token=` on the WebSocket upgrade, which the middleware accepts on `/hubs` only.
- One process: the bus is in memory and SignalR keeps groups per process. A second instance needs a cross-process bridge and a SignalR backplane first.

## Configuration

| Variable | What |
|---|---|
| `SLIDEWORKS_API_TOKEN` | Bearer token for `/api` and `/hubs`. Unset means fail closed: everything but `/health` answers 401 |
| `SLIDEWORKS_API_EXTRA_TOKENS` | Comma-separated further tokens, one per other caller, so each can be rotated alone |
| `SLIDEWORKS_DB_PATH` | SQLite file, default `/data/slide-works.db` |
| `SLIDEWORKS_SEQ_URL`, `SLIDEWORKS_SEQ_API_KEY` | Also log to Seq when set; console only otherwise |

`GET /health` is anonymous and reports the commit the image was built from.

## Component tests (Playwright)

```bash
cd src/SlideWorks.Client && pnpm run test-ct   # functional assertions; screenshots may false-fail locally
```

Screenshot assertions (`toHaveScreenshot()`) will likely fail on your machine even on unchanged code: the fonts installed there don't match the container the baselines were generated in. To update a baseline after changing a component, regenerate it inside the image CI uses:

```bash
cd src/SlideWorks.Client
docker run --rm -v "$PWD":/work -w /work mcr.microsoft.com/playwright:v1.62.0-resolute \
  bash -c "corepack enable && pnpm install --frozen-lockfile && pnpm exec playwright test -c playwright-ct.config.ts --update-snapshots=all"
```

## Deploy

`docker-publish.yml` pushes `ghcr.io/<owner>/<repo>:latest` and `:<sha>` on every push to `main`. `src/docker-compose.yml` is the shape to run it in.
The database sits on a named volume at `/data`, never a bind mount onto a NAS path.

## License

[MIT](LICENSE)
