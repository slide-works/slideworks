# SlideWorks app

The SlideWorks phone app (Expo, expo-router, TypeScript), package `slide-works-mobile` in the pnpm workspace at
`src/`. It runs as a development build only, never in Expo Go.

## Commands

From the repo root, after `pnpm install --frozen-lockfile` in `src/`:

| What                                                       | Command                                                    |
| ---------------------------------------------------------- | ---------------------------------------------------------- |
| Everything CI checks (Prettier, ESLint, tsc, Vitest, Jest) | `pnpm --dir src/SlideWorks.Mobile run check`               |
| Pure-module tests (`*.test.ts`, Vitest)                    | `pnpm --dir src/SlideWorks.Mobile exec vitest run <files>` |
| Screen tests (`*.spec.tsx`, Jest with jest-expo)           | `pnpm --dir src/SlideWorks.Mobile exec jest <files>`       |
| Bundle for Android without a device                        | `pnpm --dir src/SlideWorks.Mobile run export:android`      |
| Regenerate the theme after `tokens.css` changes            | `pnpm --dir src/SlideWorks.Mobile run tokens`              |

## Expo project

`app.json` has no `owner` and no `extra.eas.projectId` yet: the maintainer's `eas init` adds both.

The React Native Perf Monitor overlay is unavailable in dev builds, because `app.json` blocks `SYSTEM_ALERT_WINDOW`.

## Tests: two runners and the harness

- **Vitest** runs pure modules (`src/**/*.test.ts`) in Node, with no React Native: the session and guest stores, links,
  the native-intent routing, the builders.
- **Jest** (`jest-expo`, React Native Testing Library) runs screens (`src/**/*.spec.tsx`), with a 15 s timeout.
  `render`, `renderHook`, `renderScreen` and `renderApp` are async: await them.
- Only test files and `jest.setup.ts` import `src/test-support/` (ESLint enforces it). Specs assert test ids from each
  screen's `testIds.ts`, never copy.

`src/test-support/`:

| Helper                                                     | What                                                                                                                                                                                     |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `fakeApi(table)`                                           | Replaces `fetch` so the real core client runs. Keys `"METHOD /path"`; a value is a 200 body, `{ status, body?, headers? }`, or a function of the request. Unlisted → 404. `callsTo(key)` |
| `signedInApi(overrides?)`                                  | The table a signed-in app reads (`GET /api/me`, `GET /api/decks`).                                                                                                                       |
| `aMe()`, `aDeckRow()`, `aDeviceRow()`, `aJoinSummary()`, … | Builders from `src/contracts/screens/*.json`, typed with the core's types.                                                                                                               |
| `renderScreen(ui, { scheme })`                             | One screen on a 390×844 notched phone, light or dark, with its own query cache (`retry: false`).                                                                                         |
| `renderApp({ url, api, signedIn, guest })`                 | The real `app/` routes after seeding the stored session and guest id.                                                                                                                    |
| `fakeHub()`                                                | The next `createHubConnection`: records its `HubAuth` and invokes; `respond`, `emit`, `drop`, `restore`.                                                                                 |
| `mockStorage`, `webBrowser` (`nativeMocks.ts`)             | The in-memory secure and kv stores; `webBrowser.completeAuthSession(url)` / `cancelAuthSession()` settle the sign-in handoff.                                                            |

`jest.setup.ts` fakes every native module the app uses (secure-store, kv-store, web-browser, NetInfo, colour scheme,
safe area, constants, application, device, Sentry) and resets storage before each test. A native module added later
gets its fake there.
