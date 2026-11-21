# Agent tools

These scripts are the one sanctioned way for coding agents (and people) to build and test SlideWorks on a shared host. Each one carries a lesson that agents otherwise re-learned in every run: put dotnet on the PATH, take the shared lock for one command at a time, never let a reused MSBuild node hold that lock, never run a test on a failed build, name the tests to run instead of running a whole project, keep the full output in a log file, and wait for a backgrounded run without polling processes. A brief can then say "run `tools/agent/test-dotnet <project> <filter>`" instead of a paragraph of rules.

| Script | What it does | Example |
|---|---|---|
| `lib.sh` | Shared functions every script sources (`agent_root`, `agent_env`, `agent_task`, `agent_scratch`, `agent_log`, `agent_locked`, `agent_build`, `agent_result`, `agent_mark_done`). Not run on its own. | `source tools/agent/lib.sh` |
| `build` | Builds the solution, or one project, under the lock with no MSBuild node reuse, then runs the host's post-build step. | `tools/agent/build src/SlideWorks.AcceptanceTests` |
| `test-dotnet` | Builds the project, refuses a filter that selects no test, more than `AGENT_MAX_TESTS` (300) or the whole project, then runs the filtered tests under the lock and a timeout. Prints passed, failed and skipped counts and the failed test names. | `tools/agent/test-dotnet src/SlideWorks.UnitTest "FullyQualifiedName~DisplayDateTests"` |
| `wait` | Waits up to 170 s for a run the harness moved to the background, then prints its summary and exits with its code; prints `still-running` (exit 0) to be called again. | `tools/agent/wait /tmp/slideworks-agent/SW-700/test-dotnet-20261121-101500.log` |
| `agent.env.example` | A sample host env file with every key commented out. Not run. | `cp tools/agent/agent.env.example ~/.config/slideworks/agent.env` |
| `bootstrap` | (lands in wave 1) Gets a fresh worktree to a built state: pnpm install and a first build; `--client-bundle` also builds the client bundle the acceptance tests serve. | `tools/agent/bootstrap --client-bundle` |
| `preflight` | (lands in wave 1) Checks the host before a wave: dotnet, pnpm, the lock, free memory and the cross-family reviewer with a real call. | `tools/agent/preflight` |
| `test-ct` | (lands in wave 1) Runs named Playwright component spec files, screenshots excluded, after a type check. | `tools/agent/test-ct src/components/Deck/DeckList.spec.tsx` |
| `test-vitest` | (lands in wave 1) Runs named Vitest files. | `tools/agent/test-vitest src/lib/fit.test.ts` |
| `test-acceptance` | (lands in wave 1) Runs one acceptance test or one class. | `tools/agent/test-acceptance "FullyQualifiedName~.JoinJourney.Guest_WhenJoining_ShouldSeeSlide"` |
| `client-check` | (lands in wave 1) Runs prettier, eslint and tsc on the touched client files. | `tools/agent/client-check` |
| `bite` | (lands in wave 1) Proves a test fails under a compile-safe mutation, then restores the file. | `tools/agent/bite src/SlideWorks.Core/Polls/Tally.cs 'count + 1' 'count - 1' -- tools/agent/test-dotnet src/SlideWorks.UnitTest "FullyQualifiedName~TallyTests"` |
| `sync-push` | (lands in wave 1) Rebases on the shared branch, rebuilds, reruns the task's tests, then pushes to the shared branch. | `tools/agent/sync-push feature/sw-663-retro-actions` |
| `ci-status` | (lands in wave 1) Lists the CI jobs and their states for a commit. | `tools/agent/ci-status HEAD` |
| `self-check` | (lands in wave 1) Checks the config diff, stray files and commit subjects since the task's base. | `tools/agent/self-check origin/feature/sw-663-retro-actions` |
| `ct-preview` | (lands in wave 1) Renders screenshot specs into a scratch folder, never into `src`. | `tools/agent/ct-preview src/components/Slide/Slide.spec.tsx` |
| `ct-baselines` | (lands in wave 1) Fetches screenshot baselines from a CI run's artifact. | `tools/agent/ct-baselines <run id>` |

Each script's `--help` gives its full usage. A new script starts like `build`: `#!/usr/bin/env bash`, `set -euo pipefail`, then `# shellcheck source-path=SCRIPTDIR source=lib.sh` above `source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"`, so CI's shellcheck job (every file here without an extension, plus `*.sh`) can follow it. `agent_start` prints the log path and makes sure the run ends with a summary line and a `.done` marker; `agent_finish <code> [k=v ...]` and `agent_die <code> <message> [k=v ...]` end it.

## Output and exit codes

Detail lines may come first; the last line a script prints is always one summary line:

```
RESULT <script> <ok|fail|blocked> [<key>=<value> ...] log=<path>
```

The full tool output (compiler, test runner, pnpm) goes to that log, never to the terminal. The first line a running script prints is `log: <path>`, so a run the harness moves to the background can be waited for with `tools/agent/wait <path>`.

| Exit | Meaning |
|---|---|
| 0 | ok |
| 1 | tests or checks failed |
| 2 | bad usage (a missing filter, a directory where files are required, `--repeat-each`) |
| 3 | the build failed |
| 4 | the environment is missing something (the message names it) |
| 5 | the lock wait timed out |
| 124 | the command timed out |

## Where logs go

Logs go to `${AGENT_SCRATCH_ROOT:-${TMPDIR:-/tmp}/slideworks-agent}/<task>/<script>-<yyyymmdd-HHMMSS>.log`. The task is `$AGENT_TASK`, or the branch name without `wip/`. When a run ends, `<log>.done` holds its exit code; it is written after the log is complete.

## Host settings

Host facts (where dotnet lives, a post-build fix, Playwright's library path, the reviewer check, the lock) live in an env file outside the repo: `${SLIDEWORKS_AGENT_ENV:-$HOME/.config/slideworks/agent.env}`. See `agent.env.example`. Other knobs, set in the environment: `AGENT_TASK`, `AGENT_SCRATCH_ROOT`, `AGENT_MAX_TESTS` (300), `AGENT_TEST_TIMEOUT` (600 s), `AGENT_BUILD_TIMEOUT` (900 s), `AGENT_WAIT_MAX` (170 s).
