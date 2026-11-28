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
