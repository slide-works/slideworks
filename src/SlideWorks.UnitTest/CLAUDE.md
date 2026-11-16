# SlideWorks.UnitTest

Use this project sparingly. It is the exception, not the default.

Most tests belong elsewhere, in this order of preference:

1. **`SlideWorks.AcceptanceTests`**: Playwright against the real BFF and the built SPA bundle. Best for anything a user can see or do.
2. **`SlideWorks.WebApi.Tests`**: black-box tests through `WebApplicationFactory` and the HTTP surface. Best for API behaviour, status codes, persistence and auth.
3. **`SlideWorks.UnitTest`** (here): last resort.

## What belongs here

Only pure logic in `SlideWorks.Core` that is awkward to reach through the layers above: helper methods, formatters, parsers, calculations with many edge cases. No I/O, no DI container, no HTTP.

`SlideWorks.Core` is referenced only by `SlideWorks.WebApi` and this project. This project references nothing else; do not add a reference to `SlideWorks.WebApi`.

## What does not belong here

- Controllers, services, middleware or anything wired through DI: test it through the HTTP surface in `SlideWorks.WebApi.Tests`.
- Mocks of our own types. If a test needs one, it is at the wrong level.
- A test per method for coverage's sake. If an acceptance or API test already pins the behaviour, do not duplicate it here.

Naming is `Method_When_Should`, as everywhere (`.claude/skills/csharp-testing-strategy/`). Never weaken or delete an existing test (`.claude/skills/preserve-existing-tests/`).
