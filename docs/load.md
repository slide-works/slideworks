# Load

`src/SlideWorks.LoadTest` is a console tool that plays an audience against a poll: it joins voters as guests, has each
answer once, and reports how long the answer's write took and how long the results took to come back. It is run by hand
against a running app. There is no CI workflow for it, and it is not a test project, so `dotnet test` builds it and runs
nothing.

## What it does

Each voter is a guest with a `sw_guest` cookie of its own. It:

1. reads the guest poll view (`GET /api/join/{code}/poll`),
2. connects to `/hubs/deck` over WebSockets and calls `JoinAudience(code)`, with at most 50 joins in flight (the app's join
   limiter allows 100 lookups in flight from one address),
3. waits a random time inside `--seconds`, counted from when every voter has joined,
4. sends one answer (`PUT /api/join/{code}/poll/answer`), and
5. waits, up to `--results-timeout` seconds, for a `pollTally` on its own connection whose `answered` count includes its
   answer.

All durations are measured with `Stopwatch`, never by subtracting clock readings.

- **Write** is the time from sending the `PUT` to reading its response, for answers that got a 200.
- **Results** is the time from that 200 to the first `pollTally` for the round whose `answered` is at least the round's
  answered count before the run plus the number of answers acknowledged so far, this one included. Every acknowledged
  answer was committed first, so that tally includes it. A tally that was already there when the 200 arrived counts as 0.
  A voter that does not see such a tally within `--results-timeout` counts at the full timeout, so a slow app shows up as
  a high p95 rather than a missing one. When no voter sees one, results p95 is `n/a`, as it is against the stub routes,
  where the tally never publishes.
- **Errors** are voters that failed to join or whose answer was not a 200 (a 409 stale, a 429, a dropped connection, a
  timeout). The error rate is errors divided by voters, and each kind is listed.

The run stops with an error, before joining anyone, when the poll is not open.

## Running it

1. Start the app, such as `dotnet run --project src/SlideWorks.WebApi` (it listens on `http://localhost:5510`).
2. In a browser, sign in, open a deck with a poll slide whose results setting is **live**, go live, and move to the poll
   slide so its round is open. Read the six-digit join code from the live chip.
3. Run:

   ```bash
   dotnet run -c Release --project src/SlideWorks.LoadTest -- --url http://localhost:5510 --code 123456 --voters 1000 --seconds 10
   ```

| Option | Default | Meaning |
|---|---|---|
| `--code` | required | The deck's join code. |
| `--url` | `http://localhost:5510` | The app's origin. |
| `--voters` | 100 | Guests to join and answer. |
| `--seconds` | 10 | The window the answers are spread over. |
| `--pick` | `random` | `random` picks one option per voter; or 0-based positions such as `1` or `0,2`. |
| `--results-timeout` | 10 | Seconds a voter waits for the tally that includes its answer. |

It prints a table and then one JSON line (`writeP50Ms`, `writeP95Ms`, `writeP99Ms`, `resultsP95Ms`, `errors`,
`errorRate`, and the counts), and exits 0 when the run completed, 1 when the poll was not open or the app was
unreachable, and 2 for a bad option.

Each voter holds a WebSocket and a cookie, so a run of 10,000 voters needs a raised open-file limit on the machine that runs
it (`ulimit -n 20000`) and on the app's.

## Gate

1,000 voters must pass: write p95 under 1 s, results p95 under 2 s, errors under 1%. Results for 3,000 and 10,000 voters
are measured and recorded, not gated. Run the poll's own measure (people who answered divided by guests joined when the
poll closed) from `docs/measures.md`.

## Results

| Date | Build | Voters | Window (s) | Write p50 (ms) | Write p95 (ms) | Write p99 (ms) | Results p95 (ms) | Errors |
|---|---|---|---|---|---|---|---|---|
| | | 1,000 | | | | | | |
| | | 3,000 | | | | | | |
| | | 10,000 | | | | | | |
