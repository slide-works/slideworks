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
  a high p95 rather than a missing one. When no voter sees one, or more voters time out than see
  one, results p95 is `n/a` (the received and timed-out counts are still shown), since the figure would mostly be the
  timeout itself.
- **Errors** are voters that failed to join or whose answer was not a 200 (a 409 stale, a 429, a timeout), and voters
  whose hub connection dropped, and voters whose tally did not arrive within `--results-timeout`. A voter counts as at most one error, so the error rate is errors divided by voters, and
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
suite runs it: it is `[Explicit]` and in the `LoadHost` category, so it runs only when selected by name, as below
(`--filter "FullyQualifiedName~LoadHostTests"`); a run that wants to be sure it is left out can also pass
`--filter "TestCategory!=LoadHost"`.

```bash
export LOADHOST_DIR=/tmp/load-host
dotnet build src/SlideWorks.AcceptanceTests -c Release && dotnet build src/SlideWorks.LoadTest -c Release
# Server GC, as the web SDK gives the app itself; the test host would otherwise run workstation GC.
DOTNET_gcServer=1 dotnet test src/SlideWorks.AcceptanceTests -c Release --no-build --filter "FullyQualifiedName~LoadHostTests" &
# once $LOADHOST_DIR/seed.env exists:
. $LOADHOST_DIR/seed.env
flock -w 900 /tmp/slideworks-test.lock dotnet src/SlideWorks.LoadTest/bin/Release/net10.0/SlideWorks.LoadTest.dll \
  --url $URL --hub-urls $HUB_URLS --code $CODE --voters 1000 --seconds 30 --owner-cookie "$OWNER_COOKIE" --deck $DECK --slide $SLIDE
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
| `--hub-urls` | `--url` | Comma-separated origins of the same app; the voters' hub connections are spread over them, round robin. On loopback the app still sees every connection from 127.0.0.1. The load host prints them as `HUB_URLS` (127.0.0.1 to 127.0.0.8), because one client gets only its ephemeral port range per destination address and port (4,096 on this WSL host). |
| `--close-at` | none | Seconds into the answer window at which the presenter closes the poll; prints how long the close took and how many answers were acknowledged before and after it. |

It prints a table and then one JSON line (`writeP50Ms`, `writeP95Ms`, `writeP99Ms`, `resultsP95Ms`, `errors`,
`errorRate`, `dropped`, the counts, `gatePassed` and `gateFailures`), and exits 0 when the gate passed, 3 when it
failed, 1 when the poll was not open (or not live) or the app was unreachable, and 2 for a bad option.

Each voter holds a WebSocket and a cookie, so a run of 10,000 voters needs a raised open-file limit on the machine that runs
it (`ulimit -n 20000`) and on the app's.

## Gate

1,000 voters must pass: write p95 under 1 s, results p95 under 2 s, errors under 1%. The tool checks this itself after the
table and prints `GATE PASS` or `GATE FAIL` with each reason, and exits non-zero on a fail. It prints the verdict for every
run, but only the 1,000-voter run is a gate (D8); results for 3,000 and 10,000 voters are measured and recorded, not gated.

A voter whose tally does not arrive within `--results-timeout` counts as one error (`results: timed out`) as well as a
results timeout, so the error-rate gate fails when more than 1% of voters never see a result, even if results p95 still
looks fine. A voter is still one error at most, and a timed-out voter is never also counted as dropped. The timeout count
stays in the table and the JSON (`resultsTimedOut`). This rule came after the recorded runs; every recorded run had 0
timeouts, so none of their numbers change. Forced check (2026-11-17, 50 voters, `--results-timeout 0.001`): 47 timed out,
`errors 47 (94.00 %), of which 0 dropped connections`, `47 results: timed out`, `GATE FAIL`, exit 3. That check ran before the `n/a` rule above; it printed `results ms p95 1` beside the 47 timeouts,
which is why the rule was added. Today the same run prints `n/a` and also fails on results.

A results p95 of `n/a` (no voter saw its tally, or more timed out than saw it) is a fail, because against the real tally publisher it means results
never arrived. It passes only with `--no-results`, for a run where tallies are not expected. Also run the poll's own measure (people who answered divided by guests joined when the poll closed) from
`docs/measures.md`.

The results time can read a little low when the harness handles the answers of an earlier commit group after those of a
later one, which can happen when thousands of voters share a few threads. The error is bounded by one pump interval
(500 ms), so treat a result near 2 s with that in mind.

## Results

Every run below measured the app at `main` 914f36b (the merge of PR #14; no app change since), with the harness and load host
at 1552a8c (the 3,000 and 10,000 reruns and the close runs at d2060b6, which adds `--hub-urls`; the same harness and host under the shas they had before a rebase, e6d4969 and f4cf1fb), from Release builds, the app in the load host with server GC (`DOTNET_gcServer=1`), on one machine: 16 cores,
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
| 2026-11-17 11:04 | 914f36b | 3,000 | 30 | 20.3 | 4.8 | 13.2 | 87.2 | 486 | 977 of 3,000 (977 dropped): the harness's machine, see below | 2.09 / 3.65 | measured, not a gate (harness failed) |
| 2026-11-17 11:06 | 914f36b | 10,000 | 30 | 10.0 | 5.2 | 9.0 | 18.0 | 484 | 7,974 of 10,000 (7,974 dropped): the harness's machine, see below | 2.26 / 1.26 | measured, not a gate (harness failed) |
| 2026-11-17 11:13 | 914f36b | 3,000 | 30 | 19.8 | 4.6 | 14.2 | 32.5 | 486 | 0 of 3,000 (0 dropped) | 1.05 / 1.02 | measured, not a gate |
| 2026-11-17 11:17 | 914f36b | 10,000 | 30 | 70.1 | 6.0 | 125.3 | 185.5 | 506 | 0 of 10,000 (0 dropped) | 6.27 / 6.08 | measured, not a gate |

The 1,000-voter gate passed three runs in a row with no change to the app. Every voter received its tally (1,000 received,
0 timed out, each run). Only the three 1,000-voter rows are the gate (D8); the 3,000 and 10,000 rows are measurements.

**How to read results p95.** The figures of 480 to 506 ms in the results column are the publisher's tick, not headroom.
The pump publishes a deck's tally at most twice a second, so an answer waits for the next tick: on average half a tick,
and a wait spread evenly over a 500 ms tick has a p95 of about 475 ms. Against that expected 475 ms, the rows up to
3,000 voters (480 to 486 ms) are 5 to 11 ms over, and the 10,000-voter rerun (506 ms) is about 31 ms over. Against the
500 ms tick itself, only the 10,000-voter rerun is over, by about 6 ms. Either way the excess, not the figure, is the
signal about the app, and it is small. Its error bar: the harness counts a voter's result as received at the first tally whose `answered` reaches
the count that includes its answer, which can read low by up to one publish interval (500 ms) when the harness handles
200s out of commit order. So a recorded results p95 is a measurement to within -500 ms / +0 ms of the true one.

### What these numbers do not show

- One host: the harness and the app shared the same 16 cores (WSL2), and four other agents were building on it at the
  time (load averages beside each row). A separate client machine would leave the app more CPU.
- Loopback only: no network latency, loss or jitter, no TLS, no proxy in front.
- No phones: the harness parses each event and does no rendering; a real phone's time to show a result is longer.
- One poll, live results, one deck, one open round per run; no other deck live at the same time.
- Answers spread evenly over a 30 s window, not the burst of the first seconds after a question appears. The close runs
  below are the only bursts measured (1,000 answers in 1 and 2 s).
- One harness address: the app saw every connection from 127.0.0.1 (see answer (e)).

3,000 and 10,000 are measured, not gated. The first runs of each failed on the harness's side, not the app's: every error
was `join: Cannot assign requested address (127.0.0.1:5510)`. This WSL host's ephemeral port range is 44620 to 48715
(4,096 ports), which caps the connections one client can hold to one address and port, and each voter holds a WebSocket
and opens a negotiate request; the voters that did join (2,023 and 2,026) all answered and got their tallies. The load
host now listens on 127.0.0.1 to 127.0.0.8 and the harness spreads the voters' hub connections over them (`--hub-urls`,
round robin; the poll's HTTP calls stay on `--url`). The reruns, on a fresh load host and a fresh round, joined everyone
with no error. The first thing that grows at 10,000 is the join itself: 10,000 joins took 70 s at 50 in flight (about 140
a second, against about 150 a second at 3,000), and write p95 rose from 14 ms to 125 ms. Results p95 at 10,000 was 506 ms,
past the 500 ms publish interval for the first time. Nothing failed at 10,000 on this host.

### What a presenter and a newcomer feel during a run

The presenter probes from the same runs, in milliseconds, as median / maximum of N samples. N is 3 or 4 before any
voter and with every voter connected, 14 or 15 in the answer window, 2 to 10 while joining (36 at 10,000). With so few
samples only the median and the maximum mean anything. "Step" is a presenter move on the poll slide (`UpdatePresentation`,
the deep-dive switch flipped), whose hub call runs SW-4's registry and its observers inline, the arrival observer's one
indexed read and SW-5's feed recompose among them. "Player" is a fresh owner connection's start plus `JoinPresentation`
as the audience window, the player's first join. "Guest" is a fresh guest's start, `JoinAudience` and poll view.

| Run | Before any voter: step / player / guest | Joining: step / player / guest | All connected: step / player / guest | Answer window: step / player / guest |
|---|---|---|---|---|
| 1,000 #1 | 4/4, 9/11, 12/13 | 70/171, 89/242, 87/363 | 15/18, 42/49, 69/73 | 10/19, 20/315, 19/318 |
| 1,000 #2 | 4/4, 11/53, 14/44 | 62/77, 37/53, 23/30 | 11/12, 24/28, 17/20 | 9/14, 15/175, 21/172 |
| 1,000 #3 | 4/12, 16/41, 37/57 | 49/72, 280/794, 24/152 | 17/23, 19/79, 55/59 | 9/204, 23/228, 26/252 |
| 3,000 | 5/10, 16/56, 24/144 | 81/116, 359/1014, 140/567 | 16/20, 50/52, 38/42 | 12/33, 187/496, 190/477 |
| 10,000 | 4/5, 14/14, 15/15 | 119/565, 383/1328, 332/1320 | 72/75, 120/129, 125/269 | 17/38, 443/875, 448/1361 |

### Questions from wave 1, answered by these runs

- **(a) Does a burst of answers starve a close?** Yes. Measured 2026-11-17 11:25 UTC on the same build, with
  `--close-at`, under the test lock:
  - 1,000 voters over 2 s, Close sent at 1 s: the close took 1,138 ms; 477 answers had been acknowledged when it was
    sent, and all 1,000 were counted in the round's final.
  - 1,000 voters over 1 s, Close sent at 0.5 s: 618 ms; 432 acknowledged when sent, all 1,000 counted.
  - The same close with no answers arriving: 8 ms.
  In both burst runs the close landed about 120 to 140 ms after the end of the window, that is after the last answer was
  sent. What follows from the code: the answer writer commits its next group as soon as the previous commit ends, and
  `Polls.CloseAsync` opens its own SQLite write transaction, so the close gets in only when the writer's queue is
  momentarily empty. What was not measured is a stream long enough to hold it off for longer. The bound in practice is
  SQLite's busy timeout: the app sets none (`Program.cs` opens the database with `Data Source=<path>` only, and no command
  timeout is configured), so Microsoft.Data.Sqlite's default of 30 seconds applies, after which the close would fail.

  **Known limit.** While answers keep arriving, a presenter's Close waits for a gap in them. Until the close commits, the
  poll stays visibly open on the big screen, the panel and the phones, and every answer that arrives before then is
  counted. Nothing is lost or counted twice (V5 holds). The fix is in `AnswerWriter.cs` and `Polls.cs`, which SW-9 is
  rewriting; it is raised on SW-9 (SW-351) and not made here. At the bound, as a reading of the code and not something a
  run showed: nothing in the app sets a busy timeout (`Program.cs:43`, `UseSqlite($"Data Source={dbPath}")`), so the
  provider's 30 s default applies; past it, beginning the close's transaction fails with a "database is locked" error,
  nothing handles it, and the presenter's Close call would most likely return a server error with the poll still open.
- **(b) What does a presenter move cost with 1,000 guests connected?** The step probe (a move on the poll slide, hub call
  to return, with the arrival observer's indexed read inline), median / maximum: 4 / 4 to 12 ms with no guests, 11 to 17
  / 12 to 23 ms with 1,000 connected and idle, 9 to 10 / 14 to 204 ms during the answers (14 or 15 samples). Median 16 ms
  at 3,000 and 72 ms at 10,000 connected (3 samples each). The runs do not separate the observer's read from the feed's
  recompose and its broadcast.
- **(c) Join and follow latency for guests during the answers?** A fresh guest's join (start, `JoinAudience`, poll view)
  during the 1,000-voter window, median / maximum of 15: 19 to 26 / 172 to 318 ms. 190 / 477 ms at 3,000 and 448 /
  1,361 ms at 10,000 (14 or 15 samples). Following, measured as each voter's own tally: results p95 480 to 486 ms up to
  3,000 and 506 ms at 10,000, read as the publisher's tick plus a few milliseconds (above). A presenter move's own
  audienceState was not timed at the guest (SW-353).
- **(d) Does SW-6 add measurably to the player's first join?** The player probe (start plus `JoinPresentation` as the
  audience window), median / maximum: 9 to 16 / 11 to 53 ms with no guests; 15 to 23 / 175 to 315 ms during the
  1,000-voter window; one 794 ms maximum while 1,000 were joining. It passed 1 s only while thousands were joining
  (maximum 1,014 ms of 9 samples at 3,000, 1,328 ms of 36 at 10,000). There was no deck without a poll to compare with,
  so SW-6's share is not isolated (SW-352); at 1,000 voters the first join stayed well under SW-230's one-second freeze
  on this host.
- **(e) Can the harness trip the join limiter?** No, and these runs could not have shown otherwise. The join limiter is
  keyed on the connection's remote address and is checked only by the hub's `JoinAudience`; the poll view and answer
  calls do not use it for a known code. Per address it counts two things: misses (a code that finds no session, at most
  10 a minute) and lookups in flight (at most 100). A successful lookup is never counted against the caller. Every voter
  used the valid code and the harness keeps at most 50 joins in flight, so a run of valid joins cannot trip it from one
  address or many. Joins per address: the app saw every connection from 127.0.0.1, in every run. The 3,000 and 10,000
  reruns spread the voters' hub connections round robin over the destinations 127.0.0.1 to 127.0.0.8 (375 and 1,250 to
  each), but on this host the kernel picks 127.0.0.1 as the source for every loopback destination (the local route
  `127.0.0.0/8 ... src 127.0.0.1`; checked by connecting to 127.0.0.5 and reading the peer address), so the limiter saw
  3,000 and 10,000 joins from one address. No run had a `too_many_attempts`. The runs therefore say nothing about a room of
  real phones behind one NAT that mistype codes: those misses do count, 10 a minute for the whole room.

The results p95 against the real tally publisher (SW-287) is the figure in every row above: the "answered includes my
answer" rule saw every voter's tally in every run, with no timeout.

## Q&A and reactions

`--scenario qa` plays an audience against the deck's Q&A round (SW-9, Q10) instead of a poll. `--scenario poll` is the
default and is the run described above, unchanged. The qa scenario needs a live deck with questions on; it does not need
`--owner-cookie`, `--deck`, `--slide` or `--close-at`, and refuses them (exit 2), as it refuses `--pick`. `--guests` is
`--voters` under its other name.

Each guest is a phone, as a voter is (cookie, hub connection, `JoinAudience`, joins at most 50 in flight). It then:

1. reads the questions (`GET /api/join/{code}/questions`) and picks up to three open questions that are not its own,
2. waits a random time inside `--seconds`, counted from when every guest has joined,
3. upvotes the three, one `PUT /api/join/{code}/questions/votes` each, carrying every pick so far and the revision the last
   200 returned,
4. taps ten reactions (`POST /api/join/{code}/reactions`), spread evenly over what is left of the window (the app refuses an
   eleventh tap within 5 s), and
5. waits, up to `--results-timeout` seconds from each upvote's 200, for a `questionTally` on its own connection whose `votes`
   for that question include it.

Before the run the harness asks `--questions` (default 5; 0 to use what the round already holds) questions of its own, each
from a guest that is not one of the run's, and reads the votes each open question has. A `questionTally` includes a guest's
upvote on question n once its `votes` for n reach the votes n had before the run plus the upvotes on n acknowledged up to and
including this one, the same rule as the poll's. The run stops with exit 1, before joining anyone, when there is no open Q&A
round, the deck's reactions are off, or there is no open question.

- **Write** is the time from sending a `PUT` or a tap to reading its response, for those that got a 200 or 204. It is one
  p95 over upvotes and taps together.
- **Tally** is the time from an upvote's 200 to the first matching `questionTally`. `--no-results` skips it, as it skips the
  poll's results time, for a run where tallies are not expected.
- **Errors** are as the poll's: one per guest at most, listed by kind (`vote: 429 too_many_votes`, `tap: 409 reactions_off`,
  `results: timed out`), and a guest that is refused stops acting.
- **Served** lists what each route answered and how often (`vote 200 x3000`). During SW-9's build the app served stubs or
  real services depending on what had landed; the run only has to complete, and this line says which it met.

The gate is the poll's, at 1,000 guests in 30 s: write p95 under 1 s, tally p95 under 2 s, errors under 1%. Run it by hand
against the load host (`HUB_URLS` as above):

```bash
dotnet src/SlideWorks.LoadTest/bin/Release/net10.0/SlideWorks.LoadTest.dll \
  --scenario qa --url $URL --hub-urls $HUB_URLS --code $CODE --guests 1000 --seconds 30
```

Run it on a round nobody else is using: another guest upvoting during the run makes the tally times read low.

### Results

| Date (UTC) | Build | Guests | Window (s) | Joined in (s) | Write p50 (ms) | Write p95 (ms) | Write p99 (ms) | Tally p95 (ms) | Errors | Load avg start / end | Gate |
|---|---|---|---|---|---|---|---|---|---|---|---|
