# Agent tools

These scripts are the one sanctioned way for coding agents (and people) to build and test SlideWorks on a shared host. Each one carries a lesson that agents otherwise re-learned in every run: put dotnet on the PATH, take the shared lock for one command at a time, never let a reused MSBuild node hold that lock, never run a test on a failed build, name the tests to run instead of running a whole project, keep the full output in a log file, and wait for a backgrounded run without polling processes. A brief can then say "run `tools/agent/test-dotnet <project> <filter>`" instead of a paragraph of rules.

| Script | What it does | Example |
|---|---|---|
| `lib.sh` | Shared functions every script sources (`agent_root`, `agent_env`, `agent_task`, `agent_scratch`, `agent_log`, `agent_locked`, `agent_build`, `agent_result`, `agent_mark_done`). Not run on its own. | `source tools/agent/lib.sh` |
| `build` | Builds the solution, or one project, under the lock with no MSBuild node reuse, then runs the host's post-build step. | `tools/agent/build src/SlideWorks.AcceptanceTests` |
| `test-dotnet` | Builds the project, refuses a filter that selects no test, more than `AGENT_MAX_TESTS` (300) or the whole project, then runs the filtered tests under the lock and a timeout. Prints passed, failed and skipped counts and the failed test names. | `tools/agent/test-dotnet src/SlideWorks.UnitTest "FullyQualifiedName~DisplayDateTests"` |
| `wait` | The only way to wait for a backgrounded run: one `wait` per Bash command, never a sleep, until or pgrep loop. Waits up to 170 s for a run the harness moved to the background, then prints its summary and exits with its code; prints `still-running` (exit 0) to be called again. | `tools/agent/wait /tmp/slideworks-agent/SW-700/test-dotnet-20261121-101500.log` |
| `agent.env.example` | A sample host env file with every key commented out. Not run. | `cp tools/agent/agent.env.example ~/.config/slideworks/agent.env` |
| `bootstrap` | Gets a fresh worktree to a built state: pnpm install and a first build; `--client-bundle` also builds the client bundle the acceptance tests serve. | `tools/agent/bootstrap --client-bundle` |
| `preflight` | Checks the host before a wave: dotnet, pnpm, the lock, free memory and the cross-family reviewer with a real call. | `tools/agent/preflight` |
| `test-ct` | Runs named Playwright component spec files, screenshots excluded, after a type check. A `test.fail` that fails as expected is reported as `expected_failures=N`, not as a failure. | `tools/agent/test-ct src/slides/Reveal.spec.tsx` |
| `test-vitest` | Runs named Vitest files. | `tools/agent/test-vitest src/slides/safeHref.test.ts` |
| `test-acceptance` | Runs one acceptance test or one class. | `tools/agent/test-acceptance "FullyQualifiedName~.JoinJourney.Guest_WhenJoining_ShouldSeeSlide"` |
| `client-check` | Runs prettier, eslint and tsc on the touched client files. | `tools/agent/client-check` |
| `bite` | Applies one mutation to committed files (`--file/--from/--to`, or `--patch`), runs a `test-*` runner after `--`, and restores the files before its RESULT line. Refuses only when a target file has uncommitted changes. | `tools/agent/bite --file src/SlideWorks.Core/Polls/PollSettingsRules.cs --from 'optionCount < 2' --to 'optionCount < 1' -- tools/agent/test-dotnet src/SlideWorks.UnitTest "FullyQualifiedName~PollSettingsRulesTests"` |
| `sync-push` | The only way to push: rebases on the shared branch, rebuilds when the incoming commits touch built files, runs each `--test` command, then pushes. | `tools/agent/sync-push --branch feature/sw-663-retro-actions --test 'tools/agent/test-vitest src/slides/safeHref.test.ts'` |
| `ci-status` | Finds the CI run whose head contains the commit (default `HEAD`) and lists its jobs and their states. | `tools/agent/ci-status --branch feature/sw-663-retro-actions HEAD` |
| `self-check` | Checks the config files changed, stray files and commit subjects (max 120 characters) since `--base`. With `AGENT_TASK` set it looks only at commits whose subject starts with `$AGENT_TASK `. | `AGENT_TASK=SW-700 tools/agent/self-check --base <the task's base sha>` |
| `ct-preview` | Renders screenshot specs into a scratch folder, never into `src` (see Screenshot baselines). | `tools/agent/ct-preview src/slides/SlideFrame.spec.tsx` |
| `ct-baselines` | Fetches the screenshot baselines CI made from a CI run's artifact (see Screenshot baselines). | `tools/agent/ct-baselines --run <run id> --apply` |

Each script's `--help` gives its full usage. A new script starts like `build`: `#!/usr/bin/env bash`, `set -euo pipefail`, then `# shellcheck source-path=SCRIPTDIR source=lib.sh` above `source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"`, so CI's shellcheck job (every file here without an extension, plus `*.sh`) can follow it. `agent_start` prints the log path and makes sure the run ends with a summary line and a `.done` marker; `agent_finish <code> [k=v ...]` and `agent_die <code> <message> [k=v ...]` end it.

## Mutation recipes

From `bite --help`. `bite` proves a test fails when the behaviour is broken: it applies one mutation, runs the runner, restores the files from a scratch copy (never from git) and prints `RESULT bite ok bit=yes` when the runner reported failures, `bit=no` (exit 1) when everything stayed green, and `blocked reason=mutation-does-not-compile` (exit 3) when the build failed.

- Change a literal or a condition; never delete a statement. S1144 and S1481 turn an unused member or local into a build error, so a deleted call shows up as `mutation-does-not-compile`.
- To remove a call and stay analyzer-clean, replace it with a read of its dependency: `_ = dependency;`.
- To break atomicity, break only the check-and-set and keep the structure thread-safe, so the test fails on its assertion and not on a crash.
- When a review suggests a stronger assertion, bite-test that exact assertion.
- One mutation per call, never two that reach the same test, at most three calls in a row, and never wrap `bite` in another timeout: the runner time-boxes itself and `bite` must live long enough to restore.
- Commit the green step first: `bite` refuses a target file with uncommitted changes, and the tree is dirty until its RESULT line, so do not pull or rebase while it runs.
- `bite` resolves a relative runner path before it changes directory, so it also works from `src/SlideWorks.Client`.

## Screenshot baselines

From `ct-preview --help` and `ct-baselines --help`. Local runs never write baselines (`playwright-ct.config.ts` uses `updateSnapshots: 'none'` outside CI).

- `ct-preview <spec> [-g <title regex>]` is how to look at a render locally: it writes each screenshot to a scratch folder, prints each PNG's path and checks that nothing was written into `src`.
- `ct-baselines [--run <run id>] [--apply]` takes the baselines CI made: it downloads the `ct-new-baselines` artifact of the newest CI run of the branch, lists the files and, with `--apply`, copies each to its repo path and prints the list so you can open the images before you commit. It never commits.
- Commit only baselines that came from CI. A baseline made on this host does not match the CI container's fonts.

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
