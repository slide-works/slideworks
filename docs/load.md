# Load

`src/SlideWorks.LoadTest` is a console tool that plays an audience against a poll: it joins voters as guests, has each
answer once, and reports how long the answer's write took and how long the results took to come back. It is run by hand
against a running app. There is no CI workflow for it, and it is not a test project, so `dotnet test` builds it and runs
nothing.

## What it does

Each voter is a guest with a `sw_guest` cookie of its own. It:

1. connects to `/hubs/deck` over WebSockets and calls `JoinAudience(code)`, with at most 50 joins in flight (the app's join
   limiter allows 100 lookups in flight from one address),
2. reads the guest poll view (`GET /api/join/{code}/poll`), which the app gives only to a guest on the session's roster, so
   it comes after the join,
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
  a high p95 rather than a missing one. When no voter sees one, results p95 is `n/a`.
- **Errors** are voters that failed to join or whose answer was not a 200 (a 409 stale, a 429, a timeout), and voters
  whose hub connection dropped. A voter counts as at most one error, so the error rate is errors divided by voters, and
  each kind is listed.
- **Dropped connections** are voters whose WebSocket failed to start, or closed (other than by the harness at the end)
  before their tally arrived. Each is one error, shown again as its own count in the table and the JSON line. A voter that
  dropped is not also counted as a results timeout and adds no results sample, so look at the dropped count before
  trusting results p95.

The run stops with an error, before joining anyone, when the poll is not open, or when its results setting is not live: a
guest gets no counts under another setting, so the starting count would read 0 and the results would look faster than they
are. Pass `--no-results` to run anyway with results timing off. Anyone else answering the poll during a run also makes the
results read low, so run it on a poll nobody else is using.

## Running it

### With the seeded load host (what the recorded runs use)

The acceptance project holds an explicit test, `LoadHostTests`, that is not a test: it starts the app (the production
`Program.cs` and DI on a real Kestrel port, in the Production environment, with its own database and data folder in a fresh
temporary folder) and seeds a live deck: slide 1 a quote, slide 2 a poll with four options and its default settings
(results live, opens on arrival), made through `POST /api/decks` and MCP `add_slide`, then Go live. Its only addition to the
production app is the test sign-in scheme, so the seed and the harness can act as the owner without Google. It writes
`URL`, `CODE`, `DECK`, `SLIDE` and `OWNER_COOKIE` to `$LOADHOST_DIR/seed.env`, serves until `$LOADHOST_DIR/stop` exists
(or `LOADHOST_MINUTES`, default 60, pass), and deletes its database when it stops. `LOADHOST_PORT` defaults to 5510. No
suite runs it; it is selected by name.

```bash
export LOADHOST_DIR=/tmp/load-host
dotnet build src/SlideWorks.AcceptanceTests -c Release && dotnet build src/SlideWorks.LoadTest -c Release
# Server GC, as the web SDK gives the app itself; the test host would otherwise run workstation GC.
DOTNET_gcServer=1 dotnet test src/SlideWorks.AcceptanceTests -c Release --no-build --filter "FullyQualifiedName~LoadHostTests" &
# once $LOADHOST_DIR/seed.env exists:
. $LOADHOST_DIR/seed.env
flock -w 900 /tmp/slideworks-test.lock dotnet src/SlideWorks.LoadTest/bin/Release/net10.0/SlideWorks.LoadTest.dll \
  --url $URL --code $CODE --voters 1000 --seconds 30 --owner-cookie "$OWNER_COOKIE" --deck $DECK --slide $SLIDE
touch $LOADHOST_DIR/stop   # then check the port is free: ss -ltnp | grep 5510
```

The first run with `--owner-cookie` moves the presentation to the poll slide, and the arrival rule opens round 1. Later
runs reuse the open round; each run's results are counted from the round's answered count when it starts.

### By hand

1. Start the app, such as `dotnet run --project src/SlideWorks.WebApi` (it listens on `http://localhost:5510`).
2. In a browser, sign in, open a deck with a poll slide whose results setting is **live**, go live, and move to the poll
   slide so its round is open. Read the six-digit join code from the live chip.
3. Run:

   ```bash
   dotnet run -c Release --project src/SlideWorks.LoadTest -- --url http://localhost:5510 --code 123456 --voters 1000 --seconds 30
   ```

| Option | Default | Meaning |
|---|---|---|
| `--code` | required | The deck's join code. |
| `--url` | `http://localhost:5510` | The app's origin. |
| `--voters` | 100 | Guests to join and answer. |
| `--seconds` | 10 | The window the answers are spread over. |
| `--pick` | `random` | `random` picks one option per voter; or 0-based positions such as `1` or `0,2`. |
| `--results-timeout` | 10 | Seconds a voter waits for the tally that includes its answer. |
| `--no-results` | off | Tallies are not expected (a poll whose results are not live): the results check is skipped. |
| `--owner-cookie` | none | A `Cookie` header value for someone who can present the deck. With `--deck` and `--slide`, the harness joins the presentation as the presenter window does, moves it to the poll slide (which opens the round on arrival), and probes during the run (below). |
| `--deck`, `--slide` | none | The deck's id and the poll's 1-based slide number, with `--owner-cookie`. |
| `--close-at` | none | Seconds into the answer window at which the presenter closes the poll; prints how long the close took and how many answers were acknowledged before and after it. |

It prints a table and then one JSON line (`writeP50Ms`, `writeP95Ms`, `writeP99Ms`, `resultsP95Ms`, `errors`,
`errorRate`, `dropped`, the counts, `gatePassed` and `gateFailures`), and exits 0 when the gate passed, 3 when it
failed, 1 when the poll was not open (or not live) or the app was unreachable, and 2 for a bad option.

Each voter holds a WebSocket and a cookie, so a run of 10,000 voters needs a raised open-file limit on the machine that runs
it (`ulimit -n 20000`) and on the app's.

## Gate

1,000 voters must pass: write p95 under 1 s, results p95 under 2 s, errors under 1%. The tool checks this itself after the
table and prints `GATE PASS` or `GATE FAIL` with each reason, and exits non-zero on a fail. It prints the verdict for every
run, but only the 1,000-voter run is a gate; results for 3,000 and 10,000 voters are measured and recorded.

A results p95 of `n/a` (no voter saw its tally) is a fail, because against the real tally publisher it means results
never arrived. It passes only with `--no-results`, for a run where tallies are not expected. Also run the poll's own measure (people who answered divided by guests joined when the poll closed) from
`docs/measures.md`.

The results time can read a little low when the harness handles the answers of an earlier commit group after those of a
later one, which can happen when thousands of voters share a few threads. The error is bounded by one pump interval
(500 ms), so treat a result near 2 s with that in mind.

## Results

Every run below measured the app at `main` 914f36b (the merge of PR #14; no app change since), with the harness and load host
at e6d4969, from Release builds, the app in the load host with server GC (`DOTNET_gcServer=1`), on one machine: 16 cores,
Ubuntu 24.04 under WSL2, harness and app on the same host over loopback. Four other agents were building and testing on the
host at the time; each run held the test lock (`flock -w 900 /tmp/slideworks-test.lock`) so no other test ran during it,
but builds did. The load average (1 min) is given at the start and end of each run. Every run used `--owner-cookie`, so the
presenter probes ran during it (a presenter move every 2 s on the poll slide, plus a fresh player join and a fresh guest
join); the probes are their own connections and add nothing to the voters' numbers. All runs answered one open round on one
deck, so each run's results count from the answers already in it.

```bash
dotnet src/SlideWorks.LoadTest/bin/Release/net10.0/SlideWorks.LoadTest.dll --url http://127.0.0.1:5510 --code <code> \
  --voters 1000 --seconds 30 --owner-cookie "sw-test-user=<owner>" --deck <deck> --slide 2
```

| Date (UTC) | Build | Voters | Window (s) | Joined in (s) | Write p50 (ms) | Write p95 (ms) | Write p99 (ms) | Results p95 (ms) | Errors | Load avg start / end | Gate |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 2026-11-17 10:52 | 914f36b | 1,000 | 30 | 7.8 | 5.2 | 8.6 | 16.8 | 480 | 0 of 1,000 (0 dropped) | 2.50 / 2.78 | pass |
| 2026-11-17 10:58 | 914f36b | 1,000 | 30 | 7.5 | 5.3 | 8.9 | 88.0 | 483 | 0 of 1,000 (0 dropped) | 3.60 / 1.85 | pass |
| 2026-11-17 11:01 | 914f36b | 1,000 | 30 | 5.2 | 5.5 | 31.6 | 64.1 | 484 | 0 of 1,000 (0 dropped) | 2.90 / 4.72 | pass |

The 1,000-voter gate passed three runs in a row with no change to the app. Every voter received its tally (1,000 received,
0 timed out, each run). Results p95 sits just under the pump's 500 ms publish interval, which is what bounds it: an answer
waits for the next flush of its deck's tally.
