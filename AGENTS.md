# AGENTS.md

## Behavioural norms

- **Don't assume.** Don't hide confusion. Surface tradeoffs and ask before guessing.
- **Simplicity first.** Minimum code that solves the problem. Nothing speculative.
- **Surgical changes.** Touch only what you must. Clean up only your own mess.
- **Goal-driven execution.** Define success criteria up front. Loop until verified.

## Project overview

SlideWorks: web slide decks that agents build over MCP and anyone presents from a URL. Open source (MIT) and generic: no org-specific integrations, branding or registry prefixes in the repo.

ASP.NET Core (.NET 10) API with a React SPA (Vite + TypeScript) served from the same container, with live updates over a SignalR hub on WebSockets.

## Where the plan lives

The ShipLean project **SW** (Slide Works). Each Feature is an SW key: SW-1 is the foundation (decks, player, building blocks, MCP), SW-2 to SW-15 are increments in backlog order, SW-16 set up this repo. Designs and their decisions are in `design/`, one folder per Feature.

## Project layout

| Path | What |
|---|---|
| `src/SlideWorks.slnx` | Solution (XML format — build/test this, not a `.sln`) |
| `src/SlideWorks.WebApi/Features/<Feature>/` | Entity, service, controller, request and view models, grouped by feature rather than by class type. `Notes` is the template's sample; replace it |
| `src/SlideWorks.WebApi/Infrastructure/` | Controller base, exception → status mapping, bearer-token middleware |
| `src/SlideWorks.WebApi/Realtime/` | `DeckHub` (`/hubs/deck`, WebSockets only), the in-process `IDeckEvents` bus, `DeckEventsRelay` (bus → hub groups) and the `DeckEvent` records |
| `src/SlideWorks.WebApi/Data/` | `AppDbContext` |
| `src/SlideWorks.WebApi/Migrations/` | EF Core migrations (committed; applied on startup) |
| `src/SlideWorks.Core/` | Pure, dependency-free logic (e.g. `DisplayDate`). Referenced only by `SlideWorks.WebApi` and `SlideWorks.UnitTest` |
| `src/SlideWorks.WebApi.Tests/` | NUnit black-box tests through `WebApplicationFactory` |
| `src/SlideWorks.AcceptanceTests/` | Playwright (.NET) journeys against the real BFF on Kestrel serving the built SPA bundle; page objects in `Pages/`. Needs the client built first |
| `src/SlideWorks.UnitTest/` | Last-resort NUnit tests for `SlideWorks.Core` helpers; see its `CLAUDE.md` |
| `src/SlideWorks.Client/` | React SPA; `pnpm run build` emits straight into `src/SlideWorks.WebApi/wwwroot` (gitignored) |
| `src/SlideWorks.Client/src/realtime/` | `subscribeToDeck` (connect, join, rejoin, back-off), `useDeckRealtime`, `deckEvents.ts` mirroring the server's event types |
| `src/contracts/templates.json`, `src/contracts/palettes.json` | The look catalogue (SW-2): built-in templates and palettes. `SlideWorks.Core/Look/` reads them and checks each palette's contrast pairs |
| `src/SlideWorks.Client/src/look/` | `lookStyle` (LookView → `--sw-*` custom properties), `lookOf`, `deepDivesOf`, the New deck dialog, the Look panel and the `slidesUseTokens` guard rail |
| `src/SlideWorks.WebApi/Features/Render/`, `Infrastructure/Rendering/` | Render routes, `preview_deck`, the share card (`/api/cards/{id}.png`) and the Playwright card engine |
| `src/SlideWorks.Client/src/**/*.spec.tsx` | Playwright component tests next to the component; screenshot baselines in `*.spec.tsx-snapshots/` (committed) |
| `src/Directory.Build.props`, `.editorconfig` | net10, warnings as errors, Sonar + Agoda analyzers, `TimeProvider`-only clock (`src/BannedSymbols.txt`) |
| `src/Dockerfile`, `src/docker-compose.yml` | Single image: node stage builds the SPA into wwwroot, then dotnet publish; runs as the non-root app user |
| `.github/workflows/` | `ci.yml` (backend, frontend, playwright-in-container) and `docker-publish.yml` (GHCR `:latest` + `:sha` on main) |

## Build / contribute

```bash
(cd src/SlideWorks.Client && pnpm install && pnpm run build)   # once, and after client changes: acceptance tests serve this bundle
dotnet test src/SlideWorks.slnx                     # run before pushing — includes the acceptance tests, so build the client first (CI's backend job skips them; its acceptance job runs them)
dotnet run --project src/SlideWorks.WebApi          # dev: Kestrel on :5510, appsettings.Development.json
cd src/SlideWorks.Client && pnpm run dev            # dev SPA on :3000, proxies /api and /hubs -> :5510
cd src/SlideWorks.Client && pnpm run format:check && pnpm run lint && pnpm test && pnpm run build   # CI's frontend job
cd src/SlideWorks.Client && pnpm run test-ct        # component tests; screenshot asserts may false-fail locally (see below)
dotnet tool restore                               # once, for dotnet-ef
SLIDEWORKS_DB_PATH=/tmp/x.db dotnet ef migrations add <Name> --project src/SlideWorks.WebApi
docker compose -f src/docker-compose.yml up --build
```

Branch per Feature or story (`feature/sw-<n>-<slug>`), PR to `main`, and name the SW key in the PR.

## External resources

- [Agoda.IoC](https://github.com/Agoda-com/Agoda.IoC) — attribute-based DI used here

## Important notes

- **Tests are black-box, in this order of preference**: acceptance tests (`SlideWorks.AcceptanceTests`, Playwright against the real BFF and built bundle), then `WebApplicationFactory` through the HTTP surface (`SlideWorks.WebApi.Tests`, against a temp SQLite file when there is a database); `SlideWorks.UnitTest` only for pure helpers in `SlideWorks.Core`, used sparingly. Acceptance selectors are `data-testid` attributes (`GetByTestId`, enforced by Agoda AG0046), reached through page objects. Fixtures inherit `ApiTestFixture`. Naming: `Method_When_Should`. Never weaken or delete existing tests — see `.claude/skills/preserve-existing-tests/`.
- **DI via Agoda.IoC attributes** (`[RegisterSingleton]`/`[RegisterPerRequest]`/`[RegisterTransient]`) on the class — no `services.Add*` registration in `Program.cs` for our own types.
- **One top-level type per file**, named after the type. Only exception: an interface with exactly one implementing class may sit in that class's file, above it.
- **Controllers are one-liners** delegating to services; contract declared via attributes (`[ProducesResponseType]` etc.); no loops/branching/business logic in actions. Services throw `NotFoundException` / `BadRequestException` / `ConflictException`, and the filter maps them to 404 / 400 / 409.
- **API view models and one request per screen** — see `.claude/skills/api-response-for-browser/`.
- **No `Date` objects in the SPA; dates are pre-formatted strings from the server** — see `.claude/skills/no-browser-datetime/`.
- **The clock is `TimeProvider`.** `DateTime.Now`/`UtcNow` and friends are banned at build time; inject `TimeProvider`, and tests use a `FakeTimeProvider` that starts at the real time and is stepped with `Advance`. No fixed dates in tests unless the value never meets a real clock (pure formatting); AG0051 reports them.
- **Read config at runtime** (via `IServiceProvider`/injected `IConfiguration`), not in top-level `Program` statements — `WebApplicationFactory` test overrides are appended after that code runs and get silently ignored.
- **Outbound HTTP goes through a named `HttpClient`**, so a test stubs that socket by name (`ApiTestFixture.NamedHandlers`) rather than the code growing a test branch.
- **Env vars are prefixed `SLIDEWORKS_`** (`_DB_PATH`, `_API_TOKEN`, `_API_EXTRA_TOKENS`, `_PUBLIC_URL`, `_RENDER_URL`, `_RENDER_TIMEOUT`, `_SEQ_URL`, `_SEQ_API_KEY`). The one exception is the render token key, `RenderTokens__Key` (bound to `RenderTokenOptions`), which the compose file fills from `SLIDEWORKS_RENDER_TOKEN_KEY`. README's Configuration table says which are required. Logs go to the console, and to Seq too once `SLIDEWORKS_SEQ_URL` is set.
- **Live updates go through `IDeckEvents`.** A change a viewer should see live publishes a `DeckEvent` record (add it to `DeckEvent`'s `[JsonDerivedType]` list and to `deckEvents.ts`); never call `IHubContext` from a feature. Events say what changed, not the new content, and the screen re-reads through the API. The hub is WebSockets only and sits behind the `/api` token (as `?access_token=` on `/hubs` only). `JoinDeck` gets its per-deck access check when decks and accounts land (SW-1). The bus and groups are per process: running two instances needs a bridge and a backplane first. Tests: `Realtime/DeckHubTests.cs` connects over a real WebSocket through `HubReceiver`.
- **SQLite stays on ext4** (named Docker volume). Never bind-mount the DB onto a NAS/9p/SMB path.
- **Playwright screenshot baselines are environment-pinned.** `pnpm run test-ct` locally runs functional assertions fine, but `toHaveScreenshot()` will likely fail even on unchanged code — your machine's fonts don't match the `mcr.microsoft.com/playwright:v1.62.0-resolute` image the baselines were generated in and CI runs in. Only regenerate baselines through that container — see README's Playwright section.
- **Looks come only from tokens.** A deck's look is its template plus its palette (`src/contracts/`). Every colour and font in `src/SlideWorks.Client/src/slides/**` is a `var(--sw-…)` token: no hex, `rgb()`/`hsl()`, named-colour or font-family literal, not even in a comment. `src/look/slidesUseTokens.test.ts` is the guard rail; it fails on hex, `rgb()`/`hsl()` and font literals and on a palette key missing from `tokens.css`, and named colours are banned by this rule too. Never store a colour or font on a slide, and never hardcode a template's or palette's values in C# or TS: read `ITemplateCatalogue`/`IPaletteCatalogue` or the `LookView`. Tokens are named `--sw-<palette key>`, the palette JSON key without the prefix.
- **`GET /api/looks` is a static catalogue** (SW-2 D20), the same for every user and deck. Fetching it next to a screen's own request doesn't break "one request per screen", and it can be cached.
- **Look fixtures.** A component test that needs a look builds it with `lookViewOf(template, palette, overrides?)` and, for a template that locks its palette, `lockingTemplate`, both from `src/SlideWorks.Client/src/test-support/fixtures/lookViewOf.ts`. A WebApi test that needs a locking template uses `WebApi.Tests/Support/LockingTemplateCatalogue`. Don't write your own.
- **Rendering.** Agents render slide previews themselves from `preview_deck`'s URLs and instructions; the server renders only the 1200×630 share card, through `ICardRenderer` → `IRenderEngine` (`PlaywrightRenderEngine`, Chromium in the image). The render routes (`/api/render/{id}`, `/api/render/{id}/card`, `POST /api/render/{id}/session`) read only the deck a render token names, checked by `IRenderTokens.Validate(token, deckId, purpose)`. WebApi tests swap the engine for `WebApi.Tests/Support/FakeRenderEngine`, which returns fixed PNGs, records each call and throws on demand; real PNGs are made only in acceptance tests.
- **A render token never goes in a query string or a log.** Page URLs carry it in the fragment (`#token=…`), API requests send it as the `X-Render-Token` header, and `<img>` requests use the `sw_render` cookie that the session route sets on `/api/images`. Error texts and log lines name the deck, never the token.
- **Activity summaries are lowercase and actor-first**, like SW-1's: the screen puts the actor in front, so the summary reads "created the deck from the talk template with the Cobalt palette" or "changed the template to workshop and the palette to Ember". Write them through `IActivityLog`, never by hand.
