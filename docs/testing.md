# Testing races and time

Races and expiries are the bugs that reviews keep finding late. This page lists the tests to write up front and the
helpers that make them cheap. Every example is copied from a real file; open it for the whole test.

The .NET helpers live in `src/SlideWorks.WebApi.Tests/Support/Timing/` and are compiled into the acceptance project
too. `WallStepClock` needs `FakeTimeProvider`, so it is in `Support/ApiOnly/Timing/` and is for WebApi.Tests only.

## The interleaving checklist

For any session that has one request in flight (a hub join, a PUT, a re-read), write a test for each of these:

1. **A second user action while it is out.** Example: `PollAnswerLive.test.tsx`, "is sending while the PUT is out, and
   a tap meanwhile goes after it with the revision it returns".
2. **A push event while it is out.** Example: `JoinWhileMovingTests` (the presenter moves while a join is held).
3. **A read landing while it is out.** Example: `PollAnswerLive.test.tsx`, "applies only the newest read when two
   are out".

For a registry keyed by connection id, add a fourth: **a disconnect while a hub call is out**
(`GhostConnectionTests`, which holds the call with a `Gate` and waits with `DisconnectWatch` and `InvocationWatch`).

Hold the call with a deferred promise on the client, or with a `Gate` on the server. From
`src/SlideWorks.Client/src/polls/guest/PollAnswerLive.test.tsx`:

```tsx
    const out = deferred<AnswerOutcome>();
    api.putPollAnswer.mockReturnValueOnce(out.promise);
    await render(pickOne());

    await act(async () => latest().toggle(1));
    expect(latest()).toMatchObject({ state: 'sending', picks: [1] });
    await act(async () => latest().toggle(3));
    expect(puts()).toHaveLength(1);
    expect(latest().picks).toEqual([3]);
```

## One test per invariant

Before the first push of a hook or registry with async sends, write its invariants down (one call out at a time, the
latest pick wins, nothing crosses a round, server state only moves forward) and give each one its own test, named for
the invariant. A test that checks several at once tells you nothing about which one broke. From
`src/SlideWorks.Client/src/polls/guest/PollAnswerLive.test.tsx` ("applies only the newest read when two are out"):

```tsx
    const first = deferred<GuestPollView>();
    const second = deferred<GuestPollView>();
    api.getGuestPoll.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
    const activity = pickOne({ roomSeesCounts: false });
    await render(activity);
    await render({ ...activity, roomSeesCounts: true });

    await act(async () => second.resolve(view({ mine: { picks: [1], revision: 2 } }, activity)));
    await act(async () => first.resolve(view({ mine: { picks: [0], revision: 1 } }, activity)));
```

## An expiry test for every time-bounded keep or mark

When a change adds anything that lasts "until N seconds pass" (a keep, a mark, a window), its first round of tests
includes the test where the time runs out. Step the clock past the bound; never sleep. From
`src/SlideWorks.WebApi.Tests/Presenting/GhostConnectionTests.cs`
(`JoinPresentation_WhenTheJoinOutlivesKeepClosed_ShouldNoLongerBeRefused`):

```csharp
        _gate.Arm(deck);
        _ = closing.JoinAsync(deck, "k-ghost");
        await _gate.EnteredAsync();
        await closing.DisposeAsync();
        await _disconnects.DisconnectedAsync(closingId);
        Clock.Advance(Presentations.KEEP_CLOSED);
        await pruner.DisposeAsync();
        await _disconnects.DisconnectedAsync(prunerId);
        _gate.Release();
        var outcome = await _invocations.FinishedAsync(closingId, "JoinPresentation");
```

## Bound the request rate under a failing server

When a change touches retry or re-read behaviour, test the rate, not only how many calls are in flight: make the
server fail, drive events with fake timers, and count the calls in a fixed span. From
`src/SlideWorks.Client/src/qa/guest/QuestionsLive.test.tsx` ("a failing GET under a stream of higher tallies and room
pushes reads at most 4 times in 10 seconds"; the `describe` calls `vi.useFakeTimers()`):

```tsx
    api.getQuestions.mockRejectedValue(failure);

    for (let step = 1; step <= 20; step += 1) {
      await tick(500);
      await hub.push(higherTally(1 + Math.floor(step / 4)));
      if (step % 4 === 0)
        await render({ ...view.room, listRevision: view.room.listRevision + 1 + step / 4 });
    }

    // The first failure at 0.5 s, then one read per REREAD_RETRY_MS: 3.5 s, 6.5 s and 9.5 s.
    expect(api.getQuestions).toHaveBeenCalledTimes(4);
```

## Assert "no event" or "exactly N events" with a marker

`DeckEventsUntilMarker` subscribes to `IDeckEvents` for one deck. `BeforeMarkerAsync` publishes a marker
`DeckChanged`, reads until it arrives and returns the events before it. Assert on that list in the test. Never use
`TryRead` with a timeout: it passes when the event is merely slow. From
`src/SlideWorks.WebApi.Tests/Presenting/SetTalkLengthToolTests.cs`:

```csharp
        using var events = DeckEventsUntilMarker.Subscribe(Factory.Services.GetRequiredService<IDeckEvents>(), deck);
```

then, for exactly one event and for none:

```csharp
        (await events.BeforeMarkerAsync()).ShouldHaveSingleItem().ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
```

```csharp
        (await events.BeforeMarkerAsync()).ShouldBeEmpty();
```

## Open a race window with a Gate

A `Gate` holds one side of an operation at a named point until the test releases it. Wrap the real service in a
test-project decorator that calls `PassAsync`, and swap it in with `ConfigureTestServices`. Waits are bounded (AG0032),
and `TearDown` calls `Release` so nothing stays held. From `src/SlideWorks.WebApi.Tests/Presenting/GatedDeckAccess.cs`:

```csharp
    public async Task<bool> CanPresentAsync(string deckId, ICurrentActor actor)
    {
        await gate.PassAsync(deckId);
        return await inner.CanPresentAsync(deckId, actor);
    }
```

and its use in `src/SlideWorks.WebApi.Tests/Presenting/JoinWhileMovingTests.cs`:

```csharp
        _gate.Arm(deck);
        var join = audience.JoinAsync(deck, "k-audience", "audience");
        await _gate.EnteredAsync();
        var moved = await presenter.UpdateAsync(deck, 2, 0);
        _gate.Release();
        var joined = (await join).GetProperty("state");
```

Show the test failing with the window forced open before you trust it passing.

## Step the clock

Measure durations with `TimeProvider.GetTimestamp` and `GetElapsedTime`, never with wall time, and test both clocks.

- **WebApi.Tests:** the fixture's `Clock` is a `FakeTimeProvider`; `Clock.Advance` moves both clocks. To step the
  wall clock alone (NTP, a VM resuming), wrap it in `WallStepClock`. From
  `src/SlideWorks.WebApi.Tests/Presenting/PresentationWallClockTests.cs`:

  ```csharp
      protected override Action<IServiceCollection>? ConfigureTestServices => services => services
          .AddSingleton<TimeProvider>(new WallStepClock(Clock));

      private WallStepClock Wall => (WallStepClock)Factory.Services.GetRequiredService<TimeProvider>();
  ```

  ```csharp
          Clock.Advance(TimeSpan.FromSeconds(30));
          Wall.StepWall(TimeSpan.FromMinutes(5));
          var state = await owner.UpdateAsync(deck, 3, 0);
  ```

- **Acceptance journeys:** the app is shared and runs on real time, so it uses `StepClock` (the real clock plus
  offsets) as `AcceptanceApp.Clock`. A journey that steps it resets it in `TearDown`. `AcceptanceApp.Hubs` aborts
  every hub connection from the server, to drive a reconnect without a Playwright WebSocket route. From
  `src/SlideWorks.AcceptanceTests/Journeys/TimingSeamJourneyTests.cs`:

  ```csharp
      [TearDown]
      public void ResetSeams() => AcceptanceApp.ResetSeams();
  ```

  ```csharp
          AcceptanceApp.Clock.Advance(TimeSpan.FromSeconds(61));

          using var again = await RegisterAsync(http);
          again.StatusCode.ShouldBe(HttpStatusCode.Created, await again.Content.ReadAsStringAsync());
  ```

  The OAuth registration limit is per caller address, and every journey calls from 127.0.0.1. After a journey steps
  past a window and resets, the window it opened starts in the future, so the next ~2 minutes allow only 9 more
  registrations over HTTP. `AgentSeed` registers through the container and is not limited.
