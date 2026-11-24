# House rules

What fails the build, what the tests expect and which traps cost earlier runs time. Every rule says what to do, not only what is banned. AGENTS.md has the layout of the code; [testing.md](testing.md) covers races and time in tests; [../tools/agent/README.md](../tools/agent/README.md) lists the scripts. Each snippet is copied from the file named under it.

## 1. Analyzers that fail the build

`TreatWarningsAsErrors` is on in `src/Directory.Build.props`, which applies to every project under `src/`, and it adds SonarAnalyzer.CSharp 10.35.0.4138, Agoda.Analyzers 1.1.203 and the banned-API analyzer. Every rule below is enabled by default at warning level or higher in those packages (checked by loading the analyzers and reading each rule's default), so each one is a build error unless `.editorconfig` turns it off. `.editorconfig` changes only these:

- `S1135` (a `TODO:` comment) is a suggestion.
- `AG0010` (a fixture inherits a base class) is off in `SlideWorks.WebApi.Tests` and `SlideWorks.AcceptanceTests`.
- `AG0012` is off in `SlideWorks.AcceptanceTests` only: the assertions there are Playwright's retrying `Expect(...)`. It is on in `SlideWorks.WebApi.Tests` and `SlideWorks.UnitTest`.

| Rule | What it catches | Do this instead |
|---|---|---|
| AG0002 | `internal` members (the rule's own title says it keeps tests off non-public surface; earlier runs hit it on `internal` members, which is not shown in the rule's text, so that part is unverified) | Make the class `internal` and the member `public`, or make the member `private`. Core types are public or private |
| AG0005 | A test name that is not `Method_When_Should` | Exactly two or three parts, for example `Startup_WhenFreshDatabase_ShouldApplyEveryMigration`. Never write a test name into a brief that breaks it |
| AG0009 | An `IHttpContextAccessor` passed as a method argument, which includes a constructor of a request-dependent singleton | Pass only the fields you need, or design the API to take the request. If a per-request service really needs the accessor, suppress with a comment that says why, as `Features/Render/RenderSessions.cs` does (`#pragma warning disable AG0009` around the constructor) |
| AG0012 | A test method with no assertion in its own body. The analyzer also crashes on an expression-bodied test method | Keep the assertions inline in each test, not in a helper method, and give every test method a block body |
| AG0018 | A public `IEnumerable` member that is mutable | Expose `IReadOnlyList<T>` or similar |
| AG0020 | A method that returns `null` for an `IEnumerable<T>` | Return an empty sequence |
| AG0022 | A sync and an async version of one method on one type | Keep one |
| AG0032 | `Task.Wait*` and `WaitAsync` (error level) | `Task.WhenAny(task, Task.Delay(limit))`, then throw `TimeoutException`; see section 2 |
| AG0043 | `BuildServiceProvider` (error level), tests included | Let the framework own the provider; resolve through the test fixture |
| AG0046 | `GetByText`, `GetByRole` and other locators in Playwright code | `GetByTestId`, or `Filter(new() { HasTextRegex = ... })`; in WebApi use `WaitForFunctionAsync` rather than a locator |
| AG0051 | A fixed date literal in a test that meets a real clock | `FakeTimeProvider`, stepped with `Advance` |
| AG0052 | A hard-coded delay in test code | Wait for the event with a bounded wait (section 2) |
| S1144, S1481 | An unused private member or local | Delete it. A mutation that removes a call can trip these: replace the call with `_ = dependency;` (see `bite`) |
| S2365 | A property that makes a collection or array copy | A method, or store the collection |
| S3218 | A nested type's member that shadows the outer type's | Rename the member |
| S3267 | A loop that LINQ would simplify | Write the LINQ expression |
| S3928 | An `ArgumentException` parameter name that matches no parameter | Use `nameof(parameter)` |
| S4144 | Two methods with identical bodies | In fakes, make each return the outcome of its action (a closed phase, the next round, a flagged entry) |
| S4581 | `new Guid()` (a generated `new Guid("0000...")` default counts) | A named constant, or a real id; see section 5 |
| S5693 | A request body limit over 8 MB | A smaller limit. A route that really needs more states the limit and suppresses, as `Features/Templates/TemplateBundleController.cs` does around `[RequestSizeLimit(...)]` |
| S6618 | `FormattableString` where `string.Create` fits | `string.Create` |
| S6934 | An action-level route template on a controller with no `[Route]` | Put a `[Route]` on the controller |
| S6964 | A value-type input of a controller action that is not nullable, `required` or `[JsonRequired]` | Make it nullable, `required` or `[JsonRequired]`. Bind a request record to a real action in the task that adds it, or give its value types `[JsonRequired]` from the start; `Features/Templates/Views/TemplateSharingUpdate.cs` uses `[property: JsonRequired]` |
| S6966 | An awaitable method whose result is dropped | `await` it |
| S7039 | A Content Security Policy with `unsafe-inline` | Restrictive policy; no inline script or style |

Also banned at build time: `DateTime.Now`, `UtcNow` and friends (`src/BannedSymbols.txt`; use `TimeProvider`).

**Console and tool projects** (`SlideWorks.LoadTest` and any new one) get the same props, so these also fail there: S2245 (pseudo-random numbers in a security context), S2325 (a method that does not use instance data must be `static`), S3358 (nested ternary), S6966 and AG0020. A brief for a tool project lists them.

## 2. Tests (.NET)

- **NUnit, not xUnit.** Skip with `[Ignore("reason")]`, never `[Fact(Skip = ...)]`.
- **Shouldly.** String `ShouldBe` is already ordinal and has no `Case` overload. `Case.Sensitive` exists only on `ShouldContain`, `ShouldStartWith` and `ShouldEndWith`, as in `SlideWorks.WebApi.Tests/Slides/DeleteSlideTests.cs`: `slides[1].GetProperty("link").GetString()!.ShouldEndWith("#2", Case.Sensitive);`.
- **Narrow a JSON string with `ShouldNotBeNull`, not `!`**, as `Templates/TemplateAssetRoutesTests.cs` does: `body.GetProperty("uploadUrl").GetString().ShouldNotBeNull().ShouldMatch(...)`. Shared test helpers are in `SlideWorks.WebApi.Tests.Support`; name it in a brief.
- **Cases with non-printable characters are written by code point**, never as a literal character or in the test name: `[TestCase("https://good\u200B.example/cb")]` (`src/SlideWorks.WebApi.Tests/OAuth/Registration/RegistrationTests.cs`, line 54). Write a file that contains `\u` escapes with the editor's Write or Edit tool, never through a shell heredoc, which turns the escape into the character.
- **Bounded waits** use `Task.WhenAny` with `Task.Delay`, then throw. This passes AG0032 and AG0052, copied from `Polls/PollPumpTests.cs`:

  ```csharp
  private static async Task WithinAsync(Task task)
  {
      if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) != task)
      {
          throw new TimeoutException("The pump did not publish within 10 seconds.");
      }
      await task;
  }
  ```

- **One app per fixture class.** A fixture shares one app across its tests, so a settable fake (a registry, a clock, a counter) needs a key per test, such as the test's own deck id, or one test sees another's state.
- **Races, time and "no event" checks** are in [testing.md](testing.md): the interleaving checklist, the Gate, the step clock and the marker.
- **No whole suites, no `--repeat-each`.** Run the named tests; CI finds flakes. For a change that touches many journeys, read CI's acceptance job instead of queueing local acceptance runs (each takes about 5 minutes on the shared lock).

## 3. Client and component tests (CT)

- **Mount only imported components.** A component defined inside a spec cannot be mounted. Use a helper function that returns JSX, such as the harnesses in `src/SlideWorks.Client/src/test-support/`.
- **One `mount` per test.**
- **Draw a component chosen at run time with `createElement`** (the `react-hooks/static-components` rule rejects JSX for it): `createElement(SlideNumberContext, { value: slide }, reader)` in `slides/PollContext.test.tsx`.
- **A focus fix after an action** belongs in an effect that runs on the next render.
- **Use `click()`, not `check()`, on an input whose state the app controls asynchronously**: `check()` insists the state changed at once and fails.
- **Check replace versus push with `navigation.currentEntry.key`**, not `history.length` (the CT page is reused and Chromium caps the length). See `entryKey` in `player/Player.spec.tsx`.
- **A JSON import in CT needs an attribute**: `import fitFixturesJson from '@contracts/fit-fixtures/qa.json' with { type: 'json' };` (`slides/layouts/QaLayout.spec.tsx`).
- **`setInputFiles` paths are relative to `src/SlideWorks.Client`.** A file made in the spec is `{ name, mimeType, buffer }`, as `file(...)` in `templates/editor/tabs/LogoArtTab.spec.tsx`.
- **Vitest empties CSS imports** (even with `?raw`). Read the file from disk, as `look/slidesUseTokens.test.ts` does with `@vitest-environment node` and `process.getBuiltinModule('node:fs')` typed by a local `Fs` interface (the client has no Node types).
- **`satisfies` on a JSON import** uses `JsonView` or `Widen` from `test-support/fixtures/widen.ts`: `const sampleDeck = sampleDeckJson satisfies JsonView<DeckPlayView>;` (`contracts/sampleDeck.test.ts`).
- **A11y and error baseline for every client brief:** focus moves to the right place after each action; every button's accessible name includes its row's subject; every failed call shows a message; an auth redirect uses `replace: true`.
- Run `tsc --noEmit` before CT: Playwright CT never type-checks (`tools/agent/test-ct` does it for you).

## 4. DI (Agoda.IoC)

- **Register a class as itself and for an interface with `Concrete = true` plus a Factory registration.** Two plain attributes silently make two instances (two queues, two loops). From `Features/Polls/AnswerWriter.cs`:

  ```csharp
  [RegisterSingleton(Concrete = true)]
  [RegisterSingleton(For = typeof(IRoundAnswerWriter), Factory = typeof(Seam))]
  public sealed class AnswerWriter : IRoundAnswerWriter, IDisposable
  // ...
      public sealed class Seam : IComponentFactory<IRoundAnswerWriter>
      {
          public IRoundAnswerWriter Build(IComponentResolver c) => c.Resolve<AnswerWriter>();
      }
  ```

- **A class derived from a framework type with no interface** (an `OpenIdConnectEvents` subclass) needs `[RegisterPerRequest(Concrete = true)]`; see `Infrastructure/Identity/Google/GoogleSignInEvents.cs`.
- **Registration errors.** `AutoWireAssembly` (called in `Program.cs`) scans the API assembly at startup. A failure comes out as a `RegistrationFailedException` ("There are validations errors") that hides the cause. `Program.cs` passes the `OnRegistrationContextException` hook of `ContainerRegistrationOption` (Agoda.IoC.Core 1.1.144; `AutoWireAssembly` itself is in Agoda.IoC.NetCore), which writes each cause to stderr before the exception propagates, so the test log or console shows it:

  ```csharp
  builder.Services.AutoWireAssembly([ApiMarker.Assembly], isMockMode: false,
      options => options.OnRegistrationContextException = ex => Console.Error.WriteLine(ex.ToString()));
  ```

  Verified output for a `For = typeof(X)` on a class that does not implement `X`: `Agoda.IoC.Core.RegistrationContextException: SlideWorks.WebApi.Sw751BreakA: Explicitly registers For = typeof(SlideWorks.WebApi.ISw751Break), but it does not implement this type.` Look for it near the top of the log, above the `OneTimeSetUp` failure.
- **A request-dependent singleton** trips AG0009 (section 1): pass only the fields needed, or suppress with a stated reason.

## 5. EF migrations

- **Build once before `dotnet ef migrations add` in a fresh worktree** (`tools/agent/bootstrap` builds). The command from AGENTS.md: `SLIDEWORKS_DB_PATH=/tmp/x.db dotnet ef migrations add <Name> --project src/SlideWorks.WebApi`.
- **A generated `new Guid("0000...")` default breaks S4581**, and then `migrations remove` cannot build either. Recover by hand: delete the new migration files (the `.cs` and the `.Designer.cs`), restore `AppDbContextModelSnapshot.cs` from git, then fix the default and add again.
- **A migration that re-keys a SQLite table:** run `dotnet ef migrations script <previous migration>` and check two things before you apply it. Each backfill `UPDATE` comes before every `CREATE UNIQUE INDEX` on the new columns. Every foreign key in an `ef_temp_` table points at a column that is already unique in the old table.
- **A new migration touches a shared file:** `SlideWorks.WebApi.Tests/DatabaseTests.cs` holds `KNOWN_MIGRATIONS`, and a new migration's name goes at the end of that list. The branch still pins it there, so tasks that add migrations collide on that file; the orchestrator should give it one owner.

## 6. MCP tools

- **The SDK serialises a parameter's default into the tool's schema.** For a tri-state argument (absent, null or a value) take `JsonElement? x = null`, as `Features/Slides/Mcp/AddSlidesTool.cs` does with `JsonElement? position = null`.
- **Tools return an object, never an array** (AGENTS.md). Test enum arguments and one result per tool on the raw JSON-RPC wire (`Decks/McpWire.cs`), not only through `McpTestClient`. `Mcp/McpWireCoverageTests.cs` checks every tool on the wire, one case per tool, and fails on a tool with no case: add yours there.
- **Read the schema in a tool error before you retry the call.**

## 7. Agent toolbelt

Agents build, test, push and wait only through `tools/agent/*`; each script's `--help` and the [README](../tools/agent/README.md) give the usage, the RESULT line and the exit codes. The rules the scripts enforce, in short:

- **Push** with `tools/agent/sync-push --branch <shared branch> --test '<command>'` and nothing else: it rebases, rebuilds when the incoming commits touch built files, runs your tests, then pushes.
- **Unverified edits before a rebase** are committed as a WIP commit and pushed to `wip/<KEY>`, or saved as a patch with `git diff > <scratch>/<KEY>-wip.patch`. Never `git stash`: the stash is shared by every worktree.
- **Wait** with `tools/agent/wait <log>`, and only that: one `wait` per Bash command (it gives up after 170 s and a second one in the same command is backgrounded), never a sleep, `until` or `pgrep` loop.
- **Commit subjects** start with the task key and are at most 120 characters. `AGENT_TASK=<KEY> tools/agent/self-check --base <sha>` checks only that task's commits.
- **Flakes and acceptance:** no `--repeat-each`; run acceptance one test per command.
- **Mutation proofs** use `tools/agent/bite`; the recipes are in the README. `test-ct` reports a `test.fail` that fails as expected as `expected_failures=N`, not as a failure.
